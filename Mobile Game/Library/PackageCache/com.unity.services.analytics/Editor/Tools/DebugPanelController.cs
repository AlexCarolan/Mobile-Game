using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Services.Analytics.Internal;

namespace Unity.Services.Analytics.Editor
{
    internal enum DebugState
    {
        Unknown,
        EditMode,
        SdkUninitialized,
        SdkInactive,
        SdkActive,
        SdkUploading
    }

    internal enum EventStreamItemStatus
    {
        Queued,
        Uploading,
        Uploaded,
        Discarded
    }

    internal class EventStreamItem
    {
        internal string Id;
        internal string Name;
        internal DateTime Timestamp;
        internal byte[] Payload;

        internal EventStreamItemStatus Status;

        public string TimestampString
        {
            get { return $"{Timestamp:HH:mm:ss}"; }
        }
    }

    internal class DebugPanelController
    {
        readonly IDebugPanelWindow m_Window;
        readonly List<EventStreamItem> m_EventStream;

        DebugState m_SdkState;
        HashSet<string> m_CurrentUploadEventIds;

        public IList EventStream { get { return m_EventStream; } }

        internal DebugPanelController(IDebugPanelWindow window)
        {
            m_Window = window;
            m_EventStream = new List<EventStreamItem>();
        }

        internal void Initialize()
        {
            ChangeState(DebugState.EditMode);
        }

        internal void AddEventToStream(string id, string name, DateTime timestamp, byte[] payload)
        {
            m_EventStream.Add(new EventStreamItem
            {
                Id = id,
                Name = name,
                Timestamp = timestamp,
                Payload = payload
            });
            m_Window.RefreshEventStreamDisplay();
        }

        internal void MarkEventsBatchAsUploading(HashSet<string> eventIds)
        {
            m_CurrentUploadEventIds = eventIds;
            SetCurrentEventBatchStatus(EventStreamItemStatus.Uploading);
        }

        internal void MarkCurrentEventsBatchAsUploadingFailed()
        {
            SetCurrentEventBatchStatus(EventStreamItemStatus.Queued);
            m_CurrentUploadEventIds = null;
        }

        internal void MarkCurrentEventsBatchAsDiscarded()
        {
            SetCurrentEventBatchStatus(EventStreamItemStatus.Discarded);
            m_CurrentUploadEventIds = null;
        }

        internal void MarkCurrentEventsBatchAsUploaded()
        {
            SetCurrentEventBatchStatus(EventStreamItemStatus.Uploaded);
            m_CurrentUploadEventIds = null;
        }

        private void SetCurrentEventBatchStatus(EventStreamItemStatus status)
        {
            if (m_CurrentUploadEventIds != null)
            {
                for (int i = 0; i < m_EventStream.Count; i++)
                {
                    EventStreamItem item = m_EventStream[i];
                    if (m_CurrentUploadEventIds.Contains(item.Id))
                    {
                        item.Status = status;
                    }
                }
                m_Window.RefreshEventStreamDisplay();
            }
        }

        internal void ClearStream()
        {
            m_EventStream.Clear();
            m_Window.RefreshEventStreamDisplay();
        }

        internal bool Update(
            bool isPlaying,
            bool isInitialized,
            IServiceDebug serviceDebug,
            IDispatcherDebug dispatcherDebug,
            IContainerDebug containerDebug)
        {
            bool needsRepaint = false;

            if (isPlaying && isInitialized)
            {
                if (dispatcherDebug.FlushInProgress)
                {
                    needsRepaint |= ChangeState(DebugState.SdkUploading);
                }
                else if (serviceDebug.IsActive)
                {
                    ChangeState(DebugState.SdkActive);
                    m_Window.SetNextUpload(containerDebug.TimeUntilNextHeartbeat);
                    // We have definitely changed the heartbeat time so we definitely need a repaint to update that.
                    needsRepaint = true;
                }
                else
                {
                    needsRepaint |= ChangeState(DebugState.SdkInactive);
                }
            }
            else if (isPlaying)
            {
                needsRepaint |= ChangeState(DebugState.SdkUninitialized);
            }
            else
            {
                needsRepaint |= ChangeState(DebugState.EditMode);
            }

            return needsRepaint;
        }

        bool ChangeState(DebugState newState)
        {
            if (newState != m_SdkState)
            {
                switch (newState)
                {
                    case DebugState.SdkUploading:
                        m_Window.SetStatusIndicator(DebugState.SdkUploading);
                        break;
                    case DebugState.SdkActive:
                        m_Window.SetStatusIndicator(DebugState.SdkActive);
                        break;
                    case DebugState.SdkInactive:
                        ClearStream();
                        m_Window.RefreshEventStreamDisplay();
                        m_Window.ClearNextUpload();
                        m_Window.SetStatusIndicator(DebugState.SdkInactive);
                        break;
                    case DebugState.EditMode:
                        ClearStream();
                        m_Window.ClearUploadFields();
                        m_Window.SetStatusIndicator(DebugState.EditMode);
                        break;
                    case DebugState.Unknown:
                    default:
                        m_Window.ClearUploadFields();
                        m_Window.SetStatusIndicator(DebugState.SdkUninitialized);
                        break;
                }

                m_SdkState = newState;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
