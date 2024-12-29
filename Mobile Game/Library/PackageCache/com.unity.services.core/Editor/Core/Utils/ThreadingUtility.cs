using System;
using System.Runtime.CompilerServices;
using UnityEditor;

namespace Unity.Services.Core.Editor
{
    static class ThreadingUtility
    {
        /// <summary>
        /// Used for UI updates requested from methods that may not run on the main thread (UI updates should not
        /// be called from any other thread than the main thread)
        /// </summary>
        public static void RunNextUpdateOnMain(
            Action action,
            [CallerFilePath] string file = null,
            [CallerMemberName] string caller = null,
            [CallerLineNumber] int line = 0)
        {
            EditorApplication.CallbackFunction callback = null;
            callback = () =>
            {
                EditorApplication.update -= callback;
                try
                {
                    action();
                }
                catch (Exception e) when (caller != null && file != null && line != 0)
                {
                    throw new Exception($"Exception thrown from invocation made by '{file}'({line}) by {caller}", e);
                }
            };
            EditorApplication.update += callback;
        }
    }
}
