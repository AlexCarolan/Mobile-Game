namespace Unity.Services.Core.Editor
{
    interface IServiceUrlBuilder
    {
        string GetRequestUri(UriPathName uriPathName);
    }

    enum UriPathName
    {
        GetCurrentUserRoleInProject,
    }
}
