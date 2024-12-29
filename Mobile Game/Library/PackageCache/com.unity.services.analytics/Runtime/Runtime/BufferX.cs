using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Unity.Services.Analytics.Internal
{
    internal interface IBufferSystemCalls
    {
        string GenerateGuid();
        DateTime Now();
        TimeSpan GetTimeZoneUtcOffset(DateTime dateTime);
    }

    class BufferSystemCalls : IBufferSystemCalls
    {
        public string GenerateGuid()
        {
            // NOTE: we are not using .ToByteArray because we do actually need a valid string.
            // Even though the buffer is all bytes, it is ultimately for JSON, so it has to be
            // UTF8 string bytes rather than raw bytes (the string also includes hyphenated
            // subsections).
            return Guid.NewGuid().ToString();
        }

        public DateTime Now()
        {
            return DateTime.Now;
        }

        public TimeSpan GetTimeZoneUtcOffset(DateTime dateTime)
        {
            return TimeZoneInfo.Local.GetUtcOffset(dateTime);
        }
    }

    internal struct EventSummary
    {
        internal int StartIndex;
        internal int EndIndex;
        internal string Id;
    }

    class BufferX : IBuffer, IBufferDebug
    {
        // 4MB: 4 * 1024 KB to make an MB and * 1024 bytes to make a KB
        // The Collect endpoint can actually accept payloads of up to 5MB (at time of writing, Jan 2023),
        // but we want to retain some headroom... just in case.
        const long k_UploadBatchMaximumSizeInBytes = 4 * 1024 * 1024;
        const string k_MillisecondDateFormat = "yyyy-MM-dd HH:mm:ss.fff zzz";

        readonly byte[] k_WorkingBuffer;
        readonly char[] k_WorkingCharacterBuffer;

        readonly byte[] k_PayloadHeader;

        readonly byte[] k_HeaderEventName;
        readonly byte[] k_HeaderUserName;
        readonly byte[] k_HeaderSessionID;
        readonly byte[] k_HeaderEventUUID;
        readonly byte[] k_HeaderTimestamp;

        readonly byte[] k_HeaderEventVersion;
        readonly byte[] k_HeaderInstallationID;
        readonly byte[] k_HeaderPlayerID;

        readonly byte[] k_HeaderOpenEventParams;
        readonly byte[] k_CloseEvent;

        readonly byte k_Quote;
        readonly byte[] k_QuoteColon;
        readonly byte[] k_QuoteComma;
        readonly byte[] k_Comma;
        readonly byte[] k_OpenBrace;
        readonly byte[] k_CloseBraceComma;
        readonly byte[] k_OpenBracket;
        readonly byte[] k_CloseBracketComma;

        readonly byte k_Colon;
        readonly byte k_Dash;
        readonly byte k_Space;
        readonly byte k_Point;
        readonly byte k_Positive;
        readonly byte k_Negative;

        readonly byte[] k_True;
        readonly byte[] k_False;
        readonly byte[] k_Int2CharacterByte;
        readonly long[] k_Order;

        readonly IBufferSystemCalls m_SystemCalls;
        readonly IDiskCache m_DiskCache;
        readonly IIdentityManager m_UserIdentity;
        readonly ISessionManager m_Session;

        readonly List<EventSummary> m_EventSummaries;
        string m_CurrentEventId;
        string m_CurrentEventName;
        DateTime m_CurrentEventTimestamp;

        MemoryStream m_SpareBuffer;
        MemoryStream m_Buffer;

        public int Length { get { return (int)m_Buffer.Length; } }

        /// <summary>
        /// The number of events that have been recorded into this buffer.
        /// Only exposed for unit testing.
        /// </summary>
        internal int EventsRecorded { get { return m_EventSummaries.Count; } }

        /// <summary>
        /// The metadata associated with each event blob in the bytestream.
        /// Only exposed for unit testing.
        /// </summary>
        internal IReadOnlyList<EventSummary> EventSummaries { get { return m_EventSummaries; } }

        /// <summary>
        /// The raw contents of the underlying bytestream.
        /// Only exposed for unit testing.
        /// </summary>
        internal byte[] RawContents { get { return m_Buffer.ToArray(); } }

        public event Action<string, string, DateTime, byte[]> EventRecorded;
        public event Action<HashSet<string>> EventsClearing;
        public event Action<HashSet<string>> EventsCleared;

        public BufferX(IBufferSystemCalls eventIdGenerator, IDiskCache diskCache, IIdentityManager userIdentity, ISessionManager session)
        {
            m_Buffer = new MemoryStream((int)k_UploadBatchMaximumSizeInBytes);
            m_SpareBuffer = new MemoryStream((int)k_UploadBatchMaximumSizeInBytes);
            m_EventSummaries = new List<EventSummary>();

            m_SystemCalls = eventIdGenerator;
            m_DiskCache = diskCache;
            m_UserIdentity = userIdentity;
            m_Session = session;

            // Transaction receipts can be over 1MB in size, so we really do need working buffers that are this large.
            // Since a single event exceeding the maximum batch size would be eliminated from the buffer, a single
            // string parameter within that event should also never reach that size, so this should be a safe limit.
            k_WorkingBuffer = new byte[k_UploadBatchMaximumSizeInBytes];
            k_WorkingCharacterBuffer = new char[k_UploadBatchMaximumSizeInBytes];

            k_PayloadHeader = Encoding.UTF8.GetBytes("{\"eventList\":[");

            k_HeaderEventName = Encoding.UTF8.GetBytes("{\"eventName\":\"");
            k_HeaderUserName = Encoding.UTF8.GetBytes("\",\"userID\":\"");
            k_HeaderSessionID = Encoding.UTF8.GetBytes("\",\"sessionID\":\"");
            k_HeaderEventUUID = Encoding.UTF8.GetBytes("\",\"eventUUID\":\"");
            k_HeaderTimestamp = Encoding.UTF8.GetBytes("\",\"eventTimestamp\":\"");

            k_HeaderEventVersion = Encoding.UTF8.GetBytes("\"eventVersion\":");
            k_HeaderInstallationID = Encoding.UTF8.GetBytes("\"unityInstallationID\":\"");
            k_HeaderPlayerID = Encoding.UTF8.GetBytes("\"unityPlayerID\":\"");

            k_HeaderOpenEventParams = Encoding.UTF8.GetBytes("\"eventParams\":{");

            // Close params block, close event object, comma to prepare for next event
            k_CloseEvent = Encoding.UTF8.GetBytes("}},");

            k_Quote = Encoding.UTF8.GetBytes("\"")[0];
            k_QuoteColon = Encoding.UTF8.GetBytes("\":");
            k_QuoteComma = Encoding.UTF8.GetBytes("\",");
            k_Comma = Encoding.UTF8.GetBytes(",");

            k_OpenBrace = Encoding.UTF8.GetBytes("{");
            k_CloseBraceComma = Encoding.UTF8.GetBytes("},");
            k_OpenBracket = Encoding.UTF8.GetBytes("[");
            k_CloseBracketComma = Encoding.UTF8.GetBytes("],");

            k_Colon = Encoding.UTF8.GetBytes(":")[0];
            k_Dash = Encoding.UTF8.GetBytes("-")[0];
            k_Space = Encoding.UTF8.GetBytes(" ")[0];
            k_Point = Encoding.UTF8.GetBytes(".")[0];
            k_Positive = Encoding.UTF8.GetBytes("+")[0];
            k_Negative = Encoding.UTF8.GetBytes("-")[0];

            k_True = Encoding.UTF8.GetBytes("true");
            k_False = Encoding.UTF8.GetBytes("false");
            k_Int2CharacterByte = new byte[]
            {
                (byte)'0',
                (byte)'1',
                (byte)'2',
                (byte)'3',
                (byte)'4',
                (byte)'5',
                (byte)'6',
                (byte)'7',
                (byte)'8',
                (byte)'9'
            };
            k_Order = new long[]
            {
                1,
                10,
                100,
                1000,
                10000,
                100000,
                1000000,
                10000000,
                100000000,
                1000000000,
                10000000000,
                100000000000,
                1000000000000,
                10000000000000,
                100000000000000,
                1000000000000000,
                10000000000000000,
                100000000000000000,
                1000000000000000000
            };

            ClearBuffer();
        }

        private void WriteString(in string value)
        {
            int length = Encoding.UTF8.GetBytes(value, 0, Mathf.Min(value.Length, k_WorkingBuffer.Length), k_WorkingBuffer, 0);
            m_Buffer.Write(k_WorkingBuffer, 0, length);
        }

        private void WriteLong(in long value)
        {
            int length = SerializeLong(value, k_WorkingBuffer, 0, 0);
            m_Buffer.Write(k_WorkingBuffer, 0, length);
        }

        private void WriteByte(in byte value)
        {
            m_Buffer.WriteByte(value);
        }

        private void WriteBytes(in byte[] bytes)
        {
            m_Buffer.Write(bytes, 0, bytes.Length);
        }

        private void WriteName(string name)
        {
            if (name != null)
            {
                WriteByte(k_Quote);
                WriteString(name);
                WriteBytes(k_QuoteColon);
            }
        }

        private void WriteDateTime(DateTime dateTime)
        {
            // "yyyy-MM-dd HH:mm:ss.fff zzz"
            // -> "2023-07-20 11:50:41.023 +01:00"
            SerializeLong(dateTime.Year, k_WorkingBuffer, 0, 4);
            k_WorkingBuffer[4] = k_Dash;
            SerializeLong(dateTime.Month, k_WorkingBuffer, 5, 2);
            k_WorkingBuffer[7] = k_Dash;
            SerializeLong(dateTime.Day, k_WorkingBuffer, 8, 2);
            k_WorkingBuffer[10] = k_Space;
            SerializeLong(dateTime.Hour, k_WorkingBuffer, 11, 2);
            k_WorkingBuffer[13] = k_Colon;
            SerializeLong(dateTime.Minute, k_WorkingBuffer, 14, 2);
            k_WorkingBuffer[16] = k_Colon;
            SerializeLong(dateTime.Second, k_WorkingBuffer, 17, 2);
            k_WorkingBuffer[19] = k_Point;
            SerializeLong(dateTime.Millisecond, k_WorkingBuffer, 20, 3);
            k_WorkingBuffer[23] = k_Space;

            // Can we cache the offset? Then, what if DST/etc changes during a session?
            // Can we guarantee that we are always serialising NOW and not some arbitrary date from the past?
            // Answers: no, not really. So always get the offset fresh.
            TimeSpan offset = m_SystemCalls.GetTimeZoneUtcOffset(dateTime);

            // NOTE: timezone offset is all negative or all positive, so we put the symbol in front and
            // then take the Absolute values of the individual parts.
            k_WorkingBuffer[24] = offset.Ticks < 0 ? k_Negative : k_Positive;
            SerializeLong(Mathf.Abs(offset.Hours), k_WorkingBuffer, 25, 2);
            k_WorkingBuffer[27] = k_Colon;
            SerializeLong(Mathf.Abs(offset.Minutes), k_WorkingBuffer, 28, 2);

            m_Buffer.Write(k_WorkingBuffer, 0, 30);
        }

        /// <summary>
        /// Serializes a given long integer to a string, in the form of an array of UTF8 bytes.
        ///
        /// This comes with fewer memory allocations than using traditional Int64.ToString(), as
        /// it bypasses .NET's built-in culture-sensitive number formatting logic. We know
        /// it is safe to bypass this logic because we know the output is for machine-readable JSON
        /// and not for display to humans.
        ///
        /// </summary>
        /// <param name="number">The number to serialize</param>
        /// <param name="buffer">The buffer into which to serialize the number</param>
        /// <param name="startIndex">The index in the buffer from which to start writing</param>
        /// <param name="minimumLength">If the output string is shorter than this value, pad it with zeroes to meet the requirement (e.g. 451 => 0451)</param>
        /// <returns>The number of bytes added to the buffer</returns>
        private int SerializeLong(in long number, in byte[] buffer, in int startIndex, in int minimumLength)
        {
            if (number == 0)
            {
                for (int i = 0; i <= minimumLength; i++)
                {
                    buffer[startIndex + i] = k_Int2CharacterByte[0];
                }
                return Mathf.Max(1, minimumLength);
            }
            else
            {
                long absolute = Math.Abs(number);

                // "order" here is the number of digits, i.e. characters in the string version.
                // E.g. 1337 has an order of 4.
                // https://stackoverflow.com/questions/6864991/how-to-get-the-size-of-a-number-in-net
                // NOTE: using System.Math for Log10 instead of Unity's Mathf because we need the extra
                // precision of a double. Using Mathf and floats here causes issues when serialising very
                // large numbers.
                int order = (int)(Math.Log10(Math.Max(absolute, 0.5)) + 1);

                // Factor in the minimum desired length, so that we pad with 0s if necessary.
                // E.g. if you want to write 1337 in a field that must be 6 digits long, produce 001337.
                order = Mathf.Max(minimumLength, order);

                int start = startIndex;
                int length = order;
                if (number < 0)
                {
                    buffer[start] = k_Negative;
                    start++;
                    length++;
                }

                // Walk down the orders of magnitude to get each digit for the string from left-to-right.
                // E.g. 1337 means 1 thousand, 3 hundreds, 3 tens, and 7 ones.
                long accumulator = absolute;
                for (int i = order; i > 0; i--)
                {
                    long digit = accumulator / k_Order[i - 1];
                    accumulator = accumulator % k_Order[i - 1];
                    buffer[start + order - i] = k_Int2CharacterByte[digit];
                }

                return length;
            }
        }

        public void PushStandardEventStart(string name, int version)
        {
            PushCommonEventStart(name);

            WriteBytes(k_HeaderEventVersion);
            WriteLong(version);
            WriteBytes(k_Comma);

            WriteBytes(k_HeaderInstallationID);
            WriteString(m_UserIdentity.InstallId);
            WriteBytes(k_QuoteComma);

            if (!String.IsNullOrEmpty(m_UserIdentity.PlayerId))
            {
                WriteBytes(k_HeaderPlayerID);
                WriteString(m_UserIdentity.PlayerId);
                WriteBytes(k_QuoteComma);
            }

            WriteBytes(k_HeaderOpenEventParams);
        }

        public void PushCustomEventStart(string name)
        {
            PushCommonEventStart(name);

            WriteBytes(k_HeaderOpenEventParams);
        }

        private void PushCommonEventStart(string name)
        {
            m_CurrentEventTimestamp = m_SystemCalls.Now();
            m_CurrentEventId = m_SystemCalls.GenerateGuid();
            m_CurrentEventName = name;

#if UNITY_ANALYTICS_EVENT_LOGS
            Debug.LogFormat("Recording event {0} at {1} (UTC)...", m_CurrentEventName, SerializeDateTime(m_CurrentEventTimestamp));
#endif
            WriteBytes(k_HeaderEventName);
            WriteString(m_CurrentEventName);
            WriteBytes(k_HeaderUserName);
            WriteString(m_UserIdentity.UserId);
            WriteBytes(k_HeaderSessionID);
            WriteString(m_Session.SessionId);
            WriteBytes(k_HeaderEventUUID);
            WriteString(m_CurrentEventId);
            WriteBytes(k_HeaderTimestamp);
            WriteDateTime(m_CurrentEventTimestamp);
            WriteBytes(k_QuoteComma);
        }

        private void StripTrailingCommaIfNecessary()
        {
            // Stripping the comma once at the end of something is probably
            // faster than checking to see if we need to add one before
            // every single property inside it. Even though it seems
            // a bit convoluted.

            m_Buffer.Seek(-1, SeekOrigin.End);
            char precedingChar = (char)m_Buffer.ReadByte();
            if (precedingChar == ',')
            {
                // Burn that comma, we don't need it and it breaks JSON!
                m_Buffer.Seek(-1, SeekOrigin.Current);
                m_Buffer.SetLength(m_Buffer.Length - 1);
            }
        }

        public void PushEndEvent()
        {
            StripTrailingCommaIfNecessary();

            WriteBytes(k_CloseEvent);

            int bufferLength = (int)m_Buffer.Length;

            int startIndex = m_EventSummaries.Count > 0
                ? m_EventSummaries[m_EventSummaries.Count - 1].EndIndex
                : 0;
            int endIndex = bufferLength;

            // If this event is too big to ever be uploaded, clear the buffer so we don't get stuck forever.
            int eventSize = endIndex - startIndex;

            if (eventSize > k_UploadBatchMaximumSizeInBytes)
            {
                Debug.LogWarning($"Detected event that would be too big to upload (greater than {k_UploadBatchMaximumSizeInBytes / 1024}KB in size), discarding it to prevent blockage.");

                int previousBufferLength = m_EventSummaries.Count > 0 ? m_EventSummaries[m_EventSummaries.Count - 1].EndIndex : 0;

                m_Buffer.SetLength(previousBufferLength);
                m_Buffer.Position = previousBufferLength;
            }
            else
            {
                m_EventSummaries.Add(new EventSummary
                {
                    StartIndex = startIndex,
                    EndIndex = endIndex,
                    Id = m_CurrentEventId
                });

                if (EventRecorded != null)
                {
                    long currentIndex = m_Buffer.Position;

                    // Pull the buffer head back so we can copy out this event's complete body.
                    m_Buffer.Seek(startIndex, SeekOrigin.Begin);

                    int eventLength = endIndex - startIndex;
                    byte[] eventBody = new byte[eventLength];
                    m_Buffer.Read(eventBody, 0, eventLength);

                    // Reset position so we can add more events
                    m_Buffer.Seek(currentIndex, SeekOrigin.Begin);

                    EventRecorded(m_CurrentEventId, m_CurrentEventName, m_CurrentEventTimestamp, eventBody);
                }

#if UNITY_ANALYTICS_DEVELOPMENT
                Debug.Log($"Event {m_EventSummaries.Count} ended at: {bufferLength}");
#endif
            }
        }

        public void PushObjectStart(string name)
        {
            WriteName(name);
            WriteBytes(k_OpenBrace);
        }

        public void PushObjectEnd()
        {
            StripTrailingCommaIfNecessary();
            WriteBytes(k_CloseBraceComma);
        }

        public void PushArrayStart(string name)
        {
            WriteName(name);
            WriteBytes(k_OpenBracket);
        }

        public void PushArrayEnd()
        {
            StripTrailingCommaIfNecessary();

            WriteBytes(k_CloseBracketComma);
        }

        public void PushDouble(string name, double value)
        {
            WriteName(name);
            WriteString(value.ToString(CultureInfo.InvariantCulture));
            WriteBytes(k_Comma);
        }

        public void PushFloat(string name, float value)
        {
            WriteName(name);
            WriteString(value.ToString(CultureInfo.InvariantCulture));
            WriteBytes(k_Comma);
        }

        public void PushString(string name, string value)
        {
#if UNITY_ANALYTICS_DEVELOPMENT
            Debug.AssertFormat(!String.IsNullOrEmpty(value), "Required to have a value");
#endif
            if (Encoding.UTF8.GetByteCount(value) < k_WorkingBuffer.Length)
            {
                int workingBufferHead = 0;
                for (int i = 0; i < value.Length; i++)
                {
                    workingBufferHead += ProcessCharacterOntoWorkingBuffer(workingBufferHead, value[i]);

                    if (workingBufferHead >= k_WorkingCharacterBuffer.Length)
                    {
                        // If working index has tripped over the escaped buffer length, then adding the extra escape codes
                        // has made this value too big to process. We will no longer accept this value.
                        // Truncate the string so it doesn't obliterate your log file.
                        Debug.LogWarning($"String value for field {name} is too long, it will not be recorded.\nValue:\n{value.Substring(0, 128)}...");
                        break;
                    }
                }

                if (workingBufferHead < k_WorkingCharacterBuffer.Length)
                {
                    WriteName(name);
                    WriteByte(k_Quote);

                    int valueLength = Encoding.UTF8.GetBytes(k_WorkingCharacterBuffer, 0, workingBufferHead, k_WorkingBuffer, 0);
                    m_Buffer.Write(k_WorkingBuffer, 0, valueLength);

                    WriteBytes(k_QuoteComma);
                }
            }
            else
            {
                // Truncate the string so it doesn't obliterate your log file.
                Debug.LogWarning($"String value for field \"{name}\" is too long, it will not be recorded.\nValue:\n{value.Substring(0, 128)}...");
            }
        }

        private int ProcessCharacterOntoWorkingBuffer(int index, char character)
        {
            // Newline, etc.
            if (Char.IsControl(character))
            {
                // Converting to e.g. \U000A rather than \n is not normal, but it is valid JSON.
                // This gives us a reliable way to escape any control character.
                // We will allocate a small string here to generate the control code, but it
                // should be relatively rare (i.e. only if a control char is even present).
                int length = 0;
                int codepoint = Convert.ToInt32(character);
                string control = $"\\U{codepoint:X4}";
                for (int j = 0; j < control.Length; j++)
                {
                    k_WorkingCharacterBuffer[index + j] = control[j];
                    length++;
                }
                return length;
            }
            // JSON structural characters, quote and slash.
            else if (character == '"' || character == '\\')
            {
                k_WorkingCharacterBuffer[index] = '\\';
                k_WorkingCharacterBuffer[index + 1] = character;
                return 2;
            }
            // Normal text
            else
            {
                k_WorkingCharacterBuffer[index] = character;
                return 1;
            }
        }

        public void PushInt64(string name, long value)
        {
            WriteName(name);
            WriteLong(value);
            WriteBytes(k_Comma);
        }

        public void PushInt(string name, int value)
        {
            PushInt64(name, value);
        }

        public void PushBool(string name, bool value)
        {
            WriteName(name);
            if (value)
            {
                WriteBytes(k_True);
            }
            else
            {
                WriteBytes(k_False);
            }
            WriteBytes(k_Comma);
        }

        public void PushTimestamp(string name, DateTime value)
        {
            WriteName(name);
            WriteByte(k_Quote);
            WriteDateTime(value);
            WriteBytes(k_QuoteComma);
        }

        public void PushProduct(string name, TransactionRealCurrency realCurrency, List<TransactionVirtualCurrency> virtualCurrencies, List<TransactionItem> items)
        {
            PushObjectStart(name);

            if (realCurrency != null)
            {
                PushObjectStart("realCurrency");
                realCurrency.Serialize(this);
                PushObjectEnd();
            }

            if (virtualCurrencies.Count > 0)
            {
                PushArrayStart("virtualCurrencies");
                foreach (TransactionVirtualCurrency virtualCurrency in virtualCurrencies)
                {
                    PushObjectStart(null);
                    PushObjectStart("virtualCurrency");
                    virtualCurrency.Serialize(this);
                    PushObjectEnd();
                    PushObjectEnd();
                }
                PushArrayEnd();
            }

            if (items.Count > 0)
            {
                PushArrayStart("items");
                foreach (TransactionItem item in items)
                {
                    PushObjectStart(null);
                    PushObjectStart("item");
                    item.Serialize(this);
                    PushObjectEnd();
                    PushObjectEnd();
                }
                PushArrayEnd();
            }

            PushObjectEnd();
        }

        /// <summary>
        /// Attempts to serialize an object of unknown type. Supports nested objects through IDictionary and IList.
        /// Null or unknown values wll be ignored (will not be pushed to the buffer).
        /// Supported types are:
        /// - string
        /// - int/long
        /// - float/double
        /// - bool
        /// - DateTime
        /// - Enum
        /// - IDictionary<string,object>
        /// - IList<object>
        /// </summary>
        public void PushObject(string name, object value)
        {
            if (value != null)
            {
                /*
                 * Had a read of the performance of typeof - the two options were a switch on Type.GetTypeCode(paramType) or
                 * the if chain below. Although the if statement involves multiple typeofs, this is supposedly a fairly light
                 * operation, and the alternative switch option involved some messy/crazy cases for ints.
                 */
                Type paramType = value.GetType();
                if (paramType == typeof(string))
                {
                    PushString(name, (string)value);
                }
                else if (paramType == typeof(int))
                {
                    PushInt(name, (int)value);
                }
                else if (paramType == typeof(long))
                {
                    PushInt64(name, (long)value);
                }
                else if (paramType == typeof(float))
                {
                    PushFloat(name, (float)value);
                }
                else if (paramType == typeof(double))
                {
                    PushDouble(name, (double)value);
                }
                else if (paramType == typeof(bool))
                {
                    PushBool(name, (bool)value);
                }
                else if (paramType == typeof(DateTime))
                {
                    PushTimestamp(name, (DateTime)value);
                }
                // NOTE: since these are not primitive types, we can't rely on the faster typeof check for these parts.
                else if (value is Enum e)
                {
                    PushString(name, e.ToString());
                }
                else if (value is IDictionary<string, object> dictionary)
                {
                    PushObjectStart(name);

                    foreach (KeyValuePair<string, object> paramPair in dictionary)
                    {
                        PushObject(paramPair.Key, paramPair.Value);
                    }

                    PushObjectEnd();
                }
                else if (value is IList<object> list)
                {
                    if (list.Count > 0)
                    {
                        PushArrayStart(name);

                        for (int i = 0; i < list.Count; i++)
                        {
                            PushObject(null, list[i]);
                        }

                        PushArrayEnd();
                    }
                }
            }
        }

        public byte[] Serialize()
        {
            if (m_EventSummaries.Count > 0)
            {
                long originalBufferPosition = m_Buffer.Position;

                // Tick through the event end indices until we find the last complete event
                // that fits into the maximum payload size.
                int end = m_EventSummaries[0].EndIndex;
                int nextEnd = 0;
                while (nextEnd < m_EventSummaries.Count &&
                       m_EventSummaries[nextEnd].EndIndex < k_UploadBatchMaximumSizeInBytes)
                {
                    end = m_EventSummaries[nextEnd].EndIndex;
                    nextEnd++;
                }

                // This event will only be subscribed to by editor tools, so if there is no subscription,
                // don't do the extra work to assemble the list of event IDs.
                if (EventsClearing != null)
                {
                    HashSet<string> clearingEventIds = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < nextEnd; i++)
                    {
                        clearingEventIds.Add(m_EventSummaries[i].Id);
                    }

                    EventsClearing(clearingEventIds);
                }

                // Extend the payload so we can fit the suffix.
                byte[] payload = new byte[k_PayloadHeader.Length + end + 1];
                k_PayloadHeader.CopyTo(payload, 0);
                m_Buffer.Position = 0;
                m_Buffer.Read(payload, k_PayloadHeader.Length, end);

                // NOTE: the final character will be a comma that we don't want,
                // so take this opportunity to overwrite it with the closing
                // bracket (event list) and brace (payload object).
                byte[] suffix = Encoding.UTF8.GetBytes("]}");
                payload[k_PayloadHeader.Length + end - 1] = suffix[0];
                payload[k_PayloadHeader.Length + end] = suffix[1];

                m_Buffer.Position = originalBufferPosition;

                return payload;
            }
            else
            {
                return null;
            }
        }

        public void ClearBuffer()
        {
            m_Buffer.SetLength(0);
            m_Buffer.Position = 0;

            m_EventSummaries.Clear();
        }

        public void ClearBuffer(long upTo)
        {
            // The number of events may be zero here if somebody else has cleared the buffer, out of step with the
            // dispatcher getting a response. E.g. if the player triggered a data deletion request while the flush
            // request was in flight.
            // If the buffer is already empty, we do not need to do any work.
            if (m_EventSummaries.Count > 0)
            {
                MemoryStream oldBuffer = m_Buffer;
                m_Buffer = m_SpareBuffer;
                m_SpareBuffer = oldBuffer;

                // We want to keep the start and end markers for events that have been copied over.
                // We have to account for the start point change AND remove markers for events before the clear point.

                int lastClearedEventIndex = 0;
                int shift = (int)upTo;
                for (int i = 0; i < m_EventSummaries.Count; i++)
                {
                    EventSummary summary = m_EventSummaries[i];
                    summary.StartIndex -= shift;
                    summary.EndIndex -= shift;
                    m_EventSummaries[i] = summary;
                    if (m_EventSummaries[i].EndIndex <= 0)
                    {
                        lastClearedEventIndex = i;
                    }
                }

                // If the debug panel has not subscribed to the event, then there is no need to do the
                // extra work of extract the set of uploaded event IDs. This will preserve our runtime
                // performance while still allowing the editor to snoop when nececssary.
                if (EventsCleared != null)
                {
                    HashSet<string> clearedEventIds = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i <= lastClearedEventIndex; i++)
                    {
                        clearedEventIds.Add(m_EventSummaries[i].Id);
                    }

                    EventsCleared(clearedEventIds);
                }

                m_EventSummaries.RemoveRange(0, lastClearedEventIndex + 1);

                // Reset the buffer back to a blank state...
                m_Buffer.SetLength(0);
                m_Buffer.Position = 0;

                // ... and copy over anything that came after the cut-off point.
                m_SpareBuffer.Position = upTo;
                for (long i = upTo; i < m_SpareBuffer.Length; i++)
                {
                    byte b = (byte)m_SpareBuffer.ReadByte();
                    m_Buffer.WriteByte(b);
                }

                m_SpareBuffer.SetLength(0);
                m_SpareBuffer.Position = 0;
            }
        }

        public void FlushToDisk()
        {
            m_DiskCache.Write(m_EventSummaries, m_Buffer);
        }

        public void ClearDiskCache()
        {
            m_DiskCache.Clear();
        }

        public void LoadFromDisk()
        {
            bool success = m_DiskCache.Read(m_EventSummaries, m_Buffer);

            if (!success)
            {
                // Reset the buffer in case we failed half-way through populating it.
                ClearBuffer();
            }
        }

        internal static string SerializeDateTime(DateTime dateTime)
        {
            return dateTime.ToString(k_MillisecondDateFormat, CultureInfo.InvariantCulture);
        }
    }
}
