using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ChemicalSafety.Results
{
    /// <summary>
    /// Sends the finished submission ONCE to POST {BaseUrl}/api/v1/results (all scenarios done, or the 40-minute time-out).
    /// The request body is saved first as results/&lt;id&gt;.api.json; it is deleted when the server answers 200/201 (a repeat of the same
    /// client_submission_id is answered 200 and creates nothing, so a retry is safe). On a network / server error the file stays and is sent
    /// again with a back-off and on the next app start. A 4xx answer (wrong Training ID, validation) is logged and not retried.
    /// Never blocks the game: all work is a coroutine on its own DontDestroyOnLoad object.
    /// </summary>
    public sealed class ResultUploader : MonoBehaviour
    {
        /// <summary>false = no network call at all.</summary>
        public static bool Enabled = true;

        /// <summary>false = only the sample fields go to the API. true = also email, phone, totals and per-answer extras (when the backend accepts them).</summary>
        public const bool SendExtras = false;

        private const string BaseUrl = "http://52.89.166.172";
        private const string Path = "/api/v1/results";
        private const string ApiKey = "ddf75eceff87883b79af6b87e25d6a84a635c0e7ecaa6fcda822c555cc2f5c57";
        private const int MaxTries = 6;
        private const string Suffix = ".api.json";

        private static ResultUploader _inst;

        private static ResultUploader Inst
        {
            get
            {
                if (_inst == null)
                {
                    var go = new GameObject("ResultUploader");
                    DontDestroyOnLoad(go);
                    _inst = go.AddComponent<ResultUploader>();
                }

                return _inst;
            }
        }

        private static string Folder => FileSubmissionSink.Folder;

        /// <summary>Called once per participant, right after SessionResults.CompleteAll.</summary>
        public static void Send(Submission s)
        {
            if (!Enabled || !SessionResults.UploadAllowed || s == null || s.training_id <= 0)
            {
                Debug.Log("[Upload] skipped (disabled / test session / no training id)");
                return;
            }

            try
            {
                Directory.CreateDirectory(Folder);
                var path = System.IO.Path.Combine(Folder, s.client_submission_id + Suffix);
                File.WriteAllText(path, SubmissionExport.ToApiJson(s, SendExtras), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogError("[Upload] could not write the request body: " + e.Message);
                return;
            }

            Inst.StartCoroutine(Inst.Post(s.client_submission_id));
        }

        /// <summary>App start: every body that was never accepted is sent again.</summary>
        public static void RetryPending()
        {
            if (!Enabled || !Directory.Exists(Folder))
                return;
            foreach (var f in Directory.GetFiles(Folder, "*" + Suffix))
            {
                var id = System.IO.Path.GetFileName(f);
                id = id.Substring(0, id.Length - Suffix.Length);
                Inst.StartCoroutine(Inst.Post(id));
            }
        }

        private IEnumerator Post(string id)
        {
            var path = System.IO.Path.Combine(Folder, id + Suffix);
            for (var attempt = 1; attempt <= MaxTries; attempt++)
            {
                if (!File.Exists(path))
                    yield break;
                byte[] body;
                try
                {
                    body = File.ReadAllBytes(path);
                }
                catch (Exception e)
                {
                    Debug.LogError("[Upload] read failed: " + e.Message);
                    yield break;
                }

                using (var req = new UnityWebRequest(BaseUrl + Path, UnityWebRequest.kHttpVerbPOST))
                {
                    req.uploadHandler = new UploadHandlerRaw(body);
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("x-api-key", ApiKey);
                    req.timeout = 20;
                    yield return req.SendWebRequest();
                    var code = req.responseCode;
                    if (req.result == UnityWebRequest.Result.Success && (code == 200 || code == 201))
                    {
                        Debug.Log("[Upload] OK " + code + " " + req.downloadHandler.text);
                        try
                        {
                            File.Delete(path);
                            File.WriteAllText(System.IO.Path.Combine(Folder, id + ".sent"), DateTime.UtcNow.ToString("o"));
                        }
                        catch (Exception)
                        {
                            // marker only
                        }

                        yield break;
                    }

                    var text = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                    var retryable = req.result != UnityWebRequest.Result.ProtocolError || code >= 500 || code == 429 || code == 408;
                    Debug.LogWarning("[Upload] try " + attempt + " failed: " + req.result + " " + code + " " + req.error + " " + text);
                    if (!retryable)
                    {
                        Debug.LogError("[Upload] rejected by the server (not retried): " + code + " " + text);
                        yield break;
                    }
                }

                yield return new WaitForSecondsRealtime(Mathf.Min(60f, 3f * attempt * attempt));
            }
            // file stays: RetryPending() sends it at the next start
        }
    }
}
