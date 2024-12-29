using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Unity.Services.Analytics.Internal
{
    internal interface IDiskCache
    {
        /// <summary>
        /// Deletes the cache file (if one exists).
        /// </summary>
        void Clear();

        /// <summary>
        /// Compiles the provided list of indices and payload buffer into a binary file and saves it to disk.
        /// </summary>
        /// <param name="eventSummaries">A list of metadata about events that maps to sections of the stream</param>
        /// <param name="payload">The raw UTF8 byte stream of event data</param>
        void Write(List<EventSummary> eventSummaries, Stream payload);

        /// <summary>
        /// Clears and overwrites the contents of the provided metadata list and buffer with data from a binary file from disk.
        /// </summary>
        /// <param name="eventSummaries">A list of metadata about events that maps to sections of the stream</param>
        /// <param name="buffer">The raw UTF8 byte stream of event data</param>
        bool Read(List<EventSummary> eventSummaries, Stream buffer);
    }

    internal interface IFileSystemCalls
    {
        bool CanAccessFileSystem();
        bool FileExists(string path);
        void DeleteFile(string path);

        Stream OpenFileForWriting(string path);
        Stream OpenFileForReading(string path);
    }

    internal class FileSystemCalls : IFileSystemCalls
    {
        readonly bool m_CanAccessFileSystem;

        internal FileSystemCalls()
        {
            m_CanAccessFileSystem =
                // Switch requires a specific setup to have write access to the disc so it won't be handled here.
                Application.platform != RuntimePlatform.Switch &&
                Application.platform != RuntimePlatform.GameCoreXboxOne &&
                Application.platform != RuntimePlatform.GameCoreXboxSeries &&
                Application.platform != RuntimePlatform.PS5 &&
                Application.platform != RuntimePlatform.PS4 &&
                !String.IsNullOrEmpty(Application.persistentDataPath);
        }

        public bool CanAccessFileSystem()
        {
            return m_CanAccessFileSystem;
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public void DeleteFile(string path)
        {
            File.Delete(path);
        }

        public Stream OpenFileForWriting(string path)
        {
            // NOTE: FileMode.Create either makes a new file OR blats the existing one.
            // This is the desired behaviour.
            // See https://learn.microsoft.com/en-us/dotnet/api/system.io.filemode
            return new FileStream(path, FileMode.Create);
        }

        public Stream OpenFileForReading(string path)
        {
            // NOTE: FileMode.Open will throw an exception if the file does not exist.
            // So ensure the Exists check is always done before getting near here.
            // See https://learn.microsoft.com/en-us/dotnet/api/system.io.filemode
            return new FileStream(path, FileMode.Open);
        }
    }

    internal class DiskCache : IDiskCache
    {
        internal const string k_FileHeaderString = "UGSEventCache";
        internal const int k_CacheFileVersionOne = 1;
        internal const int k_CacheFileVersionTwo = 2;

        private readonly string k_CacheFilePath;
        private readonly IFileSystemCalls k_SystemCalls;
        private readonly long k_CacheFileMaximumSize;

        internal DiskCache(IFileSystemCalls systemCalls)
        {
            if (systemCalls.CanAccessFileSystem())
            {
                // NOTE: On console platforms where file system access is restricted, even asking for the persistentDataPath
                // can cause an exception, let alone trying to write to it. Be careful!

                // NOTE: Since we now have some defence against trying to read files that don't match the new file format,
                // we are safe to keep reusing the same file path. We will simply ignore and delete/overwrite the cache
                // from older SDK versions.
                k_CacheFilePath = $"{Application.persistentDataPath}/eventcache";
            }

            k_SystemCalls = systemCalls;
            k_CacheFileMaximumSize = 5 * 1024 * 1024; // 5MB, 1024B * 1024KB * 5
        }

        internal DiskCache(string cacheFilePath, IFileSystemCalls systemCalls, long maximumFileSize)
        {
            k_CacheFilePath = cacheFilePath;
            k_SystemCalls = systemCalls;
            k_CacheFileMaximumSize = maximumFileSize;
        }

        public void Write(List<EventSummary> eventSummaries, Stream payload)
        {
            if (eventSummaries.Count > 0 &&
                k_SystemCalls.CanAccessFileSystem())
            {
                // Tick through eventEnds until you find the highest one that is still under the file size limit
                int cacheEnd = 0;
                int cacheEventCount = 0;
                for (int e = 0; e < eventSummaries.Count; e++)
                {
                    if (eventSummaries[e].EndIndex < k_CacheFileMaximumSize)
                    {
                        cacheEnd = eventSummaries[e].EndIndex;
                        cacheEventCount = e + 1;
                    }
                }

                using (Stream file = k_SystemCalls.OpenFileForWriting(k_CacheFilePath))
                {
                    using (var writer = new BinaryWriter(file))
                    {
                        writer.Write(k_FileHeaderString);       // a specific string to signal file format validity
                        writer.Write(k_CacheFileVersionTwo);    // int version specifier
                        writer.Write(cacheEventCount);          // int event count (cropped to maximum file size)
                        for (int i = 0; i < cacheEventCount; i++)
                        {
                            writer.Write(eventSummaries[i].StartIndex);     // int32 event start index
                            writer.Write(eventSummaries[i].EndIndex);       // int32 event end index
                            writer.Write(eventSummaries[i].Id);
                        }

                        long payloadOriginalPosition = payload.Position;
                        payload.Position = 0;
                        for (int i = 0; i < cacheEnd; i++)
                        {
                            // NOTE: the cast to byte is important -- ReadByte actually returns an int, which is 4 bytes.
                            // So you get 3 extra bytes of 0 added if you take it verbatim. Casting to byte cuts it back down to size.
                            writer.Write((byte)payload.ReadByte());   // byte[] event data
                        }
                        payload.Position = payloadOriginalPosition;
                    }
                }
            }
        }

        public void Clear()
        {
            if (k_SystemCalls.CanAccessFileSystem() &&
                k_SystemCalls.FileExists(k_CacheFilePath))
            {
                k_SystemCalls.DeleteFile(k_CacheFilePath);
            }
        }

        public bool Read(List<EventSummary> eventSummaries, Stream buffer)
        {
            if (k_SystemCalls.CanAccessFileSystem() &&
                k_SystemCalls.FileExists(k_CacheFilePath))
            {
#if UNITY_ANALYTICS_EVENT_LOGS
                Debug.Log("Reading cached events: " + k_CacheFilePath);
#endif
                using (Stream file = k_SystemCalls.OpenFileForReading(k_CacheFilePath))
                {
                    using (var reader = new BinaryReader(file))
                    {
                        try
                        {
                            string header = reader.ReadString();
                            if (header == k_FileHeaderString)
                            {
                                int version = reader.ReadInt32();
                                switch (version)
                                {
                                    case k_CacheFileVersionOne:
                                        ReadVersionOneCacheFile(eventSummaries, reader, buffer);
                                        return true;
                                    case k_CacheFileVersionTwo:
                                        ReadVersionTwoCacheFile(eventSummaries, reader, buffer);
                                        return true;
                                    default:
                                        Debug.LogWarning($"Unable to read event cache file: unknown file format version {version}");
                                        Clear();
                                        break;
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"Unable to read event cache file: corrupt");
                                Clear();
                            }
                        }
                        catch (Exception)
                        {
                            Debug.LogWarning($"Unable to read event cache file: corrupt");
                            Clear();
                        }
                    }
                }
            }

            return false;
        }

        private void ReadVersionOneCacheFile(in List<EventSummary> eventEndIndices, BinaryReader reader, in Stream buffer)
        {
            int eventCount = reader.ReadInt32();            // int32 event count
            for (int i = 0; i < eventCount; i++)
            {
                int eventEndIndex = reader.ReadInt32();     // int32 event end index
                // During migration from old cache format, we have to fill in the blanks.
                // Only the indices are important so this should be fine.
                eventEndIndices.Add(new EventSummary
                {
                    StartIndex = i == 0 ? 0 : eventEndIndices[eventEndIndices.Count - 1].EndIndex,
                    EndIndex = eventEndIndex,
                    Id = $"loadedFromOldCache{i}"
                });
            }

            buffer.SetLength(0);
            buffer.Position = 0;
            // V1 cache files include the 14-byte {"eventList":[ header.
            // We need to skip that because the new buffer does not (it adds the header at serialisation time instead).
            reader.ReadBytes(14);
            reader.BaseStream.CopyTo(buffer);               // byte[] event data is the rest of the file
        }

        private void ReadVersionTwoCacheFile(in List<EventSummary> eventSummaries, BinaryReader reader, in Stream buffer)
        {
            int eventCount = reader.ReadInt32();            // int32 event count
            for (int i = 0; i < eventCount; i++)
            {
                int eventStartIndex = reader.ReadInt32();   // int32 event start index
                int eventEndIndex = reader.ReadInt32();     // int32 event end index
                string eventId = reader.ReadString();

                eventSummaries.Add(new EventSummary
                {
                    StartIndex = eventStartIndex,
                    EndIndex = eventEndIndex,
                    Id = eventId
                });
            }

            buffer.SetLength(0);
            buffer.Position = 0;
            reader.BaseStream.CopyTo(buffer);               // byte[] event data is the rest of the file
        }
    }
}
