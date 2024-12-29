using System;
using System.Collections.Generic;
using Unity.Services.Analytics.Internal;

namespace Unity.Services.Analytics
{
    /// <summary>
    /// The Event class is a write-only container designed for you to safely and efficiently pass event data
    /// into the Analytics SDK for batching and upload to the back-end.
    ///
    /// Each of the Standard events can be recorded using the sub-classes provided. Custom events can be recorded
    /// either by creating specific sub-classes of Event or using the more generic CustomEvent class provided.
    ///
    /// For more information about creating and recording Custom events, see the documentation page:
    /// https://docs.unity.com/ugs/en-us/manual/analytics/manual/record-custom-events
    /// </summary>
    public abstract class Event
    {
        private protected readonly Dictionary<string, string> m_Strings;
        private protected readonly Dictionary<string, long> m_Integers;
        private protected readonly Dictionary<string, bool> m_Booleans;
        private protected readonly Dictionary<string, double> m_Floats;

        internal readonly string Name;
        internal readonly bool StandardEvent;
        internal readonly int EventVersion;

        protected Event(string name)
        {
            Name = name;
            m_Strings = new Dictionary<string, string>(StringComparer.Ordinal);
            m_Integers = new Dictionary<string, long>(StringComparer.Ordinal);
            m_Booleans = new Dictionary<string, bool>(StringComparer.Ordinal);
            m_Floats = new Dictionary<string, double>(StringComparer.Ordinal);
        }

        internal Event(string name, bool standardEvent, int eventVersion) : this(name)
        {
            StandardEvent = standardEvent;
            EventVersion = eventVersion;
        }

        /// <summary>
        /// Sets a string value for the given parameter name.
        /// </summary>
        /// <param name="name">The name of this parameter, as defined in the event schema.</param>
        /// <param name="value">The value to store for this parameter.</param>
        protected void SetParameter(string name, string value)
        {
            m_Strings[name] = value;
        }

        /// <summary>
        /// Sets a Boolean value for the given parameter name.
        /// </summary>
        /// <param name="name">The name of this parameter, as defined in the event schema.</param>
        /// <param name="value">The value to store for this parameter.</param>
        protected void SetParameter(string name, bool value)
        {
            m_Booleans[name] = value;
        }

        /// <summary>
        /// Sets an integer value for the given parameter name.
        /// </summary>
        /// <param name="name">The name of this parameter, as defined in the event schema.</param>
        /// <param name="value">The value to store for this parameter.</param>
        protected void SetParameter(string name, int value)
        {
            SetParameter(name, (long)value);
        }

        /// <summary>
        /// Sets an integer value for the given parameter name.
        /// </summary>
        /// <param name="name">The name of this parameter, as defined in the event schema.</param>
        /// <param name="value">The value to store for this parameter.</param>
        protected void SetParameter(string name, long value)
        {
            m_Integers[name] = value;
        }

        /// <summary>
        /// Sets a float value for the given parameter name.
        /// </summary>
        /// <param name="name">The name of this parameter, as defined in the event schema.</param>
        /// <param name="value">The value to store for this parameter.</param>
        protected void SetParameter(string name, float value)
        {
            SetParameter(name, (double)value);
        }

        /// <summary>
        /// Sets a float value for the given parameter name.
        /// </summary>
        /// <param name="name">The name of this parameter, as defined in the event schema.</param>
        /// <param name="value">The value to store for this parameter.</param>
        protected void SetParameter(string name, double value)
        {
            m_Floats[name] = value;
        }

        internal virtual void Serialize(IBuffer buffer)
        {
            foreach (KeyValuePair<string, string> kvp in m_Strings)
            {
                buffer.PushString(kvp.Key, kvp.Value);
            }

            foreach (KeyValuePair<string, long> kvp in m_Integers)
            {
                buffer.PushInt64(kvp.Key, kvp.Value);
            }

            foreach (KeyValuePair<string, double> kvp in m_Floats)
            {
                buffer.PushDouble(kvp.Key, kvp.Value);
            }

            foreach (KeyValuePair<string, bool> kvp in m_Booleans)
            {
                buffer.PushBool(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Note that failing validation will not prevent event serialisation or upload.
        /// This method is intended to produce warning logs to assist in implementation
        /// and debugging. Events will still be validated properly by the server on reaching it.
        /// </summary>
        public virtual void Validate()
        {
        }

        /// <summary>
        /// Use this when implementing the Validate method to determine if a required parameter
        /// has been set or not.
        /// </summary>
        /// <param name="name">The name of the parameter</param>
        /// <returns>True if the parameter has been set, otherwise false</returns>
        protected bool ParameterHasBeenSet(string name)
        {
            return m_Strings.ContainsKey(name) ||
                m_Integers.ContainsKey(name) ||
                m_Floats.ContainsKey(name) ||
                m_Booleans.ContainsKey(name);
        }

        /// <summary>
        /// Clears all parameters and values so that the instance can be reused.
        /// </summary>
        public virtual void Reset()
        {
            m_Strings.Clear();
            m_Integers.Clear();
            m_Booleans.Clear();
            m_Floats.Clear();
        }

        internal static string[] BakeEnum2String<T>(bool toUpper = false) where T : Enum
        {
            // Pre-bake the string versions of the enums so we don't reallocate them every
            // time somebody sets one.
            // We're using this slightly complicated cooker rather than simply hand-rolling
            // the values so that we won't accidentally forget to update a list if we extend
            // or change the underlying enum.
            Array values = Enum.GetValues(typeof(T));
            string[] result = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                if (toUpper)
                {
                    result[i] = values.GetValue(i).ToString().ToUpperInvariant();
                }
                else
                {
                    result[i] = values.GetValue(i).ToString();
                }
            }
            return result;
        }
    }
}
