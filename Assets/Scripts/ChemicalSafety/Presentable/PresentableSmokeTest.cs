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

        // Every visible text of a panel must stay inside the safe area of its frame (not on the border arcs) and
        // option labels inside their own row.
        private static int LayoutCheck(string where, Transform root)
        {
            var canvas = root.GetComponentInChildren<Canvas>(true);
            if (canvas == null)
                return 0;
            var crt = (RectTransform)canvas.transform;
            var cr = crt.rect;
            var bad = 0;
            var count = 0;
            foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                if (string.IsNullOrEmpty(t.text) || !t.gameObject.activeInHierarchy)
                    continue;
                var skip = false;
                for (var q = t.transform; q != null && q != root; q = q.parent)
                {
                    if (q.name == "NextButton" || q.name == "ContinueButton")
                        skip = true;
                }

                if (skip)
                    continue;
                t.ForceMeshUpdate(true);
                var b = t.textBounds;
                var rt = t.rectTransform;
                count++;
                Vector2 InCanvas(Vector3 lp)
                {
                    var l = crt.InverseTransformPoint(rt.TransformPoint(lp));
                    return new Vector2((l.x - cr.xMin) / cr.width, (l.y - cr.yMin) / cr.height);
                }

                var mn = InCanvas(b.min);
                var mx = InCanvas(b.max);
                var outside = mn.x < 0.05f || mx.x > 0.95f || mn.y < 0.09f || mx.y > 0.95f;
                var inRow = true;
                var parent = rt.parent as RectTransform;
                if (parent != null && (parent.name.StartsWith("Row") || parent.name.StartsWith("Option")))
                {
                    var pr = parent.rect;
                    var a = parent.InverseTransformPoint(rt.TransformPoint(b.min));
                    var c = parent.InverseTransformPoint(rt.TransformPoint(b.max));
                    inRow = a.x >= pr.xMin - 2f && c.x <= pr.xMax + 2f && a.y >= pr.yMin - 2f && c.y <= pr.yMax + 2f;
                }

                if (outside || !inRow)
                {
                    bad++;
                    var txt = t.text.Replace("\n", " ");
                    Say("LAYOUT FAIL " + where + ": '" + (txt.Length > 40 ? txt.Substring(0, 40) : txt) + "' x " +
                        mn.x.ToString("0.00") + "-" + mx.x.ToString("0.00") + " y " + mn.y.ToString("0.00") + "-" +
                        mx.y.ToString("0.00") + (inRow ? "" : " (leaves its row)"));
                }
            }

            Say("layout " + where + ": " + count + " texts checked, violations = " + bad);
            return bad;
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

        private static void PressButton(FloatingTouchButton b)
        {
            typeof(FloatingTouchButton).GetMethod("Press", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(b, new object[] { false });
        }

        private static void Poke(PokeTarget p)
        {
            typeof(PokeTarget).GetMethod("Fire", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(p, new object[] { false });
        }

        // The fingertip must rest on a panel instead of sinking into it, and still press the button.
        private IEnumerator PanelStopTest(LabelPickPopup popup)
        {
            var hand = GlovedHandPose.Right;
            var opt = FindDeep(popup.transform, "Option1");
            var btn = opt != null ? opt.GetComponent<Button>() : null;
            if (hand == null || btn == null || !hand.HasTip)
            {
                Say("FAIL: panel stop test needs the right glove and Option1");
                yield break;
            }

            var fired = 0;
            UnityEngine.Events.UnityAction count = () => fired++;
            btn.onClick.AddListener(count);
            var rt = (RectTransform)opt;
            var canvasT = rt.GetComponentInParent<Canvas>().transform;
            var ctl = hand.transform;
            var keep = ctl.position;
            var behind = rt.TransformPoint(rt.rect.center) + canvasT.forward * 0.04f; // 4 cm behind the panel
            ctl.position += behind - hand.PressPoint;
            var trace = "";
            for (var f = 0; f < 120; f++)
            {
                yield return null;
                var lp = rt.InverseTransformPoint(hand.PressPoint);
                if (f % 6 == 0)
                    {
                    var cg = rt.GetComponentInParent<CanvasGroup>();
                    trace += (-lp.z * rt.lossyScale.z * 100f).ToString("0.0") + "|" + fired + "|a" +
                             (cg != null ? cg.alpha.ToString("0.0") : "-") + "|p" +
                             (FingerPokeSystem.PanelPush(hand.PressPoint, 0.012f).magnitude * 100f).ToString("0.0") + " ";
                }
            }

            Say("panel stop trace (cm in front of option | fired): " + trace);
            var local = rt.InverseTransformPoint(hand.PressPoint);
            var depthCm = -local.z * rt.lossyScale.z * 100f;
            Say("panel stop: fingertip started 4 cm behind the popup, now " + depthCm.ToString("0.0") +
                " cm in front of the option, option fired " + fired + "x -> " + (depthCm > -0.2f && fired >= 1 ? "OK" : "FAIL"));
            yield return Shot("03b_panel_stop");
            btn.onClick.RemoveListener(count);
            ctl.position = keep;
            yield return new WaitForSeconds(0.4f);
        }

        // The chart box on the details card must be within arm's reach and open the chart when touched.
        private IEnumerator ChartBoxTouchTest(ContainerInspect inspect, ChartViewer chart)
        {
            var hand = GlovedHandPose.Right;
            var card = FindDeep(inspect.transform, "InspectCard");
            var box = card != null ? FindDeep(card, "ChartBox") : null;
            if (hand == null || box == null)
            {
                Say("FAIL: chart box touch test needs the right glove and the card");
                yield break;
            }

            var rt = (RectTransform)box;
            var centre = rt.TransformPoint(rt.rect.center);
            Say("chart box distance from the head = " +
                Vector3.Distance(Camera.main.transform.position, centre).ToString("0.00") + " m (reach is about 0.65)");
            var ctl = hand.transform;
            var keep = ctl.position;
            ctl.position += centre + card.forward * 0.04f - hand.PressPoint; // 4 cm behind the box
            yield return new WaitForSeconds(0.7f);
            Say("chart opened by touching the box = " + chart.IsOpen + " -> " + (chart.IsOpen ? "OK" : "FAIL"));
            if (chart.IsOpen)
                Click(chart.transform, "CloseChart");
            ctl.position = keep;
            yield return new WaitForSeconds(0.8f);
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
            var subs = Get<WorldSubtitle>(_director, "subtitle");
            var vw = 0f;
            while (voiceSrc.isPlaying && vw < 20f)
            {
                vw += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.8f);
            Say("subtitle visible after the hint voice ended = " + subs.IsVisible + " (expected False)");
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
            var buttons = Get<FloatingTouchButton[]>(_director, "drumButtons");
            for (var bi = 0; bi < buttons.Length; bi++)
            {
                var dp = drums[bi].transform.position;
                var bp = buttons[bi].transform.position;
                Say("arrow " + (bi + 1) + " offset from drum centre: dx=" + (bp.x - dp.x).ToString("0.000") + " dz=" +
                    (bp.z - dp.z).ToString("0.000") + " height=" + bp.y.ToString("0.00"));
            }

            // close-up of a drum Touch button (arrow icon)
            var btnCam = Camera.main.transform;
            var btnKeep = btnCam.rotation;
            btnCam.rotation = Quaternion.LookRotation(buttons[1].transform.position - btnCam.position);
            yield return new WaitForSeconds(0.2f);
            yield return Shot("02b_touch_button");
            yield return new WaitForSeconds(0.35f);
            yield return Shot("02c_touch_button_blink");
            btnCam.rotation = btnKeep;
            var order = new[] { 1, 0, 3, 2 };
            var step = 0;
            foreach (var i in order)
            {
                Say("touch drum " + (i + 1) + " button armed=" + buttons[i].IsArmed);
                PressButton(buttons[i]);
                yield return new WaitForSeconds(0.8f);
                Say("popup open = " + popup.IsOpen);
                if (step == 0)
                    LayoutCheck("SC01 label popup", popup.transform);
                if (step == 0)
                {
                    yield return Shot("03_popup");
                    yield return PanelStopTest(popup);
                }

                if (step == 0)
                {
                    // wrong first on the first drum
                    var wrong = (i + 1) % 4;
                    Click(popup.transform, "Option" + (wrong + 1));
                    yield return new WaitForSeconds(1.0f);
                    Say("after wrong: popup open = " + popup.IsOpen + " voice=" +
                        (voice.clip != null ? voice.clip.name : "none") + " playing=" + voice.isPlaying);
                    LayoutCheck("SC01 label popup (wrong)", popup.transform);
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
            var carrierBtn = Get<FloatingTouchButton>(_director, "carrierButton");
            Say("carrier button armed = " + carrierBtn.IsArmed + " subtitle visible = " + Get<WorldSubtitle>(_director, "subtitle").IsVisible);
            PressButton(carrierBtn);
            yield return new WaitForSeconds(1.1f);
            yield return Shot("06b_loaded");

            yield return WaitStage("mcq1");
            yield return new WaitForSeconds(2f);
            var mcq1 = Get<PresentableMcqPanel>(_director, "mcqSc01");
            Say("mcq1 open = " + mcq1.IsOpen);
            LayoutCheck("SC01 MCQ", mcq1.transform);
            yield return Shot("07_question");
            Click(mcq1.transform, "Row1");
            yield return new WaitForSeconds(1.2f);
            LayoutCheck("SC01 MCQ wrong card", mcq1.transform);
            yield return Shot("08_wrong");
            Click(mcq1.transform, "Close");
            yield return new WaitForSeconds(0.8f);
            Click(mcq1.transform, "Row4");
            var heard = "";
            var sawCorrect1 = false;
            var waited = 0f;
            while (waited < 8f)
            {
                if (voice.clip != null && voice.clip.name.Contains("Sc01Complete"))
                    heard = voice.clip.name;
                if (voice.clip != null && voice.clip.name == "VO_Correct_bn")
                    sawCorrect1 = true;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("scenario complete vo = " + (heard.Length > 0 ? heard : "NOT HEARD") + ", common correct-answer voice heard = " + sawCorrect1);
            yield return Shot("09_correct");
            LayoutCheck("SC01 MCQ correct card", mcq1.transform);
            Click(mcq1.transform, "NextButton");

            yield return WaitStage("sc02Intro");
            var cont2 = FindDeep(panel.transform, "ContinueButton");
            var waitIntro = 0f;
            while (cont2 != null && !cont2.gameObject.activeSelf && waitIntro < 40f)
            {
                waitIntro += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.6f);
            yield return Shot("10_sc02_instruction");
            Click(panel.transform, "ContinueButton");

            yield return WaitStage("sc02Explore");
            yield return new WaitForSeconds(1.2f);
            yield return Shot("11_sc02_room");
            Say("STATS sc02: tris=" + UnityStats.triangles + " drawCalls=" + UnityStats.drawCalls);
            var roomCam = Camera.main.transform;
            var roomKeep = roomCam.localRotation;
            roomCam.localRotation = Quaternion.Euler(-4f, 62f, 0f);
            yield return Shot("11b_sc02_viewer_left");
            roomCam.localRotation = Quaternion.Euler(-4f, -62f, 0f);
            yield return Shot("11c_sc02_viewer_right");
            roomCam.localRotation = Quaternion.Euler(-4f, -62f, 0f) ;
            roomCam.localRotation = roomKeep;

            var hsPick = Get<FloorHotspot>(_director, "hotspotSc02Pick");
            var placement = Get<ContainerPlacement>(_director, "placement");
            var inspect = Get<ContainerInspect>(_director, "inspect");
            var doors = Get<CompartmentDoor[]>(_director, "doors");
            var chart = Get<ChartViewer>(_director, "chart");
            Say("pick marker armed = " + hsPick.IsArmed +
                ", placement = " + (placement != null) + ", inspect = " + (inspect != null && inspect.Count == 6));

            // reach: horizontal distance from the Stand Here marker to every container (arm reach is about 0.7 m)
            for (var ri = 0; ri < inspect.Count; ri++)
            {
                var cr = inspect.PokeOf(ri).GetComponent<Renderer>();
                var cp = cr != null ? cr.bounds.center : inspect.PokeOf(ri).transform.position;
                var mp = hsPick.transform.position;
                Say("reach container " + ri + " from the pick marker: " +
                    Vector2.Distance(new Vector2(cp.x, cp.z), new Vector2(mp.x, mp.z)).ToString("0.00") + " m");
            }

            // walk to the pallet marker
            hsPick.Activate();
            yield return new WaitForSeconds(0.6f);

            // doors with nothing in the hand: plain open / close
            Poke(doors[0].GetComponent<PokeTarget>());
            yield return new WaitForSeconds(1.0f);
            Say("door 0 open after a poke (empty hand) = " + doors[0].IsOpen);
            yield return Shot("12_sc02_door");
            Poke(doors[0].GetComponent<PokeTarget>());
            yield return new WaitForSeconds(1.0f);
            Say("door 0 open after the second poke = " + doors[0].IsOpen + " (expected False)");

            if (chart != null)
            {
                Poke(chart.GetComponent<PokeTarget>());
                yield return new WaitForSeconds(0.6f);
                Say("chart open = " + chart.IsOpen);
                yield return Shot("13_sc02_chart");
                Click(chart.transform, "CloseChart");
                yield return new WaitForSeconds(0.4f);
            }

            // container inspect: hand + card + chart box + card text fit
            Poke(inspect.PokeOf(0));
            yield return new WaitForSeconds(1.0f);
            Say("held after poke 1 = " + inspect.HeldName);
            yield return Shot("13b_inspect_held");

            // grip: put the left glove where a hand would be; the container should rest in the palm with the
            // fingers closed round it, and the scan beam runs from it to the details card
            var cam0 = Camera.main.transform;
            inspect.ForceHandMode();
            var lhand = GlovedHandPose.Left;
            if (lhand != null)
            {
                lhand.transform.SetPositionAndRotation(
                    cam0.position + cam0.forward * 0.5f + cam0.right * -0.16f + cam0.up * -0.2f, cam0.rotation);
                yield return new WaitForSeconds(1.3f);
                yield return Shot("13f_grip");
                var beamT = inspect.transform.Find("ScanBeam");
                var beamLr = beamT != null ? beamT.GetComponent<LineRenderer>() : null;
                Say("scan beam visible = " + (beamLr != null && beamLr.enabled));
            }
            else
            {
                Say("FAIL: no left glove for the grip test");
            }
            var card = FindDeep(inspect.transform, "InspectCard");
            Click(card, "ChartBox");
            yield return new WaitForSeconds(0.6f);
            Say("chart open from card = " + chart.IsOpen);
            yield return new WaitForSeconds(0.5f);
            Say("details card visible while the chart is open = " + inspect.CardVisible + " (expected False)");
            Click(chart.transform, "CloseChart");
            yield return new WaitForSeconds(0.7f);
            Say("details card visible after the chart closed = " + inspect.CardVisible + " (expected True)");
            yield return ChartBoxTouchTest(inspect, chart);
            for (var ci = 0; ci < inspect.Count; ci++)
            {
                Poke(inspect.PokeOf(ci));
                yield return new WaitForSeconds(0.9f);
                var spill = inspect.CheckCardFits();
                Say("card " + ci + " (" + inspect.HeldName + ") text fits = " + (spill.Length == 0) +
                    (spill.Length > 0 ? " FAIL spill: " + spill : ""));
                if (ci == 0 || ci == 5)
                    yield return Shot("13e_inspect_card_" + ci);
            }

            inspect.ReturnAll();
            yield return new WaitForSeconds(1.0f);

            // the storage game: all six containers into their compartments
            // container order: 0 flammable drum, 1 oxidizer, 2 acid, 3 cleaner canister, 4 reactive jar, 5 spray
            var compartment = new[] { 0, 1, 2, 3, 3, 3 };
            var placeOrder = new[] { 2, 0, 1, 3, 4, 5 };
            // no waiting for a walk to the cabinet in the test: the rig glides there after a short patience
            typeof(PresentableSequenceDirector).GetField("shelfPatience", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_director, 0.4f);
            var ghostT = placement.transform.Find("SlotGhost");
            for (var oi = 0; oi < placeOrder.Length; oi++)
            {
                var ci2 = placeOrder[oi];
                var tw = 0f;
                while (!hsPick.IsArmed && tw < 15f)
                {
                    tw += Time.unscaledDeltaTime;
                    yield return null;
                }

                hsPick.Activate();
                yield return new WaitForSeconds(0.3f);
                Poke(inspect.PokeOf(ci2));
                yield return new WaitForSeconds(3.0f); // voice-over starts, rig glides to the cabinet
                var headNow = Camera.main.transform.position;
                var stand = Get<Transform>(_director, "shelfStand").position;
                Say("container " + ci2 + " held = " + inspect.HeldName + ", distance to the cabinet stand = " +
                    Vector2.Distance(new Vector2(headNow.x, headNow.z), new Vector2(stand.x, stand.z)).ToString("0.00") + " m");
                if (oi == 0)
                    Say("eye-level board y = " + FindFirstObjectByType<CabinetRows>(FindObjectsInactive.Include).CurrentEyeBoardY.ToString("0.00") +
                        ", place voice-over = " + (Get<AudioSource>(_director, "voice").clip != null
                            ? Get<AudioSource>(_director, "voice").clip.name : "none"));
                if (oi == 0)
                    Say("details card visible at the cabinet = " + inspect.CardVisible + " (expected True)");

                if (oi == 0)
                {
                    // a wrong door first: red, the container stays in the hand
                    var wrongDoor = compartment[ci2] == 0 ? 1 : 0;
                    Poke(doors[wrongDoor].GetComponent<PokeTarget>());
                    yield return new WaitForSeconds(1.6f);
                    Say("wrong door state = " + doors[wrongDoor].State + ", still holding = " + inspect.HasHeld +
                        ", door open = " + doors[wrongDoor].IsOpen);
                    yield return Shot("12b_wrong_placement");
                    yield return new WaitForSeconds(2.6f);
                    Say("after the warning: door state = " + doors[wrongDoor].State + ", door open = " + doors[wrongDoor].IsOpen +
                        ", placed = " + placement.Placed);
                }

                Poke(doors[compartment[ci2]].GetComponent<PokeTarget>());
                yield return new WaitForSeconds(1.6f);
                Say("right door state = " + doors[compartment[ci2]].State + ", slot ghost visible = " +
                    (ghostT != null && ghostT.gameObject.activeSelf));
                if (oi == 0)
                    yield return Shot("12c_correct_placement");
                if (ghostT != null && ghostT.gameObject.activeSelf)
                    Poke(ghostT.GetComponent<PokeTarget>());
                var before = placement.Placed;
                tw = 0f;
                while (placement.Placed == before && tw < 10f)
                {
                    tw += Time.unscaledDeltaTime;
                    yield return null;
                }

                Say("placed " + placement.Placed + " / " + placement.Total);
                if (oi == 0 || oi == 5)
                    yield return Shot("12d_placed_" + (oi + 1));
            }

            var mcq2 = Get<PresentableMcqPanel>(_director, "mcqSc02");
            var t = 0f;
            while (!mcq2.IsOpen && t < 20f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(1.2f);
            LayoutCheck("SC02 MCQ", mcq2.transform);
            Say("mcq2 open = " + mcq2.IsOpen + " after " + placement.Placed + " / " + placement.Total + " placed");
            yield return Shot("14_sc02_question");
            Click(mcq2.transform, "Row2");
            var heard2 = "";
            var sawCorrect2 = false;
            var hw = 0f;
            var voiceNow = Get<AudioSource>(_director, "voice");
            while (hw < 12f)
            {
                if (voiceNow.clip != null && voiceNow.clip.name.Contains("Sc02Complete"))
                    heard2 = voiceNow.clip.name;
                if (voiceNow.clip != null && voiceNow.clip.name == "VO_Correct_bn")
                    sawCorrect2 = true;
                hw += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("scenario 2 complete vo = " + (heard2.Length > 0 ? heard2 : "NOT HEARD") + ", common correct-answer voice heard = " + sawCorrect2);
            yield return Shot("15_sc02_correct");
            LayoutCheck("SC02 MCQ correct card", mcq2.transform);
            Click(mcq2.transform, "NextButton");

            // --- Scenario 3: instruction card at eye level, inspect the old drum, immediate-action question
            yield return WaitStage("sc03Intro", 30f);
            var sc03 = Get<Sc03Inspect>(_director, "sc03Inspect");
            var det = Get<Sc03Detonation>(_director, "sc03Detonation");
            var mcq3 = Get<PresentableMcqPanel>(_director, "mcqSc03");
            var cont3 = FindDeep(panel.transform, "ContinueButton");
            var w3 = 0f;
            while (cont3 != null && !cont3.gameObject.activeSelf && w3 < 60f)
            {
                w3 += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.6f);
            var head3 = Camera.main.transform;
            var flat3 = Vector3.ProjectOnPlane(head3.forward, Vector3.up).normalized;
            Vector3 Rel(Vector3 world)
            {
                var d = world - head3.position;
                return new Vector3(Vector3.Dot(d, Vector3.Cross(Vector3.up, flat3)), d.y, Vector3.Dot(d, flat3));
            }

            var toCard = Rel(panel.transform.position);
            var toBtn = Rel(cont3.position);
            Say("SC03 card vs eye (level, upright frame): up=" + toCard.y.ToString("0.00") + " m, right=" +
                toCard.x.ToString("0.00") + " m, distance=" + toCard.z.ToString("0.00") + " m; Continue: up=" +
                toBtn.y.ToString("0.00") + ", right=" + toBtn.x.ToString("0.00") + " -> " +
                (Mathf.Abs(toCard.y) < 0.12f && Mathf.Abs(toCard.x) < 0.1f && Mathf.Abs(toBtn.x) < 0.12f &&
                 toBtn.y > -0.4f ? "OK" : "FAIL"));
            Say("SC03 instruction clip = " + (voiceSrc.clip != null ? voiceSrc.clip.name : "none"));
            yield return Shot("16_sc03_instruction");
            Click(panel.transform, "ContinueButton");

            yield return WaitStage("sc03Inspect", 30f);
            yield return new WaitForSeconds(2.2f);
            var clip3 = Get<SdsClipboardController>(_director, "clipboard");
            Say("SC03 SDS visible after Continue = " + clip3.IsVisible);
            clip3.Toggle();
            yield return new WaitForSeconds(0.6f);
            Say("SC03 SDS after X (hide) = " + clip3.IsVisible + " (expected False)");
            clip3.Toggle();
            yield return new WaitForSeconds(0.7f);
            Say("SC03 SDS after X (show) = " + clip3.IsVisible + " (expected True)");
            var labelAnchor3 = Get<Transform>(sc03, "labelAnchor");
            var camKeep3 = Camera.main.transform.localRotation;
            Camera.main.transform.rotation = Quaternion.LookRotation(labelAnchor3.position - Camera.main.transform.position);
            yield return Shot("17_sc03_drum_view");
            Say("STATS sc03 room: tris=" + UnityStats.triangles + " drawCalls=" + UnityStats.drawCalls + " setPass=" + UnityStats.setPassCalls);
            var armW = 0f;
            while (!sc03.ScanArmed && armW < 10f)
            {
                armW += Time.unscaledDeltaTime;
                yield return null;
            }

            var scanHand = GlovedHandPose.Right;
            Say("SC03 label scan armed = " + sc03.ScanArmed + " distance from the head to the label = " +
                Vector3.Distance(Camera.main.transform.position, labelAnchor3.position).ToString("0.00") + " m");
            yield return new WaitForSeconds(0.6f);
            yield return Shot("17b_sc03_scan_idle");
            // really hold a finger on the label: the right glove goes to the label plane (the drum stops it there)
            sc03.LabelScan.ForceHandMode();
            var ctlScan = scanHand.transform;
            var ctlKeep = ctlScan.position;
            ctlScan.position += labelAnchor3.position + labelAnchor3.forward * 0.06f - scanHand.PressPoint;
            yield return new WaitForSeconds(0.5f);
            Say("SC03 scan progress after 0.5 s of holding = " + sc03.LabelScan.Progress.ToString("0.00") + " (expected about 0.5)");
            yield return Shot("17c_sc03_scan_holding");
            // lifting the finger drains the ring
            ctlScan.position = ctlKeep;
            yield return new WaitForSeconds(0.4f);
            Say("SC03 scan progress after lifting the finger = " + sc03.LabelScan.Progress.ToString("0.00") + " (expected below 0.2)");
            ctlScan.position += labelAnchor3.position + labelAnchor3.forward * 0.06f - scanHand.PressPoint;
            var scanW = 0f;
            while (!sc03.Touched && scanW < 6f)
            {
                scanW += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("SC03 scan finished by holding the label = " + sc03.Touched);
            ctlScan.position = ctlKeep;
            var repW = 0f;
            while (!sc03.ReportVisible && repW < 15f)
            {
                repW += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(1.2f);
            Say("SC03 report visible = " + sc03.ReportVisible + ", clip = " + (voiceSrc.clip != null ? voiceSrc.clip.name : "none"));
            yield return Shot("18_sc03_report");
            Camera.main.transform.localRotation = camKeep3;
            Click(sc03.ReportCard, "ContinueButton");

            yield return WaitStage("sc03Question", 30f);
            var qW = 0f;
            while (!mcq3.IsOpen && qW < 10f)
            {
                qW += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(1.2f);
            Say("SC03 mcq open = " + mcq3.IsOpen);
            LayoutCheck("SC03 MCQ", mcq3.transform);
            yield return Shot("19_sc03_question");
            Click(mcq3.transform, "Row1");
            Camera.main.transform.rotation = Quaternion.LookRotation(labelAnchor3.position + Vector3.up * 0.25f - Camera.main.transform.position);
            yield return new WaitForSeconds(0.2f);
            yield return Shot("20a_blast_0");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("20b_blast_1");
            yield return new WaitForSeconds(1.2f);
            Say("SC03 wrong answer: detonation active = " + det.IsActive + ", clip = " +
                (voiceSrc.clip != null ? voiceSrc.clip.name : "none") + ", particles = " +
                det.GetComponentsInChildren<ParticleSystem>().Length);
            yield return Shot("20c_blast_2");
            yield return new WaitForSeconds(2.0f);
            yield return Shot("20d_blast_3");
            LayoutCheck("SC03 MCQ wrong card", mcq3.transform);
            Say("SC03 stats during the blast: tris=" + UnityStats.triangles + " drawCalls=" + UnityStats.drawCalls);
            Camera.main.transform.localRotation = camKeep3;
            Click(mcq3.transform, "Close");
            yield return new WaitForSeconds(0.8f);
            Say("SC03 after closing the red card: detonation active = " + det.IsActive + " (expected False)");
            Click(mcq3.transform, "Row3");
            yield return new WaitForSeconds(1.4f);
            Say("SC03 second wrong answer: detonation active = " + det.IsActive);
            Click(mcq3.transform, "Close");
            yield return new WaitForSeconds(0.8f);
            Click(mcq3.transform, "Row2");
            var heard3 = "";
            var sawCorrect3 = false;
            var h3 = 0f;
            while (h3 < 14f)
            {
                if (voiceSrc.clip != null && voiceSrc.clip.name.Contains("Sc03Complete"))
                    heard3 = voiceSrc.clip.name;
                if (voiceSrc.clip != null && voiceSrc.clip.name == "VO_Correct_bn")
                    sawCorrect3 = true;
                h3 += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("scenario 3 complete vo = " + (heard3.Length > 0 ? heard3 : "NOT HEARD") + ", common correct-answer voice heard = " + sawCorrect3);
            yield return Shot("21_sc03_correct");
            LayoutCheck("SC03 MCQ correct card", mcq3.transform);
            Click(mcq3.transform, "NextButton");

            // Scenario 4 is entered only after SC03 Next. The checks above are unchanged.
            yield return WaitStage("sc04Intro", 30f);
            var cont4 = FindDeep(panel.transform, "ContinueButton");
            var w4 = 0f;
            while (cont4 != null && !cont4.gameObject.activeSelf && w4 < 40f)
            {
                w4 += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.4f);
            Say("SC04 instruction clip = " + (voiceSrc.clip != null ? voiceSrc.clip.name : "none"));
            yield return Shot("29_sc04_instruction");
            Click(panel.transform, "ContinueButton");
            yield return WaitStage("sc04Explore", 20f);
            var gloves = Get<GloveInspect>(_director, "gloves");
            var clip4 = Get<SdsClipboardController>(_director, "clipboard");
            yield return new WaitForSeconds(0.4f);
            Say("SC04 SDS starts hidden = " + !clip4.IsVisible);
            yield return Shot("30_sc04_lab");
            clip4.Toggle();
            yield return new WaitForSeconds(0.9f);
            yield return Shot("31_sc04_sds_held");
            Say("SC04 SDS after X = " + clip4.IsVisible);
            clip4.Toggle();
            for (var g = 0; g < 3; g++)
            {
                var covers = gloves.MeshWithinReach(g);
                Say("SC04 glove " + g + " mesh in reach = " + covers);
                if (!covers)
                    Debug.LogError("[SMOKE] SC04 glove " + g + " collider does not cover the visible mesh");
                var poke = gloves.PokeOf(g);
                if (poke != null)
                    Poke(poke);
                var waitHold = 0f;
                while (!gloves.Holding && waitHold < 3f)
                {
                    waitHold += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return new WaitForSeconds(0.5f);
                Say("SC04 holding glove " + g + " = " + gloves.Holding);
                Click(gloves.transform, "CloseButton");
                var waitBack = 0f;
                while (gloves.Holding && waitBack < 3f)
                {
                    waitBack += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            var gateWait = 0f;
            var next4 = FindDeep(gloves.transform, "NextButton");
            while ((next4 == null || !next4.gameObject.activeInHierarchy) && gateWait < 8f)
            {
                gateWait += Time.unscaledDeltaTime;
                next4 = FindDeep(gloves.transform, "NextButton");
                yield return null;
            }

            Say("SC04 gloves seen = " + gloves.Seen + " / 3");
            Click(gloves.transform, "NextButton");
            yield return WaitStage("sc04Question", 20f);
            var mcq4 = Get<PresentableMcqPanel>(_director, "mcqSc04");
            var q4 = 0f;
            while (!mcq4.IsOpen && q4 < 8f)
            {
                q4 += Time.unscaledDeltaTime;
                yield return null;
            }

            LayoutCheck("SC04 MCQ", mcq4.transform);
            Click(mcq4.transform, "Row1");
            yield return new WaitForSeconds(0.8f);
            Click(mcq4.transform, "Close");
            yield return new WaitForSeconds(0.4f);
            Click(mcq4.transform, "Row3");
            var heard4 = "";
            var h4 = 0f;
            while (h4 < 12f)
            {
                if (voiceSrc.clip != null && voiceSrc.clip.name.Contains("Sc04Complete"))
                    heard4 = voiceSrc.clip.name;
                h4 += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("scenario 4 complete vo = " + (heard4.Length > 0 ? heard4 : "NOT HEARD"));
            Click(mcq4.transform, "NextButton");
            yield return WaitStage("complete", 30f);
            yield return new WaitForSeconds(1f);
            yield return Shot("12_complete");

            Say("DONE runtime errors/exceptions = " + _errors);
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
