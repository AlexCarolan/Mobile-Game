using System;
using System.Collections.Generic;
using Unity.Services.Analytics.Data;
using Unity.Services.Analytics.Internal;
using Unity.Services.Authentication.Internal;
using Unity.Services.Core;
using Unity.Services.Core.Analytics.Internal;
using Unity.Services.Core.Configuration.Internal;
using Unity.Services.Core.Device.Internal;
using Unity.Services.Core.Environments.Internal;
using Unity.Services.Core.Internal;
using UnityEngine;

namespace Unity.Services.Analytics
{
    public static class AnalyticsService
    {
        const string k_CollectUrlPattern = "https://collect.analytics.unity3d.com/api/analytics/collect/v2/projects/{0}/environments/{1}";

        static AnalyticsServiceInstance m_Instance;
        static IDispatcherDebug m_DispatcherDebug;
        static IBufferDebug m_BufferDebug;
        static Action<string, string, DateTime, byte[]> m_EventRecordedCallback;
        static Action<HashSet<string>> m_EventsClearingCallback;
        static Action<byte[]> m_FlushStartedCallback;
        static Action<int, bool, bool, bool, bool, byte[]> m_FlushCompletedCallback;

        internal static bool IsInitialized
        {
            get { return m_Instance != null; }
        }

        internal static IServiceDebug ServiceDebug
        {
            get { return m_Instance; }
        }

        internal static IDispatcherDebug DispatcherDebug
        {
            get { return m_DispatcherDebug; }
        }

        internal static void Initialize(CoreRegistry registry)
        {
            var cloudProjectId = registry.GetServiceComponent<ICloudProjectId>();
            var installationId = registry.GetServiceComponent<IInstallationId>();
            var playerId = registry.GetServiceComponent<IPlayerId>();
            var environments = registry.GetServiceComponent<IEnvironments>();
            var customUserId = registry.GetServiceComponent<IExternalUserId>();

            var coreStatsHelper = new CoreStatsHelper();
            var persistence = new PlayerPrefsPersistence();
            var userIdentity = new IdentityManager(installationId, playerId, customUserId, persistence);
            var session = new SessionManager();

            string projectId = cloudProjectId?.GetCloudProjectId() ?? Application.cloudProjectId;
            string collectUrl = String.Format(k_CollectUrlPattern, projectId, environments.Current.ToLowerInvariant());

            var buffer = new BufferX(new BufferSystemCalls(), new DiskCache(new FileSystemCalls()), userIdentity, session);

            var containerObject = AnalyticsContainer.CreateContainer();
            var webRequestHelper = new WebRequestHelper();
            var dispatcher = new Dispatcher(webRequestHelper, collectUrl);

            m_Instance = new AnalyticsServiceInstance(
                new DataGenerator(buffer, new CommonDataWrapper(projectId), new DeviceDataWrapper()),
                buffer,
                coreStatsHelper,
                dispatcher,
                new AnalyticsForgetter(collectUrl, persistence, webRequestHelper),
                userIdentity,
                environments.Current,
                new AnalyticsServiceSystemCalls(),
                containerObject,
                session);
            containerObject.Initialize(m_Instance);
            m_Instance.ResumeDataDeletionIfNecessary();

            m_DispatcherDebug = dispatcher;
            m_BufferDebug = buffer;
            if (m_EventRecordedCallback != null)
            {
                m_BufferDebug.EventRecorded += m_EventRecordedCallback;
                m_BufferDebug.EventsClearing += m_EventsClearingCallback;
                m_DispatcherDebug.FlushStarted += m_FlushStartedCallback;
                m_DispatcherDebug.FlushFinished += m_FlushCompletedCallback;
            }

            StandardEventServiceComponent standardEventComponent = new StandardEventServiceComponent(
                registry.GetServiceComponent<IProjectConfiguration>(),
                m_Instance);
            registry.RegisterServiceComponent<IAnalyticsStandardEventComponent>(standardEventComponent);

            AnalyticsUserIdServiceComponent userIdComponent = new AnalyticsUserIdServiceComponent(m_Instance);
            registry.RegisterServiceComponent<IAnalyticsUserId>(userIdComponent);

#if UNITY_ANALYTICS_DEVELOPMENT
            Debug.LogFormat("Core Initialize Callback\nCollect URL: {0}\nInstall ID: {1}\nPlayer ID: {2}\nCustom Analytics ID: {3}",
                collectUrl,
                installationId.GetOrCreateIdentifier(),
                playerId?.PlayerId,
                customUserId.UserId
            );
#endif
        }

        internal static void SubscribeDebugEvents(
            Action<string, string, DateTime, byte[]> eventRecordedCallback,
            Action<HashSet<string>> eventsUploadingCallback,
            Action<byte[]> flushStarted,
            Action<int, bool, bool, bool, bool, byte[]> flushCompleted)
        {
            m_EventRecordedCallback = eventRecordedCallback;
            m_EventsClearingCallback = eventsUploadingCallback;
            m_FlushStartedCallback = flushStarted;
            m_FlushCompletedCallback = flushCompleted;
            if (m_BufferDebug != null)
            {
                m_BufferDebug.EventRecorded += m_EventRecordedCallback;
                m_BufferDebug.EventsClearing += m_EventsClearingCallback;
                m_DispatcherDebug.FlushStarted += m_FlushStartedCallback;
                m_DispatcherDebug.FlushFinished += m_FlushCompletedCallback;
            }
        }

        internal static void UnsubscribeDebugEvents()
        {
            if (m_BufferDebug != null)
            {
                m_BufferDebug.EventRecorded -= m_EventRecordedCallback;
                m_BufferDebug.EventsClearing -= m_EventsClearingCallback;
                m_DispatcherDebug.FlushStarted -= m_FlushStartedCallback;
                m_DispatcherDebug.FlushFinished -= m_FlushCompletedCallback;
            }
            m_EventRecordedCallback = null;
            m_EventsClearingCallback = null;
        }

        internal static void TearDown()
        {
            if (m_BufferDebug != null &&
                m_EventRecordedCallback != null)
            {
                m_BufferDebug.EventRecorded -= m_EventRecordedCallback;
                m_BufferDebug.EventsClearing -= m_EventsClearingCallback;
                m_DispatcherDebug.FlushStarted -= m_FlushStartedCallback;
                m_DispatcherDebug.FlushFinished -= m_FlushCompletedCallback;
            }

            m_Instance = null;
            m_DispatcherDebug = null;
            m_BufferDebug = null;
        }

        public static IAnalyticsService Instance
        {
            get
            {
                if (m_Instance == null)
                {
                    throw new ServicesInitializationException("The Analytics service has not been initialized. Please initialize Unity Services.");
                }

                return m_Instance;
            }
        }
    }
}
