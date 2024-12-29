using System;
using Unity.Services.Core.Internal;
using UnityEngine;

namespace Unity.Services.Core.Components
{
    /// <summary>
    /// Base behaviour to manage services.
    /// Provides two methods to override:
    /// - OnRegistryReady when the services registry has been set
    /// - OnServicesReady when the services have been initialized
    /// </summary>
    public abstract class ServicesBehaviour : MonoBehaviour
    {
        /// <summary>
        /// Access to the services registry.
        /// The registry is set in Start.
        /// </summary>
        public IUnityServices Services { get; internal set; }

        /// <summary>
        /// Use this to setup a custom services registry. All services in a registry are unique.
        /// </summary>
        [Header("Services Registry")]
        [Tooltip("Use this to setup a custom services registry. All services in a registry are unique.")]
        [SerializeField]
        public bool UseCustomServices;

        /// <summary>
        /// Unique local identifier for the custom set of services. Used as the key in the registries dictionary.
        /// </summary>
        [SerializeField]
        [Tooltip("Unique local identifier for the custom set of services. Used as the key in the registries dictionary.")]
        [Visibility(nameof(UseCustomServices), true)]
        public string ServicesIdentifier;

        internal virtual void Start()
        {
            SetRegistry();

            if (Services != null)
            {
                if (Services.State == ServicesInitializationState.Initialized)
                {
                    OnServicesInitialized();
                }
                else
                {
                    Services.Initialized -= OnServicesInitialized;
                    Services.Initialized += OnServicesInitialized;
                }
            }
        }

        internal virtual void OnDestroy()
        {
            if (Services != null)
            {
                Services.Initialized -= OnServicesInitialized;
            }

            Cleanup();
        }

        void SetRegistry()
        {
            Services = UseCustomServices
                ? UnityServices.Services.ContainsKey(ServicesIdentifier)
                ? UnityServices.Services[ServicesIdentifier]
                : UnityServices.CreateServices(ServicesIdentifier)
                : UnityServices.Instance;

            OnServicesReady();
        }

        /// <summary>
        /// Called when the services registry is set and ready to be used
        /// </summary>
        protected abstract void OnServicesReady();

        /// <summary>
        /// Called when the services are initialized and ready to be used
        /// </summary>
        protected abstract void OnServicesInitialized();

        /// <summary>
        /// Called on destroy to cleanup
        /// </summary>
        protected abstract void Cleanup();
    }
}
