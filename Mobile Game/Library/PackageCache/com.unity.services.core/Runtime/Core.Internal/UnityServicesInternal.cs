using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NotNull = JetBrains.Annotations.NotNullAttribute;

namespace Unity.Services.Core.Internal
{
    /// <summary>
    /// Utility to initialize all Unity services from a single endpoint.
    /// </summary>
    class UnityServicesInternal : IUnityServices
    {
        internal const string InitSuccessEventInvocationError = "Exception in services initialization success event handler: ";
        internal const string InitFailureEventInvocationError = "Exception in services initialization failure event handler: ";

        public event Action Initialized;
        public event Action<Exception> InitializeFailed;
        public ServicesInitializationState State { get; private set; }

        public InitializationOptions Options
        {
            get => Registry.Options;
            internal set => Registry.Options = value;
        }

        internal bool CanInitialize;

        TaskCompletionSource<object> m_Initialization;

        [NotNull]
        internal CoreRegistry Registry { get; }
        [NotNull]
        internal CoreMetrics Metrics { get; }
        [NotNull]
        internal CoreDiagnostics Diagnostics { get; }

        public UnityServicesInternal(
            [NotNull] CoreRegistry registry,
            [NotNull] CoreMetrics coreMetrics,
            [NotNull] CoreDiagnostics coreDiagnostics)
        {
            Registry = registry;
            Metrics = coreMetrics;
            Diagnostics = coreDiagnostics;
        }

        /// <summary>
        /// Single entry point to initialize all used services.
        /// </summary>
        /// <param name="options">
        /// The options to customize services initialization.
        /// </param>
        /// <returns>
        /// Return a handle to the initialization operation.
        /// </returns>
        public async Task InitializeAsync(InitializationOptions options)
        {
            try
            {
                if (options is null)
                {
                    options = new InitializationOptions();
                }

                if (!HasRequestedInitialization()
                    || HasInitializationFailed())
                {
                    Registry.Options = options;
                    m_Initialization = new TaskCompletionSource<object>();
                }

                if (!CanInitialize
                    || State != ServicesInitializationState.Uninitialized)
                {
                    await m_Initialization.Task;
                }
                else
                {
                    await InitializeServicesAsync();
                }

                bool HasInitializationFailed()
                {
                    return m_Initialization.Task.IsCompleted
                        && m_Initialization.Task.Status != TaskStatus.RanToCompletion;
                }

                TriggerInitializeSuccess();
            }
            catch (Exception e)
            {
                TriggerInitializeFailed(e);
                throw;
            }
        }

        public string GetIdentifier()
        {
            return Registry.InstanceId;
        }

        void TriggerInitializeSuccess()
        {
            try
            {
                Initialized?.Invoke();
            }
            catch (Exception e)
            {
                CoreLogger.LogError($"{InitSuccessEventInvocationError} {e}");
            }
        }

        void TriggerInitializeFailed(Exception initException)
        {
            try
            {
                InitializeFailed?.Invoke(initException);
            }
            catch (Exception e)
            {
                CoreLogger.LogError($"{InitFailureEventInvocationError} {e}");
            }
        }

        public T GetService<T>()
        {
            return Registry.GetService<T>();
        }

        bool HasRequestedInitialization()
        {
            return !(m_Initialization is null);
        }

        async Task InitializeServicesAsync()
        {
            State = ServicesInitializationState.Initializing;
            var initStopwatch = new Stopwatch();
            initStopwatch.Start();

            var dependencyTree = Registry.PackageRegistry.Tree;
            if (dependencyTree is null)
            {
                var reason = new NullReferenceException("Services require a valid dependency tree to be initialized.");
                FailServicesInitialization(reason);
                throw reason;
            }

            var sortedPackageTypeHashes = new List<int>(dependencyTree.PackageTypeHashToInstance.Count);

            try
            {
                SortPackages();
                await InitializePackagesAsync();
            }
            catch (Exception reason)
            {
                FailServicesInitialization(reason);
                throw;
            }

            SucceedServicesInitialization();

            void SortPackages()
            {
                var sorter = new DependencyTreeInitializeOrderSorter(dependencyTree, sortedPackageTypeHashes);
                sorter.SortRegisteredPackagesIntoTarget();
            }

            async Task InitializePackagesAsync()
            {
                var initializer = new CoreRegistryInitializer(Registry, sortedPackageTypeHashes);
                await initializer.InitializeRegistryAsync();
            }

            void FailServicesInitialization(Exception reason)
            {
                State = ServicesInitializationState.Uninitialized;
                initStopwatch.Stop();
                m_Initialization.TrySetException(reason);
            }

            void SucceedServicesInitialization()
            {
                State = ServicesInitializationState.Initialized;
                Registry.LockComponentRegistration();

                initStopwatch.Stop();
                m_Initialization.TrySetResult(null);
            }
        }

        internal void SendInitializationMetrics(List<PackageInitializationInfo> packageInitInfos)
        {
            foreach (var initInfo in packageInitInfos)
            {
                Metrics.SendInitTimeMetricForPackage(initInfo.PackageType, initInfo.InitializationTimeInSeconds);
            }
        }

        internal void EnableInitialization()
        {
            CanInitialize = true;
        }

        internal async Task EnableInitializationAsync()
        {
            CanInitialize = true;

            CorePackageRegistry.Instance.Lock();

            if (!HasRequestedInitialization())
                return;

            await InitializeServicesAsync();
        }
    }
}
