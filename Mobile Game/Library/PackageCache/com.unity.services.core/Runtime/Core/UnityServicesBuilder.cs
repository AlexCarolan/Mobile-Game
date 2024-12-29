namespace Unity.Services.Core
{
    static class UnityServicesBuilder
    {
        internal delegate IUnityServices CreationDelegate(string servicesId);
        internal static CreationDelegate InstanceCreationDelegate { get; set; }

        public static IUnityServices Create(string servicesId)
        {
            if (InstanceCreationDelegate == null)
            {
                throw new ServicesCreationException($"Error creating services. The creation delegate has not been initialized.");
            }

            return InstanceCreationDelegate(servicesId);
        }
    }
}
