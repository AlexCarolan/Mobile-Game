using UnityEngine;

namespace Unity.Services.Analytics.Internal
{
    internal interface IPersistence
    {
        void SaveValue(string key, int value);
        void SaveValue(string key, string value);

        int LoadInt(string key);
        string LoadString(string key);
    }

    internal class PlayerPrefsPersistence : IPersistence
    {
        // NOTE: There are risks to using PlayerPrefs, as those are easy to wipe (developer calls DeleteAll).
        // However, we cannot use a file due to console platform restrictions on disk access. So for now
        // we must accept the risks of using PlayerPrefs.

        public void SaveValue(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }

        public void SaveValue(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        public int LoadInt(string key)
        {
            if (PlayerPrefs.HasKey(key))
            {
                return PlayerPrefs.GetInt(key);
            }
            else
            {
                return 0;
            }
        }

        public string LoadString(string key)
        {
            if (PlayerPrefs.HasKey(key))
            {
                return PlayerPrefs.GetString(key);
            }
            else
            {
                return null;
            }
        }
    }
}
