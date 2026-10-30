using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChemicalSafety.Localization
{
    public enum Lang { En = 0, Bn = 1 }

    [Serializable]
    public sealed class LText
    {
        public string en;
        public string bn;

        public string Get(Lang lang) => lang == Lang.Bn ? bn : en;
    }

    [Serializable]
    public sealed class LString
    {
        public string key;
        public string en;
        public string bn;
    }

    [Serializable]
    public sealed class LArray
    {
        public string key;
        public string[] en = Array.Empty<string>();
        public string[] bn = Array.Empty<string>();
    }

    [Serializable]
    public sealed class LOption
    {
        public string id;
        public LText text;
        public LText feedback;
        public LText wrongTitle;
    }

    [Serializable]
    public sealed class LQuestion
    {
        public string id;
        public LText header;
        public LText prompt;
        public LOption[] options = Array.Empty<LOption>();
        [Tooltip("Id of the correct option (never an index: the order is shuffled per session).")]
        public string correct;
        public LText correctTitle;
        public LText wrongTitle;
        public LText nextLabel;
        [Tooltip("false when an option refers to other options by position (all of the above, A and B).")]
        public bool shuffle = true;
    }

    [Serializable]
    public sealed class ScenarioContent
    {
        public string scenario;
        public LString[] strings = Array.Empty<LString>();
        public LArray[] arrays = Array.Empty<LArray>();
        public LQuestion[] questions = Array.Empty<LQuestion>();
    }

    /// <summary>
    /// Single entry point for every text of the game. One JSON file per scenario holds English and Bangla together
    /// (Assets/Resources/Content/SC01.json ...), so the two languages cannot drift apart.
    /// </summary>
    public static class Loc
    {
        public const string ResourceFolder = "Content/";

        private static readonly Dictionary<string, ScenarioContent> Cache = new Dictionary<string, ScenarioContent>();

        public static Lang Current { get; private set; } = Lang.En;
        public static bool IsBangla => Current == Lang.Bn;
        public static event Action LanguageChanged;

        public static void SetLanguage(Lang lang)
        {
            if (Current == lang)
                return;
            Current = lang;
            LanguageChanged?.Invoke();
        }

        public static string Suffix(Lang lang) => lang == Lang.Bn ? "bn" : "en";
        public static string Suffix() => Suffix(Current);

        public static ScenarioContent Scenario(string scenario)
        {
            if (Cache.TryGetValue(scenario, out var cached))
                return cached;
            var asset = Resources.Load<TextAsset>(ResourceFolder + scenario);
            ScenarioContent content = null;
            if (asset != null)
                content = JsonUtility.FromJson<ScenarioContent>(asset.text);
            else
                Debug.LogError("[Loc] Missing content file Resources/" + ResourceFolder + scenario);
            Cache[scenario] = content ?? new ScenarioContent { scenario = scenario };
            return Cache[scenario];
        }

        public static void ClearCache()
        {
            Cache.Clear();
            _arrays.Clear();
            _en2bn = null;
        }

        private static readonly Dictionary<string, string[]> _arrays = new Dictionary<string, string[]>();
        private static Dictionary<string, string> _en2bn;

        /// <summary>String array for a key in the current language (cached; the same array instance is returned every call).</summary>
        public static string[] A(string scenario, string key)
        {
            var id = scenario + "/" + key + "/" + Suffix();
            if (_arrays.TryGetValue(id, out var cached))
                return cached;
            string[] result = null;
            foreach (var a in Scenario(scenario).arrays)
            {
                if (a.key != key)
                    continue;
                result = Current == Lang.Bn ? a.bn : a.en;
                if (result == null || result.Length == 0 || result.Length != a.en.Length)
                {
                    Debug.LogError($"[Loc] {scenario}/{key} has a missing {Suffix()} array");
                    result = a.en;
                }

                break;
            }

            if (result == null)
            {
                Debug.LogError($"[Loc] Missing array {scenario}/{key}");
                result = Array.Empty<string>();
            }

            _arrays[id] = result;
            return result;
        }

        /// <summary>Bangla text for an English UI literal (looked up by the exact English text), or null.</summary>
        public static string Translate(string en)
        {
            if (string.IsNullOrEmpty(en))
                return null;
            if (_en2bn == null)
                BuildIndex();
            return _en2bn.TryGetValue(en, out var bn) ? bn : null;
        }

        private static void BuildIndex()
        {
            _en2bn = new Dictionary<string, string>();
            foreach (var asset in Resources.LoadAll<TextAsset>("Content"))
            {
                ScenarioContent c;
                try { c = JsonUtility.FromJson<ScenarioContent>(asset.text); }
                catch (Exception) { continue; }
                if (c == null)
                    continue;
                foreach (var s in c.strings)
                    Add(s.en, s.bn);
                foreach (var a in c.arrays)
                {
                    for (var i = 0; i < a.en.Length && i < a.bn.Length; i++)
                        Add(a.en[i], a.bn[i]);
                }
            }
        }

        private static void Add(string en, string bn)
        {
            if (!string.IsNullOrEmpty(en) && !string.IsNullOrEmpty(bn) && !_en2bn.ContainsKey(en))
                _en2bn[en] = bn;
        }

        public static bool HasBengali(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            foreach (var c in s)
            {
                if (c >= 'ঀ' && c <= '৿')
                    return true;
            }

            return false;
        }

        /// <summary>Text for a key in the current language; falls back to English, then to the key, and logs it loudly.</summary>
        public static string T(string scenario, string key) => T(scenario, key, Current);

        public static string T(string scenario, string key, Lang lang)
        {
            foreach (var s in Scenario(scenario).strings)
            {
                if (s.key != key)
                    continue;
                var text = lang == Lang.Bn ? s.bn : s.en;
                if (!string.IsNullOrEmpty(text))
                    return text;
                Debug.LogError($"[Loc] {scenario}/{key} has no {Suffix(lang)} text");
                return s.en ?? key;
            }

            Debug.LogError($"[Loc] Missing key {scenario}/{key}");
            return key;
        }

        public static LQuestion Question(string scenario, string questionId)
        {
            foreach (var q in Scenario(scenario).questions)
            {
                if (q.id == questionId)
                    return q;
            }

            Debug.LogError($"[Loc] Missing question {questionId} in {scenario}");
            return null;
        }
    }
}
