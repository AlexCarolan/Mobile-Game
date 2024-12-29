using System;
using System.Collections.Generic;
using Unity.Services.Analytics.Data;
using Unity.Services.Analytics.Internal;
using Unity.Services.Analytics.Platform;
using UnityEngine;

namespace Unity.Services.Analytics
{
    internal interface IAnalyticsServiceSystemCalls
    {
        DateTime UtcNow { get; }
    }

    internal class AnalyticsServiceSystemCalls : IAnalyticsServiceSystemCalls
    {
        public DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }
    }

    internal interface IUnstructuredEventRecorder
    {
        void CustomData(string eventName,
            IDictionary<string, object> eventParams,
            int? eventVersion,
            bool isStandardEvent,
            string callingMethodIdentifier);
    }

    partial class AnalyticsServiceInstance : IAnalyticsService, IUnstructuredEventRecorder, IServiceDebug
    {
        public string PrivacyUrl => "https://unity.com/legal/game-player-and-app-user-privacy-policy";

        const string k_ForgetCallingId = "com.unity.services.analytics.Events." + nameof(RequestDataDeletion);
        const string k_StartUpCallingId = "com.unity.services.analytics.Events.Startup";
        internal const string k_InvokedByUserCallingId = "com.unity.services.analytics.Events.UserInvoked";

        readonly TimeSpan k_BackgroundSessionRefreshPeriod = TimeSpan.FromMinutes(5);
        readonly TransactionCurrencyConverter converter = new TransactionCurrencyConverter();

        readonly IIdentityManager m_UserIdentity;
        readonly ISessionManager m_Session;
        readonly IDataGenerator m_DataGenerator;
        readonly ICoreStatsHelper m_CoreStatsHelper;
        readonly IDispatcher m_DataDispatcher;
        readonly IAnalyticsForgetter m_AnalyticsForgetter;
        readonly IAnalyticsServiceSystemCalls m_SystemCalls;
        readonly IAnalyticsContainer m_Container;

        internal IBuffer m_DataBuffer;

        public string SessionID { get { return m_Session.SessionId; } }

        int m_BufferLengthAtLastGameRunning;
        DateTime m_ApplicationPauseTime;

        bool m_IsActive;

        /// <summary>
        /// This is for internal unit test usage only.
        /// In the real world, use Activate() and Deactivate().
        /// </summary>
        internal bool Active
        {
            get { return m_IsActive; }
            set { m_IsActive = value; }
        }

        public bool IsActive { get { return m_IsActive; } }

        public string GetAnalyticsUserID()
        {
            return m_UserIdentity.UserId;
        }

        public IIdentityManager UserIdentity
        {
            get { return m_UserIdentity; }
        }

        internal AnalyticsServiceInstance(IDataGenerator dataGenerator,
                                          IBuffer realBuffer,
                                          ICoreStatsHelper coreStatsHelper,
                                          IDispatcher dispatcher,
                                          IAnalyticsForgetter forgetter,
                                          IIdentityManager userIdentity,
                                          string environment,
                                          IAnalyticsServiceSystemCalls systemCalls,
                                          IAnalyticsContainer container,
                                          ISessionManager session)
        {
            m_DataGenerator = dataGenerator;
            m_SystemCalls = systemCalls;

            m_CoreStatsHelper = coreStatsHelper;
            m_DataDispatcher = dispatcher;
            m_Container = container;

            m_DataBuffer = realBuffer;
            m_DataDispatcher.SetBuffer(realBuffer);

            m_IsActive = false;

            m_AnalyticsForgetter = forgetter;

            m_UserIdentity = userIdentity;
            m_Session = session;
        }

        internal void ResumeDataDeletionIfNecessary()
        {
            if (m_AnalyticsForgetter.DeletionInProgress)
            {
                DeactivateWithDataDeletionRequest();
            }
        }

        public void StartDataCollection()
        {
            // The New flow allows "opt out and back in again", so this method can be activated
            // repeatedly within a single session. It should do nothing if the SDK is already
            // active, but otherwise (re)activate the SDK as normal.
            if (!m_IsActive)
            {
                // In case you had previously requested data deletion, you must now be able to request it again.
                m_AnalyticsForgetter.ResetDataDeletionStatus();
                m_CoreStatsHelper.SetCoreStatsConsent(true);

                Activate();
            }
        }

        void Activate()
        {
            if (!m_IsActive)
            {
                m_IsActive = true;
                m_Container.Enable();
                m_DataBuffer.LoadFromDisk();

                RecordStartupEvents();

                Flush();
            }
        }

        public void StopDataCollection()
        {
            if (m_IsActive)
            {
                m_DataDispatcher.Flush();
                Deactivate();
            }
        }

        internal void DeactivateWithDataDeletionRequest()
        {
            m_DataBuffer.ClearBuffer();
            m_DataBuffer.ClearDiskCache();
            m_Container.Enable();
            m_AnalyticsForgetter.AttemptToForget(m_UserIdentity.UserId, m_UserIdentity.InstallId, m_UserIdentity.PlayerId, BufferX.SerializeDateTime(DateTime.Now), k_ForgetCallingId, DataDeletionCompleted);

            Deactivate();
        }

        void DataDeletionCompleted()
        {
            if (!m_IsActive)
            {
                m_Container.Disable();
            }
        }

        void Deactivate()
        {
            if (m_IsActive)
            {
                m_IsActive = false;

                if (!m_AnalyticsForgetter.DeletionInProgress)
                {
                    // Only disable the container if opting out is not in progress. Otherwise, leave it
                    // running so that the heartbeat can re-attempt the deletion request upload until
                    // it succeeds.
                    m_Container.Disable();
                }
            }

            m_CoreStatsHelper.SetCoreStatsConsent(false);
        }

        bool m_StartUpEventsRecorded = false;
        void RecordStartupEvents()
        {
            if (!m_StartUpEventsRecorded)
            {
                // Only record start-up events once in a session, even if the player opts in/out/in again.
                m_StartUpEventsRecorded = true;

                // Startup Events.
                m_DataGenerator.SdkStartup(k_StartUpCallingId);
                m_DataGenerator.ClientDevice(k_StartUpCallingId);

#if UNITY_DOTSRUNTIME
                var isTiny = true;
#else
                var isTiny = false;
#endif

                m_DataGenerator.GameStarted(k_StartUpCallingId, Application.buildGUID, SystemInfo.operatingSystem, isTiny, DebugDevice.IsDebugDevice(), Locale.AnalyticsRegionLanguageCode());

                if (m_UserIdentity.IsNewPlayer)
                {
                    m_DataGenerator.NewPlayer(k_StartUpCallingId, SystemInfo.deviceModel);
                }
            }
        }

        internal void ApplicationPaused(bool paused)
        {
            if (paused)
            {
                m_ApplicationPauseTime = m_SystemCalls.UtcNow;
#if UNITY_ANALYTICS_DEVELOPMENT
                Debug.Log("Analytics SDK detected application pause at: " + m_ApplicationPauseTime.ToString());
#endif
            }
            else
            {
                DateTime now = m_SystemCalls.UtcNow;

#if UNITY_ANALYTICS_DEVELOPMENT
                Debug.Log("Analytics SDK detected application unpause at: " + now);
#endif
                if (now > m_ApplicationPauseTime + k_BackgroundSessionRefreshPeriod)
                {
                    m_Session.StartNewSession();
                }
            }
        }

        internal int AutoflushPeriodMultiplier
        {
            get { return Mathf.Clamp(1 + m_DataDispatcher.ConsecutiveFailedUploadCount, 1, 8); }
        }

        public void Flush()
        {
            if (m_IsActive)
            {
                // No need for any further conditional guards, m_IsActive is only true if we are clear to flush.
                m_DataDispatcher.Flush();
            }
            else if (m_AnalyticsForgetter.DeletionInProgress)
            {
                DeactivateWithDataDeletionRequest();
            }
        }

        public void RequestDataDeletion()
        {
            DeactivateWithDataDeletionRequest();
        }

        internal void ApplicationQuit()
        {
            if (m_IsActive)
            {
                m_DataGenerator.GameEnded("com.unity.services.analytics.Events.Shutdown", DataGenerator.SessionEndState.QUIT);

                // Flush to disk before attempting final upload, in case we do not have enough time during teardown
                // to make the request and/or determine its success (e.g. if we shut down offline)
                m_DataBuffer.FlushToDisk();

                Flush();
            }

            // If we are quitting merely as part of returning to Edit Mode in the editor,
            // we still need to clear up the static accessor(s).
            AnalyticsService.TearDown();
        }

        internal void RecordGameRunningIfNecessary()
        {
            if (m_IsActive)
            {
                if (m_DataBuffer.Length == 0 || m_DataBuffer.Length == m_BufferLengthAtLastGameRunning)
                {
                    m_DataGenerator.GameRunning("com.unity.services.analytics.AnalyticsServiceInstance.RecordGameRunningIfNecessary");
                    m_BufferLengthAtLastGameRunning = m_DataBuffer.Length;
                }
                else
                {
                    m_BufferLengthAtLastGameRunning = m_DataBuffer.Length;
                }
            }
        }

        public long ConvertCurrencyToMinorUnits(string currencyCode, double value)
        {
            return converter.Convert(currencyCode, value);
        }

        public void CustomData(string eventName)
        {
            CustomData(eventName, null);
        }

        public void CustomData(string eventName, IDictionary<string, object> eventParams)
        {
            CustomData(eventName, eventParams, null, false, "AnalyticsServiceInstance.CustomData");
        }

        public void CustomData(string eventName,
            IDictionary<string, object> eventParams,
            int? eventVersion,
            bool isStandardEvent,
            string callingMethodIdentifier)
        {
            if (m_IsActive)
            {
                if (isStandardEvent)
                {
                    m_DataBuffer.PushStandardEventStart(eventName, eventVersion.Value);
                    m_DataGenerator.PushCommonParams(callingMethodIdentifier);
                }
                else
                {
                    m_DataBuffer.PushCustomEventStart(eventName);
                }

                if (eventParams != null)
                {
                    foreach (KeyValuePair<string, object> paramPair in eventParams)
                    {
                        m_DataBuffer.PushObject(paramPair.Key, paramPair.Value);
                    }
                }

                m_DataBuffer.PushEndEvent();
            }
#if UNITY_ANALYTICS_EVENT_LOGS
            else
            {
                Debug.Log("Did not record custom event " + eventName + " because player has not opted in.");
            }
#endif
        }

        public void RecordEvent(string name)
        {
            if (m_IsActive)
            {
                m_DataGenerator.PushEmptyEvent(name);
            }
#if UNITY_ANALYTICS_EVENT_LOGS
            else
            {
                Debug.Log("Did not record event " + name + " because player has not opted in.");
            }
#endif
        }

        public void RecordEvent(Event e)
        {
            RecordEvent(e, k_InvokedByUserCallingId);
        }

        internal void RecordEvent(Event e, string callingMethodIdentifier)
        {
            if (m_IsActive)
            {
                m_DataGenerator.PushEvent(callingMethodIdentifier, e);
            }
#if UNITY_ANALYTICS_EVENT_LOGS
            else
            {
                Debug.Log("Did not record custom event " + e.Name + " because player has not opted in.");
            }
#endif
        }
    }
}
