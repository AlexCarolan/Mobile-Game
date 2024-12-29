using System;
using System.Threading.Tasks;

namespace Unity.Services.Core
{
    /// <summary>
    /// Central registry for an instance of unity services.
    /// </summary>
    public interface IUnityServices
    {
        /// <summary>
        /// Invoked when initialization completes successfully.
        /// </summary>
        event Action Initialized;

        /// <summary>
        /// Invoked when initialization fails.
        /// </summary>
        event Action<Exception> InitializeFailed;

        /// <summary>
        /// The initialization state of the services instance.
        /// </summary>
        ServicesInitializationState State { get; }

        /// <summary>
        /// Initialize the services
        /// </summary>
        /// <param name="options">The options for the services</param>
        /// <returns>Return a handle to the asynchronous initialization process.</returns>
        Task InitializeAsync(InitializationOptions options = null);

        /// <summary>
        /// Provides the unique identifier for the services registry or null for the main services.
        /// </summary>
        /// <returns>The unique identifier for the services registry</returns>
        string GetIdentifier() { return null; }

        /// <summary>
        /// Retrieve a service from the service registry
        /// </summary>
        /// <typeparam name="T">The type that was registered for the service</typeparam>
        /// <returns>The service if available, otherwise null</returns>
        T GetService<T>();
    }
}
