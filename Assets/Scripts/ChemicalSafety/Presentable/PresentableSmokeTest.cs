#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Editor-only desk smoke test: plays the whole Presentable flow by invoking the same events a poke
    /// would, logs [SMOKE] lines to the console and saves screenshots. Menu: Chemical Safety/Presentable/Run Smoke Test.
    /// </summary>
    public sealed class PresentableSmokeTest : MonoBehaviour
    {
        private const string Flag = "PresentableSmoke";
        private const string DirKey = "PresentableSmokeDir";

        private string _dir;
        private int _errors;
        private PresentableSequenceDirector _director;
        private float _hx = 0f, _hy = 0f, _hz = 0f; // controller rotation relative to the head

        [MenuItem("Chemical Safety/Presentable/Run Smoke Test")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
                return;
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!SessionState.GetBool(Flag, false))
                return;
            SessionState.SetBool(Flag, false);
            new GameObject("PresentableSmokeTest").AddComponent<PresentableSmokeTest>();
        }

        private void Start()
        {
            _dir = SessionState.GetString(DirKey, Path.Combine(Application.dataPath, "../Temp/Smoke"));
            Directory.CreateDirectory(_dir);
            Application.logMessageReceived += OnLog;
            StartCoroutine(Main());
        }

        private void OnDestroy() => Application.logMessageReceived -= OnLog;

        private void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
            {
                _errors++;
                Debug.Log("[SMOKE] RUNTIME ERROR: " + msg);
            }
        }

        private static void Say(string s) => Debug.Log("[SMOKE] " + s);

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"), 1);
            yield return null;
            yield return null;
        }

        private IEnumerator WaitStage(string stage, float timeout = 120f)
        {
            var t = 0f;
            while (_director.CurrentStage != stage && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("stage " + stage + (_director.CurrentStage == stage ? " reached" : " NOT reached (at " + _director.CurrentStage + ")"));
        }

        private static T Get<T>(object o, string field)
        {
            var f = o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            return (T)f.GetValue(o);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }

            return null;
        }

        private static void Click(Transform root, string name)
        {
            var t = FindDeep(root, name);
            var b = t != null ? t.GetComponent<Button>() : null;
            if (b == null)
            {
                Say("FAIL button not found: " + name);
                return;
            }

            b.onClick.Invoke();
        }

        private static void Poke(PokeTarget p)
        {
            typeof(PokeTarget).GetMethod("Fire", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(p, new object[] { false });
        }

        private IEnumerator Main()
        {
            _director = FindFirstObjectByType<PresentableSequenceDirector>();
            var flow = _director.transform.parent;
            var art = Get<PresentableArt>(_director, "art");
            Say("voPickLabel=" + (art.voPickLabel != null ? art.voPickLabel.name : "NULL") + " voWrongLabel=" +
                (art.voWrongLabel != null ? art.voWrongLabel.name : "NULL"));

            // --- Instructions panel: Continue hidden during the VO, shown after, speaker replays, Continue advances.
            var panel = Get<InstructionPanelView>(_director, "instructionPanel");
            var voiceSrc = Get<AudioSource>(_director, "voice");
            yield return WaitStage("instruction");
            yield return new WaitForSeconds(1.5f);
            var cont = FindDeep(panel.transform, "ContinueButton");
            Say("during instruction VO: continue visible = " + cont.gameObject.activeSelf + " clip=" +
                (voiceSrc.clip != null ? voiceSrc.clip.name : "none"));
            yield return Shot("00a_instruction_playing");
            var tt = 0f;
            while (!cont.gameObject.activeSelf && tt < 60f)
            {
                tt += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("after VO: continue visible = " + cont.gameObject.activeSelf);
            yield return new WaitForSeconds(1.0f);
            Say("hint clip = " + (voiceSrc.clip != null ? voiceSrc.clip.name : "none") + " playing=" + voiceSrc.isPlaying);
            yield return Shot("00b_instruction_continue");
            yield return new WaitForSeconds(5f);
            Click(panel.transform, "Speaker");
            yield return new WaitForSeconds(1f);
            Say("after speaker click: clip = " + (voiceSrc.clip != null ? voiceSrc.clip.name : "none") + " playing=" + voiceSrc.isPlaying);
            Click(panel.transform, "Speaker");
            yield return new WaitForSeconds(1f);
            Say("after 2nd speaker click: clip = " + (voiceSrc.clip != null ? voiceSrc.clip.name : "none") + " playing=" + voiceSrc.isPlaying +
                " stage=" + _director.CurrentStage);
            Click(panel.transform, "ContinueButton");

            yield return WaitStage("explore");
            yield return new WaitForSeconds(1f);
            yield return Shot("01_explore");
            var camT = Camera.main.transform;
            var keep = camT.localRotation;
            camT.localRotation = Quaternion.Euler(-40f, 0f, 0f);
            yield return Shot("01b_ceiling");
            camT.localRotation = Quaternion.Euler(-5f, -50f, 0f);
            yield return Shot("01c_left");
            camT.localRotation = Quaternion.Euler(-5f, 50f, 0f);
            yield return Shot("01d_right");
            camT.localRotation = Quaternion.Euler(10f, 180f, 0f);
            yield return Shot("01e_back");
            camT.localRotation = keep;

            // --- clipboard held in the left glove: put the left controller where a hand would be
            var clipCtl = Get<SdsClipboardController>(_director, "clipboard");
            // Without a headset the rig deactivates the untracked controllers; wake them for the hand test.
            foreach (var g in FindObjectsByType<GlovedHandPose>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                g.gameObject.SetActive(true);
                foreach (var b in g.GetComponents<Behaviour>())
                {
                    if (b.GetType().Name == "TrackedPoseDriver")
                        b.enabled = false;
                }
            }

            yield return null;
            if (GlovedHandPose.Left == null)
            {
                // In the editor without a headset the gloves' Start is not run by Unity; run it by hand.
                foreach (var g in FindObjectsByType<GlovedHandPose>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    typeof(GlovedHandPose).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(g, null);
            }

            var lh = GlovedHandPose.Left;
            if (lh != null)
            {
                lh.transform.SetPositionAndRotation(
                    camT.position + camT.forward * 0.5f + camT.right * -0.14f + camT.up * -0.12f,
                    camT.rotation * Quaternion.Euler(Get<float>(this, "_hx"), Get<float>(this, "_hy"), Get<float>(this, "_hz")));
                clipCtl.ForceHandMode();
                yield return new WaitForSeconds(0.6f);
                clipCtl.SetVisible(false);
                yield return new WaitForSeconds(0.2f);
                clipCtl.SetVisible(true);
                yield return new WaitForSeconds(0.15f);
                yield return Shot("02a_clip_rising");
                yield return new WaitForSeconds(1.2f);
                yield return Shot("02b_clip_held");
                clipCtl.SetVisible(false);
            }
            else
            {
                Say("FAIL: no left glove");
            }
            yield return new WaitForSeconds(0.5f);
            Say("STATS (editor Game view, explore camera): tris=" + UnityStats.triangles + " verts=" + UnityStats.vertices +
                " drawCalls=" + UnityStats.drawCalls + " setPass=" + UnityStats.setPassCalls);

            var drums = Get<DrumCallout[]>(_director, "drums");
            foreach (var d in drums)
            {
                var g = Get<CanvasGroup>(d, "_bubbleGroup");
                Say(d.name + " plate alpha at start = " + g.alpha);
            }

            var hs1 = Get<FloorHotspot>(_director, "hotspotSc01");
            Say("Hotspot_SC01 position = " + hs1.transform.position);
            hs1.Activate();
            yield return WaitStage("pickLabel");
            var voice = Get<AudioSource>(_director, "voice");
            yield return new WaitForSeconds(0.5f);
            Say("voice clip after hotspot = " + (voice.clip != null ? voice.clip.name : "none") +
                " playing=" + voice.isPlaying);
            yield return new WaitForSeconds(2.2f);
            var rig = Get<Transform>(_director, "rig");
            Say("rig position after glide = " + rig.position + " camera=" + Camera.main.transform.position);
            yield return Shot("02_pick_stage");

            var popup = Get<LabelPickPopup>(_director, "labelPopup");
            var pokes = Get<PokeTarget[]>(_director, "drumPokes");
            var order = new[] { 1, 0, 3, 2 };
            var step = 0;
            foreach (var i in order)
            {
                Say("poke drum " + (i + 1) + " interactable=" + pokes[i].Interactable);
                Poke(pokes[i]);
                yield return new WaitForSeconds(0.8f);
                Say("popup open = " + popup.IsOpen);
                if (step == 0)
                    yield return Shot("03_popup");

                if (step == 0)
                {
                    // wrong first on the first drum
                    var wrong = (i + 1) % 4;
                    Click(popup.transform, "Option" + (wrong + 1));
                    yield return new WaitForSeconds(1.0f);
                    Say("after wrong: popup open = " + popup.IsOpen + " voice=" +
                        (voice.clip != null ? voice.clip.name : "none") + " playing=" + voice.isPlaying);
                    yield return Shot("04_popup_wrong");
                }

                Click(popup.transform, "Option" + (i + 1));
                yield return new WaitForSeconds(1.2f);
                var g = Get<CanvasGroup>(drums[i], "_bubbleGroup");
                Say("drum " + (i + 1) + " done: popup open=" + popup.IsOpen + " plate alpha=" + g.alpha +
                    " pokable again=" + pokes[i].Interactable);
                if (step == 0 || step == 3)
                    yield return Shot("05_plate_" + (step + 1));
                step++;
            }

            yield return WaitStage("trolley");
            yield return new WaitForSeconds(2.5f);
            var trolley = Get<GameObject>(_director, "trolley");
            Say("trolley active = " + trolley.activeSelf + " pos=" + trolley.transform.position);
            yield return Shot("06_trolley");
            Poke(Get<PokeTarget>(_director, "trolleyPoke"));

            yield return WaitStage("mcq1");
            yield return new WaitForSeconds(2f);
            var mcq1 = Get<PresentableMcqPanel>(_director, "mcqSc01");
            Say("mcq1 open = " + mcq1.IsOpen);
            yield return Shot("07_question");
            Click(mcq1.transform, "Row1");
            yield return new WaitForSeconds(1.2f);
            yield return Shot("08_wrong");
            Click(mcq1.transform, "Close");
            yield return new WaitForSeconds(0.8f);
            Click(mcq1.transform, "Row4");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("09_correct");
            Click(mcq1.transform, "NextButton");

            yield return WaitStage("sc02");
            yield return new WaitForSeconds(1.5f);
            var hs2 = Get<FloorHotspot>(_director, "hotspotSc02");
            hs2.Activate();
            var mcq2 = Get<PresentableMcqPanel>(_director, "mcqSc02");
            var t = 0f;
            while (!mcq2.IsOpen && t < 15f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(1.5f);
            Say("mcq2 open = " + mcq2.IsOpen);
            yield return Shot("10_sc02_question");
            Click(mcq2.transform, "Row2");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("11_sc02_correct");
            Click(mcq2.transform, "NextButton");
            yield return WaitStage("complete", 30f);
            yield return new WaitForSeconds(1f);
            yield return Shot("12_complete");

            Say("DONE runtime errors/exceptions = " + _errors);
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
