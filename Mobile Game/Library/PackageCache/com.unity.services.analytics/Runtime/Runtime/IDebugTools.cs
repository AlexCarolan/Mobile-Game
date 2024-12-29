using System;
using System.Collections.Generic;

namespace Unity.Services.Analytics.Internal
{
    internal interface IServiceDebug
    {
        bool IsActive { get; }
        IIdentityManager UserIdentity { get; }
    }

    internal interface IBufferDebug
    {
        event Action<string, string, DateTime, byte[]> EventRecorded;
        event Action<HashSet<string>> EventsClearing;
        event Action<HashSet<string>> EventsCleared;
    }

    internal interface IDispatcherDebug
    {
        bool FlushInProgress { get; }

        event Action<byte[]> FlushStarted;
        event Action<int, bool, bool, bool, bool, byte[]> FlushFinished;
    }

    internal interface IContainerDebug
    {
        float TimeUntilNextHeartbeat { get; }
    }
}
