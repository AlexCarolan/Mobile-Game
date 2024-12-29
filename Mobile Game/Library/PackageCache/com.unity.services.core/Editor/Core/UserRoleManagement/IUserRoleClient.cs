using System;
using Unity.Services.Core.Internal;

namespace Unity.Services.Core.Editor
{
    interface IUserRoleClient
    {
        void QueryProjectUsers(
            AsyncOperation<UserRole> resultAsyncOp,
            Action<UserRoleRequest.User, AsyncOperation<UserRole>> onComplete,
            string serviceToken);
    }
}
