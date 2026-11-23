using System;
using UnityEngine;

namespace ChemicalSafety.Results
{
    [Serializable]
    public sealed class ResultApiProfile
    {
        public string name;      // stage | production
        public string baseUrl;   // e.g. https://gizvr.sensor.buzz/api/v1/
        public string batchPath; // results/batch
        public string apiKey;
    }

    [Serializable]
    public sealed class ResultApiConfigData
    {
        public string active; // the profile this build talks to
        public ResultApiProfile[] profiles;
    }

    /// <summary>
    /// Which server the results go to. Read once from Resources/ResultApiConfig.json (baked into the build; switch it with the menu
    /// Chemical Safety/Presentable/API Server, or in the Editor with the environment variable CHEMSAFETY_API_ENV = stage | production).
    /// If the active profile is missing or broken nothing is sent (results stay on the device and are retried), it never falls back to another server.
    /// </summary>
    public static class ResultApiConfig
    {
        public const string ResourceName = "ResultApiConfig";

        private static ResultApiProfile _profile;
        private static bool _loaded;

        public static string ActiveName
        {
            get
            {
                Load();
                return _profile != null ? _profile.name : "none";
            }
        }

        public static string BatchUrl
        {
            get
            {
                Load();
                return _profile == null ? string.Empty : Join(_profile.baseUrl, _profile.batchPath);
            }
        }

        public static string ApiKey
        {
            get
            {
                Load();
                return _profile != null ? _profile.apiKey ?? string.Empty : string.Empty;
            }
        }

        public static void Reload() => _loaded = false;

        public static string Join(string baseUrl, string path)
        {
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(path))
                return string.Empty;
            return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        }

        /// <summary>Parses the config text; returns the active profile (or null). Pure, used by the tests.</summary>
        public static ResultApiProfile Pick(string json, string overrideName)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            ResultApiConfigData data;
            try
            {
                data = JsonUtility.FromJson<ResultApiConfigData>(json);
            }
            catch (ArgumentException)
            {
                return null; // not JSON
            }

            if (data == null || data.profiles == null)
                return null;
            var want = string.IsNullOrWhiteSpace(overrideName) ? data.active : overrideName.Trim();
            foreach (var p in data.profiles)
            {
                if (p != null && string.Equals(p.name, want, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(p.baseUrl) && !string.IsNullOrWhiteSpace(p.batchPath))
                    return p;
            }

            return null;
        }

        private static void Load()
        {
            if (_loaded)
                return;
            _loaded = true;
            _profile = null;
            try
            {
                var asset = Resources.Load<TextAsset>(ResourceName);
                string over = null;
#if UNITY_EDITOR
                over = Environment.GetEnvironmentVariable("CHEMSAFETY_API_ENV");
#endif
                if (asset == null)
                {
                    Debug.LogError("[Upload] Resources/" + ResourceName + ".json is missing: nothing will be sent (results stay on the device)");
                    return;
                }

                _profile = Pick(asset.text, over);
                Resources.UnloadAsset(asset);
                if (_profile == null)
                    Debug.LogError("[Upload] no valid active API profile in " + ResourceName + ".json: nothing will be sent (results stay on the device)");
                else
                    Debug.Log("[Upload] server = " + _profile.name.ToUpperInvariant() + " " + Join(_profile.baseUrl, _profile.batchPath));
            }
            catch (Exception e)
            {
                _profile = null;
                Debug.LogError("[Upload] API config could not be read: " + e.Message);
            }
        }
    }
}
