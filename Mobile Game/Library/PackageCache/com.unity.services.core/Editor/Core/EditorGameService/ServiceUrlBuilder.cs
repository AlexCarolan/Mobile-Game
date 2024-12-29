using System.Collections.Generic;

namespace Unity.Services.Core.Editor
{
    class ServiceUrlBuilder : IServiceUrlBuilder
    {
        ICloudEnvironmentConfigurationProvider m_ConfigurationProvider;
        public ServiceUrlBuilder(ICloudEnvironmentConfigurationProvider configurationProvider)
        {
            m_ConfigurationProvider = configurationProvider;
        }

        public string GetRequestUri(UriPathName endpoint)
        {
            return m_ConfigurationProvider.IsStaging() ?
                "https://staging.services.unity.com/api/unity/legacy/v1" + k_UriPathParameters[endpoint] :
                "https://services.unity.com/api/unity/legacy/v1" + k_UriPathParameters[endpoint];
        }

        static readonly Dictionary<UriPathName, string> k_UriPathParameters = new Dictionary<UriPathName, string>
        {
            {UriPathName.GetCurrentUserRoleInProject, "/projects/{0}/users/me"},
        };
    }
}
