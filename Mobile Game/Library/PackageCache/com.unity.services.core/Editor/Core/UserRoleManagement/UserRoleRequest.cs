using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.Core.Internal;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.Services.Core.Editor
{
    class UserRoleRequest : IUserRoleRequest
    {
        readonly IAccessTokens m_AccessTokens;
        readonly IServiceUrlBuilder m_ServiceUrlBuilder;
        readonly IUserRoleClient m_UserRoleClient;

        public UserRoleRequest(
            IAccessTokens accessTokens,
            IUserRoleClient userRoleClient)
        {
            m_AccessTokens = accessTokens;
            m_UserRoleClient = userRoleClient;
        }

        public IAsyncOperation<UserRole> GetUserRole()
        {
            var resultAsyncOp = new AsyncOperation<UserRole>();
            resultAsyncOp.SetInProgress();

            var serviceToken = m_AccessTokens.GetServicesGatewayTokenAsync();
            serviceToken.ContinueWith(tokenOperation =>
            {
                ThreadingUtility.RunNextUpdateOnMain(() => OnTokenReceived(tokenOperation, resultAsyncOp));
            });

            return resultAsyncOp;
        }

        void OnTokenReceived(Task<string> tokenOperation, AsyncOperation<UserRole> resultAsyncOp)
        {
            if (string.IsNullOrEmpty(tokenOperation.Result))
            {
                resultAsyncOp.Fail(tokenOperation.Exception);
            }

            QueryProjectUsers(resultAsyncOp, tokenOperation.Result);
        }

        void QueryProjectUsers(
            AsyncOperation<UserRole> resultAsyncOp,
            string serviceToken)
        {
            m_UserRoleClient.QueryProjectUsers(
                resultAsyncOp,
                FindUserRoleToFinishAsyncOperation,
                serviceToken);
        }

        internal static void FindUserRoleToFinishAsyncOperation(User user, AsyncOperation<UserRole> resultAsyncOp)
        {
            try
            {
                if (user == null)
                {
                    throw new CurrentUserNotFoundException();
                }

                var currentUserRole = GetUserRoleFromUser(user);

                resultAsyncOp.Succeed(currentUserRole);
            }
            catch (Exception ex)
            {
                resultAsyncOp.Fail(ex);
            }
        }

        internal static UserRole GetUserRoleFromUser(User user)
        {
            var currentRole = UserRole.Unknown;

            foreach (var role in user.Roles)
            {
                if (!role.IsLegacy)
                {
                    continue;
                }

                var iterationRole = UserRole.Unknown;

                if (string.Equals("user", role.Name, StringComparison.OrdinalIgnoreCase))
                {
                    iterationRole = UserRole.User;
                }
                else if (string.Equals("manager", role.Name, StringComparison.OrdinalIgnoreCase))
                {
                    iterationRole = UserRole.Manager;
                }
                else if (string.Equals("owner", role.Name, StringComparison.OrdinalIgnoreCase))
                {
                    iterationRole = UserRole.Owner;
                }

                currentRole = iterationRole > currentRole ? iterationRole : currentRole;
            }

            return currentRole;
        }

        [Serializable]
        internal class User
        {
            [JsonProperty("id")]
            public string Id;
            [JsonProperty("genesisId")]
            public string GenesisId;
            [JsonProperty("email")]
            public string Email;
            [JsonProperty("name")]
            public string Name;
            [JsonProperty("roles")]
            public Role[] Roles;
        }

        [Serializable]
        internal class Role
        {
            [JsonProperty("id")]
            public string Id { get; set; }
            [JsonProperty("name")]
            public string Name { get; set; }
            [JsonProperty("entityType")]
            public string EntityType { get; set; }
            [JsonProperty("isLegacy")]
            public bool IsLegacy { get; set; }
            [JsonProperty("policyId")]
            public string PolicyId { get; set; }
            [JsonProperty("legacyRoleType")]
            public string LegacyRoleType { get; set; }
        }

        internal class RequestNotAuthorizedException : Exception {}

        internal class CurrentUserNotFoundException : Exception {}
    }
}
