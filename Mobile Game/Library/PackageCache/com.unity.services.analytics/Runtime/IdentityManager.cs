using System;
using Unity.Services.Authentication.Internal;
using Unity.Services.Core.Configuration.Internal;
using Unity.Services.Core.Device.Internal;

namespace Unity.Services.Analytics.Internal
{
    internal interface IIdentityManager
    {
        string UserId { get; }
        string InstallId { get; }
        string PlayerId { get; }
        string ExternalId { get; }

        bool IsNewPlayer { get; }
    }

    internal class IdentityManager : IIdentityManager
    {
        internal const string k_UnityAnalyticsInstallationIdKey = "UnityAnalyticsInstallationId";

        readonly IPlayerId m_PlayerId;
        readonly IExternalUserId m_ExternalId;

        public string UserId
        {
            get
            {
                // NOTE: we cannot cache this value because it may change at runtime
                string customId = m_ExternalId.UserId;
                return !String.IsNullOrEmpty(customId) ? customId : InstallId;
            }
        }
        public string InstallId { get; private set; }
        public string PlayerId { get { return m_PlayerId?.PlayerId; } }
        public string ExternalId { get { return m_ExternalId?.UserId; } }

        public bool IsNewPlayer { get; private set; }

        public IdentityManager(IInstallationId installId, IPlayerId playerId, IExternalUserId externalId, IPersistence persistence)
        {
            InstallId = installId.GetOrCreateIdentifier();
            m_PlayerId = playerId;
            m_ExternalId = externalId;

            string analyticsIdentifier = persistence.LoadString(k_UnityAnalyticsInstallationIdKey);

            if (String.IsNullOrEmpty(analyticsIdentifier) || analyticsIdentifier != InstallId)
            {
                persistence.SaveValue(k_UnityAnalyticsInstallationIdKey, InstallId);
                IsNewPlayer = true;
            }
            else
            {
                IsNewPlayer = false;
            }
        }
    }
}
