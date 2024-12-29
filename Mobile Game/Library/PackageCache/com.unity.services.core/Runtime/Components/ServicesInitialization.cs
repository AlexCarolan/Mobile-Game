using System;
using System.Threading.Tasks;
using Unity.Services.Core.Environments;
using Unity.Services.Core.Internal;
using UnityEngine;

namespace Unity.Services.Core.Components
{
    /// <summary>
    /// Manage access and initialization to a set of services
    /// </summary>
    [AddComponentMenu("Services/Services Initialization")]
    public class ServicesInitialization : ServicesBehaviour
    {
        /// <summary>
        /// Option to try to initialize the services in Start().
        /// </summary>
        [Header("Automation")]
        [Tooltip("This will attempt to initialize the services in Start().")]
        [SerializeField]
        public bool InitializeOnStart;

        /// <summary>
        /// Use this to set a custom environment in the initialization options. Defaults to the environment defined in the project settings or production.
        /// </summary>
        [SerializeField]
        [Tooltip("Use this to set a custom environment in the initialization options. Defaults to the environment defined in the project settings or production.")]
        [Visibility(nameof(InitializeOnStart), true)]
        public bool UseCustomEnvironment;

        /// <summary>
        /// Choose the environment name to pass in the initialization options. You can configure environments in the unity dashboard.
        /// </summary>
        [SerializeField]
        [Tooltip("Choose the environment name to pass in the initialization options. You can configure environments in the unity dashboard.")]
        [Visibility(nameof(UseCustomEnvironment), true)]
        public string EnvironmentName = "production";

        /// <summary>
        /// Offers <see cref="IUnityServices"/> events as unity events.
        /// </summary>
        [Header("Events")]
        [SerializeField]
        public ServicesInitializationEvents Events = new ServicesInitializationEvents();

        internal bool IsSetupDone { get; private set; }

        internal ServicesInitialization()
        {
        }

        /// <summary>
        /// Called when the services registry is set and ready to be used
        /// </summary>
        protected override async void OnServicesReady()
        {
            await SetupAsync();
        }

        /// <summary>
        /// Called when the services are initialized and ready to be used
        /// </summary>
        protected override void OnServicesInitialized()
        {
        }

        /// <summary>
        /// Called on destroy to cleanup
        /// </summary>
        protected override void Cleanup()
        {
            if (Services != null)
            {
                Services.Initialized -= OnInitialized;
                Services.InitializeFailed -= OnInitializeFailed;
            }
        }

        internal async Task SetupAsync()
        {
            if (Services.State != ServicesInitializationState.Initialized)
            {
                Services.Initialized -= OnInitialized;
                Services.Initialized += OnInitialized;
                Services.InitializeFailed -= OnInitializeFailed;
                Services.InitializeFailed += OnInitializeFailed;
            }

            if (Services.State == ServicesInitializationState.Uninitialized && InitializeOnStart)
            {
                await InitializeOnStartAsync();
            }

            IsSetupDone = true;
        }

        internal async Task InitializeOnStartAsync()
        {
            if (Services == null)
            {
                Events?.InitializeFailed?.Invoke(new Exception("Trying to initiliaze services before the registry is set."));
                return;
            }

            try
            {
                await Services.InitializeAsync(BuildInitializationOptions());
            }
            catch (Exception)
            {
            }
        }

        internal InitializationOptions BuildInitializationOptions()
        {
            var options = new InitializationOptions();

            if (UseCustomEnvironment)
            {
                options.SetEnvironmentName(EnvironmentName);
            }

            return options;
        }

        void OnInitialized()
        {
            Events?.Initialized?.Invoke();
        }

        void OnInitializeFailed(Exception e)
        {
            Events?.InitializeFailed?.Invoke(e);
        }
    }
}
