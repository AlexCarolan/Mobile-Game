using System;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.Services.Core.Components
{
    /// <summary>
    /// Offers <see cref="IUnityServices"/> events as unity events.
    /// </summary>
    [Serializable]
    public class ServicesInitializationEvents
    {
        /// <summary>
        /// Offers <see cref="IUnityServices.Initialized"/> as a unity event.
        /// </summary>
        [SerializeField]
        public UnityEvent Initialized = new UnityEvent();

        /// <summary>
        /// Offers <see cref="IUnityServices.InitializeFailed"/> as a unity event.
        /// </summary>
        [SerializeField]
        public UnityEvent<Exception> InitializeFailed = new UnityEvent<Exception>();
    }
}
