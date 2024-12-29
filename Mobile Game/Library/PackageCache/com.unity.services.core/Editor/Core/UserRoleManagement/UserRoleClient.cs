using System;
using Unity.Services.Core.Internal;
using Unity.Services.Core.Internal.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.Services.Core.Editor
{
    class UserRoleClient : IUserRoleClient
    {
        readonly IServiceUrlBuilder m_ServiceUrlBuilder;
        readonly IJsonSerializer m_Serializer;

        public UserRoleClient(IServiceUrlBuilder serviceUrlBuilder, IJsonSerializer serializer)
        {
            m_ServiceUrlBuilder = serviceUrlBuilder;
            m_Serializer = serializer;
        }

        public void QueryProjectUsers(
            AsyncOperation<UserRole> resultAsyncOp,
            Action<UserRoleRequest.User, AsyncOperation<UserRole>> onComplete,
            string serviceToken)
        {
            try
            {
                var usersUrl =
                    m_ServiceUrlBuilder.GetRequestUri(UriPathName.GetCurrentUserRoleInProject);
                usersUrl = string.Format(usersUrl, CloudProjectSettings.projectId);
                var getProjectUsersRequest = new UnityWebRequest(
                    usersUrl,
                    UnityWebRequest.kHttpVerbGET)
                {
                    downloadHandler = new DownloadHandlerBuffer()
                };
                getProjectUsersRequest.SetRequestHeader("AUTHORIZATION", $"Bearer {serviceToken}");
                var operation = getProjectUsersRequest.SendWebRequest();
                operation.completed += _ => OnRequestCompleted(getProjectUsersRequest, resultAsyncOp, onComplete);
            }
            catch (Exception ex)
            {
                resultAsyncOp.Fail(ex);
                Debug.LogException(ex);
            }
        }

        void OnRequestCompleted(UnityWebRequest webRequest, AsyncOperation<UserRole> operation, Action<UserRoleRequest.User, AsyncOperation<UserRole>> onComplete)
        {
            const int requestNotAuthorizedCode = 401;
            if (webRequest.responseCode == requestNotAuthorizedCode)
            {
                var exception = new UserRoleRequest.RequestNotAuthorizedException();
                operation.Fail(exception);
                throw exception;
            }

            var user = ExtractUserFromUnityWebRequest(webRequest);
            onComplete?.Invoke(user, operation);
        }

        UserRoleRequest.User ExtractUserFromUnityWebRequest(UnityWebRequest unityWebRequest)
        {
            if (!UnityWebRequestHelper.IsUnityWebRequestReadyForTextExtract(unityWebRequest, out var jsonContent))
            {
                return null;
            }

            m_Serializer.TryJsonDeserialize<UserRoleRequest.User>(jsonContent, out var userList);
            return userList;
        }
    }
}
