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

        /// <summary>Dev start at Scenario 5: ray or finger path for SC05 only, then stop.</summary>
        private IEnumerator Sc05Smoke(InstructionPanelView panel, AudioSource voiceSrc)
        {
            yield return WaitStage("sc05Intro", 40f);
            if (PresentableInput.AllowedIn(5))
            {
                yield return Sc05RaySmoke(voiceSrc);
                yield break;
            }

            var amb = Get<Sc05Ambience>(_director, "sc05Ambience");
            yield return new WaitForSeconds(2.5f);
            yield return Shot("40_sc05_instruction");
            yield return ClickContinue(panel, 40f);
            yield return WaitStage("sc05Explore", 20f);
            var masks5 = Get<MaskInspect>(_director, "masks");
            yield return new WaitForSeconds(0.8f);
            yield return Shot("41_sc05_rail");
            for (var k = 0; k < masks5.Count; k++)
            {
                var idx = masks5.CurrentIndex;
                var pk = masks5.PokeOf(idx);
                if (pk != null)
                    Poke(pk);
                var wm = 0f;
                while (!masks5.Holding && wm < 3f)
                {
                    wm += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return new WaitForSeconds(0.7f);
                Say("SC05 holding " + masks5.HeldIndex + ": card = [" + masks5.CardName + "] / [" + masks5.CardCompat + "]");
                LayoutCheck("SC05 mask card " + idx, masks5.CardTransform);
                yield return Shot("42_sc05_mask_" + idx);
                Click(masks5.transform, "CloseButton");
                var wb = 0f;
                while (masks5.Holding && wb < 3f)
                {
                    wb += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return new WaitForSeconds(0.3f);
            }

            var gate5 = 0f;
            var next5 = FindDeep(masks5.transform, "NextButton");
            while ((next5 == null || !next5.gameObject.activeInHierarchy) && gate5 < 8f)
            {
                gate5 += Time.unscaledDeltaTime;
                next5 = FindDeep(masks5.transform, "NextButton");
                yield return null;
            }

            Say("SC05 masks seen = " + masks5.Seen + " / " + masks5.Count);
            Click(masks5.transform, "NextButton");
            yield return WaitStage("sc05Question", 20f);
            var mcq5 = Get<PresentableMcqPanel>(_director, "mcqSc05");
            var q5 = 0f;
            while (!mcq5.IsOpen && q5 < 8f)
            {
                q5 += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(2.4f);
            LayoutCheck("SC05 MCQ", mcq5.transform);
            Click(mcq5.transform, "Row2");
            var heard5 = "";
            var h5 = 0f;
            while (h5 < 14f)
            {
                if (voiceSrc.clip != null && voiceSrc.clip.name.Contains("Sc05Complete"))
                    heard5 = voiceSrc.clip.name;
                h5 += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("scenario 5 complete vo = " + (heard5.Length > 0 ? heard5 : "NOT HEARD"));
            Click(mcq5.transform, "NextButton");
        }

        // Scenario 5 (controller ray): masks show by themselves, gate, inline-reveal question.
        private IEnumerator Sc05RaySmoke(AudioSource voiceSrc)
        {
            Say("SC05 input mode = " + PresentableInput.Mode + ", ray active = " + PresentableInput.RayActive);
            var masks = Get<MaskInspect>(_director, "masks");
            var amb = Get<Sc05Ambience>(_director, "sc05Ambience");
            yield return new WaitForSeconds(2.2f);
            yield return Shot("40_sc05_instruction");
            var ip = FindFirstObjectByType<InstructionPanelView>(FindObjectsInactive.Include);
            if (ip != null)
                LayoutCheck("SC05 small instruction card", ip.transform);
            Say("SC05 machine sound on = " + amb.IsOn);
            yield return WaitStage("sc05Explore", 60f);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("41_sc05_rail");
            for (var k = 0; k < masks.Count; k++)
            {
                var t0 = 0f;
                while (!masks.AutoCardShown && t0 < 60f)
                {
                    t0 += Time.unscaledDeltaTime;
                    yield return null;
                }

                Say("SC05 ray: mask " + k + " card shown by itself = " + masks.AutoCardShown + " after " + t0.ToString("0.0") + " s");
                if (!masks.AutoCardShown)
                    Debug.LogError("[SMOKE] SC05 ray: mask " + k + " properties never showed up by themselves");
                yield return new WaitForSeconds(1.6f);
                Say("SC05 ray: holding " + masks.HeldIndex + ": card = [" + masks.CardName + "] / [" + masks.CardCompat + "], overlaps another mask = " +
                    masks.HeldOverlapsOthers() + " (expected False)");
                LayoutCheck("SC05 mask card " + k, masks.CardTransform);
                yield return Shot("42_sc05_mask_" + k);
                var t1 = 0f;
                while (masks.AutoCardShown && t1 < 40f)
                {
                    t1 += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            var g = 0f;
            while (!masks.GateVisible && g < 12f)
            {
                g += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.8f);
            Say("SC05 ray: masks seen = " + masks.Seen + " / " + masks.Count + ", gate visible = " + masks.GateVisible);
            LayoutCheck("SC05 gate card", masks.GateTransform);
            yield return Shot("43_sc05_gate");
            Click(masks.transform, "NextButton");
            yield return WaitStage("sc05Question", 20f);
            var mcq5 = Get<PresentableMcqPanel>(_director, "mcqSc05");
            var q = 0f;
            while (!mcq5.IsOpen && q < 8f)
            {
                q += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(2.8f);
            yield return Shot("44_sc05_question");
            LayoutCheck("SC05 MCQ", mcq5.transform);
            var cur = mcq5.Current;
            var wrong = false;
#if UNITY_EDITOR
            wrong = UnityEditor.EditorPrefs.GetBool("ChemSafetySc05Wrong", false);
#endif
            var target = wrong ? (cur.correctIndex + 1) % cur.options.Length : cur.correctIndex;
            Say("SC05 answer = " + (wrong ? "WRONG" : "CORRECT") + " inline reveal = " + cur.inlineReveal);
            var row = FindDeep(mcq5.transform, "Row" + (target + 1));
            if (row != null)
                row.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return new WaitForSeconds(2.2f);
            yield return Shot("45_sc05_reveal");
            var reasons = 0;
            foreach (var tmp in mcq5.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
            {
                if (tmp.name.StartsWith("Reason") && tmp.gameObject.activeInHierarchy && tmp.text.Length > 0)
                    reasons++;
            }

            Say("SC05 reveal: reasons shown = " + reasons + "/" + cur.options.Length + ", wrong popup open = " +
                (FindDeep(mcq5.transform, "WrongPopup") != null && FindDeep(mcq5.transform, "WrongPopup").gameObject.activeInHierarchy));
            LayoutCheck("SC05 MCQ reveal", mcq5.transform);
            Click(mcq5.transform, "NextButton");
        }

        // Scenario 6 (controller ray): automatic demonstration (hood, fan, containers, pour), PREVIEW replays it, NEXT opens the inline-reveal question.
        private IEnumerator Sc06RaySmoke(AudioSource voiceSrc)
        {
            Say("SC06 input mode = " + PresentableInput.Mode + ", ray active = " + PresentableInput.RayActive);
            var lab = Get<Sc06Lab>(_director, "lab");
            var auto = Get<Sc06AutoPour>(_director, "sc06Auto");
            var fan = Get<Sc06ExhaustFan>(_director, "sc06Fan");
            var gate = Get<Sc06Gate>(_director, "sc06Gate");
            var mcq6 = Get<PresentableMcqPanel>(_director, "mcqSc06");
            yield return WaitStage("sc06Intro", 60f);
            yield return new WaitForSeconds(2.2f);
            yield return Shot("60_sc06_instruction");
            var ip = FindFirstObjectByType<InstructionPanelView>(FindObjectsInactive.Include);
            if (ip != null)
                LayoutCheck("SC06 small instruction card", ip.transform);
            yield return WaitStage("sc06Auto", 60f);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("61_sc06_room");
            yield return WaitStage("sc06Hood", 30f);
            yield return new WaitForSeconds(1.4f);
            Say("SC06 auto: hood on = " + lab.HoodOn + " (expected True), fan on = " + lab.LevOn + " (expected False yet)");
            yield return Shot("62_sc06_hood_on");
            yield return WaitStage("sc06Fan", 30f);
            yield return new WaitForSeconds(3.2f);
            Say("SC06 auto: fan on = " + lab.LevOn + ", fan speed = " + (fan != null ? fan.Speed01.ToString("0.00") : "n/a") + " (expected > 0)");
            if (fan == null || fan.Speed01 < 0.05f)
                Debug.LogError("[SMOKE] SC06: the exhaust fan did not start turning");
            yield return Shot("63_sc06_fan_on");
            yield return WaitStage("sc06Move", 40f);
            var steps = "";
            var last = -1;
            var t = 0f;
            var shotMove = false;
            var shotPour = false;
            while (!auto.Finished && t < 90f)
            {
                t += Time.unscaledDeltaTime;
                if (auto.Step != last)
                {
                    last = auto.Step;
                    steps += last + " ";
                }

                if (!shotMove && auto.Step == 2)
                {
                    shotMove = true;
                    yield return new WaitForSeconds(1.0f);
                    yield return Shot("64_sc06_carry");
                }

                if (!shotPour && auto.Step == 4)
                {
                    shotPour = true;
                    yield return new WaitForSeconds(2.2f);
                    yield return Shot("65_sc06_pour");
                    Say("SC06 auto: jar level while pouring acid = " + auto.Level.ToString("0.00"));
                }

                yield return null;
            }

            Say("SC06 auto: steps seen in order = [" + steps.Trim() + "] (expected 1 2 3 4), finished = " + auto.Finished + ", jar level = " + auto.Level.ToString("0.00") +
                ", closest two containers in the hood = " + auto.MinSeparation.ToString("0.00") + " m (must be >= 0.3)");
            if (auto.MinSeparation < 0.3f)
                Debug.LogError("[SMOKE] SC06: two containers came closer than 0.3 m (they would overlap)");
            if (!auto.Finished)
                Debug.LogError("[SMOKE] SC06: the automatic demonstration never finished");
            var g = 0f;
            while (!gate.Visible && g < 20f)
            {
                g += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(1.0f);
            LayoutCheck("SC06 gate card", gate.Card);
            var label = FindDeep(gate.Card, "BackButton");
            var lab6 = label != null ? label.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            Say("SC06 gate: left button text = [" + (lab6 != null ? lab6.text : "?") + "] (expected PREVIEW), visible = " + gate.Visible);
            yield return Shot("66_sc06_gate");
            // PREVIEW: the whole animation plays again from the start
            Click(gate.Card, "BackButton");
            yield return new WaitForSeconds(2.0f);
            Say("SC06 preview: stage back at the start of the animation, hood on = " + lab.HoodOn + " (expected False right after the reset), jar level = " + auto.Level.ToString("0.00") + " (expected 0)");
            yield return WaitStage("sc06Gate", 160f);
            var g2 = 0f;
            while (!gate.Visible && g2 < 20f)
            {
                g2 += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.8f);
            Click(gate.Card, "NextButton");
            yield return WaitStage("sc06Question", 20f);
            var q = 0f;
            while (!mcq6.IsOpen && q < 8f)
            {
                q += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(2.8f);
            yield return Shot("67_sc06_question");
            LayoutCheck("SC06 MCQ", mcq6.transform);
            var cur = mcq6.Current;
            var wrong = false;
#if UNITY_EDITOR
            wrong = UnityEditor.EditorPrefs.GetBool("ChemSafetySc06Wrong", false);
#endif
            var target = wrong ? (cur.correctIndex + 1) % cur.options.Length : cur.correctIndex;
            Say("SC06 answer = " + (wrong ? "WRONG" : "CORRECT") + " inline reveal = " + cur.inlineReveal);
            var row = FindDeep(mcq6.transform, "Row" + (target + 1));
            if (row != null)
                row.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return new WaitForSeconds(2.2f);
            yield return Shot("68_sc06_reveal");
            var reasons = 0;
            foreach (var tmp in mcq6.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
            {
                if (tmp.name.StartsWith("Reason") && tmp.gameObject.activeInHierarchy && tmp.text.Length > 0)
                    reasons++;
            }

            Say("SC06 reveal: reasons shown = " + reasons + "/" + cur.options.Length);
            LayoutCheck("SC06 MCQ reveal", mcq6.transform);
            Click(mcq6.transform, "NextButton");
        }

        private IEnumerator Sc04RaySmoke(GloveInspect gloves)
        {
            Say("SC04 input mode = " + PresentableInput.Mode + ", ray active = " + PresentableInput.RayActive);
            for (var g = 0; g < gloves.Count; g++)
            {
                var armed = gloves.ArrowOf(g) != null && gloves.ArrowOf(g).IsArmed;
                if (armed)
                    Debug.LogError("[SMOKE] SC04 ray: glove " + g + " has a floating arrow (the properties must start by themselves)");
                var t0 = 0f;
                while (!gloves.AutoCardShown && t0 < 60f)
                {
                    t0 += Time.unscaledDeltaTime;
                    yield return null;
                }

                Say("SC04 ray: glove " + g + " card shown by itself = " + gloves.AutoCardShown + " after " + t0.ToString("0.0") + " s");
                if (!gloves.AutoCardShown)
                    Debug.LogError("[SMOKE] SC04 ray: glove " + g + " properties never showed up by themselves");
                yield return new WaitForSeconds(1.6f);
                var hb = gloves.GloveBounds(g);
                var upright = hb.size.y > hb.size.z;
                Say("SC04 ray: held glove " + g + " upright = " + upright + " size " + hb.size.ToString("F2"));
                if (!upright)
                    Debug.LogError("[SMOKE] SC04 ray: held glove " + g + " is not upright");
                var cardT = FindDeep(gloves.transform, "GloveCard");
                if (cardT != null)
                {
                    LayoutCheck("SC04 glove card " + g, cardT);
                    var cam = Camera.main.transform;
                    var gap = Vector3.Distance(cardT.position, hb.center);
                    Say("SC04 ray: card to glove centre distance = " + gap.ToString("0.00") + " m (wanted about 0.2 - 0.5)");
                }

                if (g == 2)
                    yield return Shot("31b_sc04_glove_card");
                var t1 = 0f;
                while (gloves.AutoCardShown && t1 < 40f)
                {
                    t1 += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            yield return WaitStage("sc04Gate", 40f);
            var gate4 = Get<Sc06Gate>(_director, "sc04Gate");
            var gateWait = 0f;
            while (!gate4.Visible && gateWait < 10f)
            {
                gateWait += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.8f);
            Say("SC04 ray: gloves seen = " + gloves.Seen + " / 3, gate visible = " + gate4.Visible);
            LayoutCheck("SC04 gate", gate4.Card);
            yield return Shot("32_sc04_gate");
            gate4.PressNext();
            yield return WaitStage("sc04Question", 20f);
            var mcq4 = Get<PresentableMcqPanel>(_director, "mcqSc04");
            var q4 = 0f;
            while (!mcq4.IsOpen && q4 < 8f)
            {
                q4 += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(2.8f);
            yield return Shot("33_sc04_question");
            LayoutCheck("SC04 MCQ", mcq4.transform);
            var cur = mcq4.Current;
            var wrong4 = false;
#if UNITY_EDITOR
            wrong4 = UnityEditor.EditorPrefs.GetBool("ChemSafetySc04Wrong", false);
#endif
            var target = wrong4 ? (cur.correctIndex + 1) % cur.options.Length : cur.correctIndex;
            Say("SC04 answer = " + (wrong4 ? "WRONG" : "CORRECT") + " inline reveal = " + cur.inlineReveal);
            var row = FindDeep(mcq4.transform, "Row" + (target + 1));
            if (row != null)
                row.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return new WaitForSeconds(2.2f);
            yield return Shot("34_sc04_reveal");
            var reasons = 0;
            foreach (var tmp in mcq4.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
            {
                if (tmp.name.StartsWith("Reason") && tmp.gameObject.activeInHierarchy && tmp.text.Length > 0)
                    reasons++;
            }

            Say("SC04 reveal: reasons shown = " + reasons + "/" + cur.options.Length + ", wrong popup open = " +
                (FindDeep(mcq4.transform, "WrongPopup") != null && FindDeep(mcq4.transform, "WrongPopup").gameObject.activeInHierarchy));
            LayoutCheck("SC04 MCQ reveal", mcq4.transform);
            Click(mcq4.transform, "NextButton");
        }

        private IEnumerator Sc03RaySmoke(PresentableMcqPanel mcq3, Sc03Detonation det)
        {
            var sc03 = Get<Sc03Inspect>(_director, "sc03Inspect");
            yield return WaitStage("sc03Inspect", 60f);
            yield return new WaitForSeconds(1.2f);
            var head = Camera.main.transform.position;
            var stand = Get<Transform>(_director, "sc03RayStand").position;
            var label = Get<Transform>(sc03, "labelAnchor").position;
            Say("SC03 stand: head " + Vector2.Distance(new Vector2(head.x, head.z), new Vector2(stand.x, stand.z)).ToString("0.00") +
                " m off the stand, " + Vector2.Distance(new Vector2(head.x, head.z), new Vector2(label.x, label.z)).ToString("0.00") + " m from the label");
            yield return Shot("sc03r_stand");
            yield return WaitStage("sc03Scan", 40f);
            yield return new WaitForSeconds(1.3f);
            yield return Shot("sc03r_scan");
            yield return WaitStage("sc03Report", 40f);
            yield return new WaitForSeconds(4.5f);
            Say("SC03 report visible = " + sc03.ReportVisible + " (no touch and hold, no Continue)");
            yield return Shot("sc03r_report");
            yield return WaitStage("sc03Question", 60f);
            var w = 0f;
            while (!mcq3.IsOpen && w < 25f)
            {
                w += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(2.8f);
            yield return Shot("sc03r_question");
            LayoutCheck("SC03 MCQ", mcq3.transform);
            var wrong = false;
#if UNITY_EDITOR
            wrong = UnityEditor.EditorPrefs.GetBool("ChemSafetySc03Wrong", false);
#endif
            var q = mcq3.Current;
            var target = wrong ? (q.correctIndex + 1) % q.options.Length : q.correctIndex;
            Say("SC03 answer = " + (wrong ? "WRONG" : "CORRECT") + " inline reveal = " + q.inlineReveal);
            var row = FindDeep(mcq3.transform, "Row" + (target + 1));
            if (row != null)
                row.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return new WaitForSeconds(2.0f);
            yield return Shot("sc03r_reveal");
            var reasons = 0;
            foreach (var tmp in mcq3.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
            {
                if (tmp.name.StartsWith("Reason") && tmp.gameObject.activeInHierarchy && tmp.text.Length > 0)
                    reasons++;
            }

            Say("SC03 reveal: reasons shown = " + reasons + "/4, wrong popup open = " +
                (FindDeep(mcq3.transform, "WrongPopup") != null && FindDeep(mcq3.transform, "WrongPopup").gameObject.activeInHierarchy));
            Say("SC03 blast running after the answer = " + det.IsActive + " (expected False)");
            var frame = 0;
            var nw = 0f;
            var nextOn = false;
            while (!nextOn && nw < 40f)
            {
                foreach (var tr in mcq3.GetComponentsInChildren<Transform>())
                    if (tr.name == "NextButton" && tr.gameObject.activeInHierarchy)
                        nextOn = true;
                if (!wrong && frame < 10 && nw >= frame * 1.3f)
                {
                    yield return Shot("sc03r_proto_" + frame);
                    frame++;
                }

                nw += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("SC03 NEXT visible = " + nextOn + " after " + nw.ToString("0.0") + " s" + (wrong ? " (no animation)" : " (after the protocol)"));
            yield return Shot("sc03r_next");
            Click(mcq3.transform, "NextButton");
            var att = ChemicalSafety.Results.SessionResults.Current != null && ChemicalSafety.Results.SessionResults.Current.attempts != null
                ? ChemicalSafety.Results.SessionResults.Current.attempts.Find(a => a.lesson_id == "SC03") : null;
            Say("SC03 marks: " + (att != null ? "score " + att.score + " / " + att.max_score + ", answers " + att.answers.Count : "NO ATTEMPT"));
        }

        // Scenario 2 (callout flow): stand, then six chemicals one after the other, each with a callout and its own question.
        // Question 2 is answered wrong on purpose (red cross + reasons), the others right; the last one ends with NEXT.
        private IEnumerator Sc02CalloutSmoke()
        {
            var mcq = Get<PresentableMcqPanel>(_director, "mcqSc02");
            var voiceSrc = Get<AudioSource>(_director, "voice");
            yield return WaitStage("sc02Stand", 90f);
            var head = Camera.main.transform.position;
            var stand = Get<Transform>(_director, "sc02RayStand").position;
            Say("SC02 stand: head " + Vector2.Distance(new Vector2(head.x, head.z), new Vector2(stand.x, stand.z)).ToString("0.00") + " m off the stand");
            yield return Shot("11_sc02_stand");
            for (var n = 1; n <= 6; n++)
            {
                yield return WaitStage("sc02Callout" + n, 120f);
                var w = 0f;
                while (!mcq.IsOpen && w < 25f)
                {
                    w += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return new WaitForSeconds(2.8f);
                yield return Shot("sc02_q" + n + "_open");
                LayoutCheck("SC02 Q" + n, mcq.transform);
                var wrong = n == 2;
                var q = mcq.Current;
                var target = wrong ? (q.correctIndex + 1) % q.options.Length : q.correctIndex;
                Say("SC02 Q" + n + " answer = " + (wrong ? "WRONG" : "CORRECT") + " prompt = " + q.prompt);
                var row = FindDeep(mcq.transform, "Row" + (target + 1));
                if (row != null)
                    row.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return new WaitForSeconds(2.2f);
                yield return Shot("sc02_q" + n + "_reveal");
                var reasons = 0;
                foreach (var tmp in mcq.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
                {
                    if (tmp.name.StartsWith("Reason") && tmp.gameObject.activeInHierarchy && tmp.text.Length > 0)
                        reasons++;
                }

                Say("SC02 Q" + n + " reveal: reasons shown = " + reasons + "/4");
                if (n == 6)
                {
                    var nw = 0f;
                    var nextOn = false;
                    while (!nextOn && nw < 30f)
                    {
                        foreach (var tr in mcq.GetComponentsInChildren<Transform>())
                            if (tr.name == "NextButton" && tr.gameObject.activeInHierarchy)
                                nextOn = true;
                        nw += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    Say("SC02 Q6: NEXT visible = " + nextOn + " after " + nw.ToString("0.0") + " s");
                    yield return Shot("sc02_q6_next");
                    Click(mcq.transform, "NextButton");
                }
                else
                {
                    yield return new WaitForSeconds(0.2f);
                }
            }

            var att = ChemicalSafety.Results.SessionResults.Current != null && ChemicalSafety.Results.SessionResults.Current.attempts != null
                ? ChemicalSafety.Results.SessionResults.Current.attempts.Find(a => a.lesson_id == "SC02") : null;
            Say("SC02 marks: " + (att != null ? "score " + att.score + " / " + att.max_score + ", answers " + att.answers.Count : "NO ATTEMPT"));
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
        private static int LayoutCheck(string where, Transform root, bool wholeCanvas = false)
        {
            // wholeCanvas: panels that are laid out edge to edge on their canvas (Scenarios 13-18) only have to stay inside the canvas
            var xLo = wholeCanvas ? 0.003f : 0.05f;
            var xHi = wholeCanvas ? 1.005f : 0.95f;
            var yLo = wholeCanvas ? 0.0f : 0.09f;
            var yHi = wholeCanvas ? 1.0f : 0.95f;
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
                var outside = mn.x < xLo || mx.x > xHi || mn.y < yLo || mx.y > yHi;
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

        // Scenarios 13-18: the supervisor must stay clear of the question panel (and the SDS panel) as the player sees them.
        private static void ScSupervisorClear(string where, ScCheckRunner runner)
        {
            var sup = Get<LabSupervisor>(runner, "supervisor");
            var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
            var canvas = runner.MainPanel != null ? runner.MainPanel.GetComponentInChildren<Canvas>(true) : null;
            if (sup == null || cam == null || canvas == null)
            {
                Say("supervisor clear check " + where + " skipped");
                return;
            }

            Rect Screen3(Vector3[] pts)
            {
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                foreach (var p in pts)
                {
                    var s = cam.WorldToViewportPoint(p);
                    x0 = Mathf.Min(x0, s.x);
                    y0 = Mathf.Min(y0, s.y);
                    x1 = Mathf.Max(x1, s.x);
                    y1 = Mathf.Max(y1, s.y);
                }

                return Rect.MinMaxRect(x0, y0, x1, y1);
            }

            var supPts = new System.Collections.Generic.List<Vector3>();
            foreach (var r in sup.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (r.name == "CoatLining")
                    continue;
                var b = r.bounds;
                for (var i = 0; i < 8; i++)
                    supPts.Add(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
            }

            var corners = new Vector3[4];
            ((RectTransform)canvas.transform).GetWorldCorners(corners);
            var panelRect = Screen3(corners);
            var supRect = Screen3(supPts.ToArray());
            var inter = supRect;
            inter.xMin = Mathf.Max(supRect.xMin, panelRect.xMin);
            inter.xMax = Mathf.Min(supRect.xMax, panelRect.xMax);
            inter.yMin = Mathf.Max(supRect.yMin, panelRect.yMin);
            inter.yMax = Mathf.Min(supRect.yMax, panelRect.yMax);
            var overlap = inter.width > 0f && inter.height > 0f ? inter.width * inter.height / Mathf.Max(0.0001f, supRect.width * supRect.height) : 0f;
            var visible = supRect.xMax > 0f && supRect.xMin < 1f;
            Say("supervisor vs question canvas " + where + ": overlap = " + (overlap * 100f).ToString("0") + "% (expected 0), in view = " + visible);
        }

        // Login: the keyboard must be chest-high and clear of the card, the footer fades while typing, both fingers may type, the rig is locked.
        private IEnumerator LoginErgoCheck(LoginScreen login)
        {
            var head = Get<Transform>(login, "_head");
            var kbT = Get<Transform>(login, "_kbT");
            var panelT = Get<Transform>(login, "_panelT");
            var cam = Camera.main;
            Say("login: left-hand poke enabled = " + FingerPokeSystem.LeftPokeEnabled + " (expected True), rig locked = " + (Get<Transform>(login, "_rig") != null) +
                " (expected True), seated mode = " + Get<bool>(login, "_seated"));
            var focus = typeof(LoginScreen).GetMethod("Focus", BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldType = typeof(LoginScreen).GetNestedType("Field", BindingFlags.NonPublic);
            focus.Invoke(login, new object[] { Enum.ToObject(fieldType, 1) });
            yield return new WaitForSeconds(1.3f);
            var d = kbT.position - head.position;
            var below = Mathf.Atan2(-d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            Say("login keyboard: centre " + below.ToString("0") + " deg below the eye line (expected <= 35), " + d.magnitude.ToString("0.00") + " m away");
            var chrome = Get<System.Collections.Generic.List<CanvasGroup>>(login, "_chrome");
            Say("login: footer (START, toggle, credits) faded while typing = " + (chrome.Count > 0 && chrome[0].alpha < 0.1f) + " (expected True)");
            if (cam != null)
            {
                Rect View(RectTransform rt)
                {
                    var c = new Vector3[4];
                    rt.GetWorldCorners(c);
                    float x0 = 9f, y0 = 9f, x1 = -9f, y1 = -9f;
                    foreach (var w in c)
                    {
                        var v = cam.WorldToViewportPoint(w);
                        x0 = Mathf.Min(x0, v.x); y0 = Mathf.Min(y0, v.y); x1 = Mathf.Max(x1, v.x); y1 = Mathf.Max(y1, v.y);
                    }

                    return Rect.MinMaxRect(x0, y0, x1, y1);
                }

                var contact = FindDeep(panelT, "ContactField") as RectTransform;
                var kbRect = View((RectTransform)kbT);
                if (contact != null)
                {
                    var cr = View(contact);
                    Say("login: keyboard top edge " + kbRect.yMax.ToString("0.00") + " vs contact field bottom " + cr.yMin.ToString("0.00") + " in the view (keyboard must stay below the field)");
                }

                Say("login: keyboard inside the view = " + (kbRect.xMin > -0.05f && kbRect.xMax < 1.05f && kbRect.yMin > -0.1f));
            }

            yield return Shot("00_login_focus");
            focus.Invoke(login, new object[] { Enum.ToObject(fieldType, 0) });
            yield return new WaitForSeconds(0.9f);
        }

        // Scenarios 13-18: a finger drag on the open SDS sheet must move it, like a phone.
        private static void ScDragScrollCheck(string where, ScCheckRunner runner)
        {
            var scroll = Get<UnityEngine.UI.ScrollRect>(runner, "_sdsScroll");
            if (scroll == null || scroll.content == null || scroll.viewport == null)
            {
                Say("SDS drag scroll " + where + " skipped");
                return;
            }

            var vp = scroll.viewport;
            var before = scroll.content.anchoredPosition.y;
            var range = Mathf.Max(0f, scroll.content.rect.height - vp.rect.height);
            var c = vp.rect.center;
            ScrollZone.Handle(vp.TransformPoint(new Vector3(c.x, c.y - 80f, 0f)), 0.03f);
            ScrollZone.Handle(vp.TransformPoint(new Vector3(c.x, c.y + 60f, 0f)), 0.03f); // finger moves up: the sheet follows
            ScrollZone.Handle(vp.TransformPoint(new Vector3(c.x, c.y, 0.5f / Mathf.Max(0.0001f, vp.lossyScale.z))), 0.03f); // lifted away
            var after = scroll.content.anchoredPosition.y;
            Say("SDS drag scroll " + where + ": scrollable range = " + range.ToString("0") + ", moved " + (after - before).ToString("0") + " px (expected > 0)");
            scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x, 0f);
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
            // the options are shuffled per session: RowK means "the option that is Kth in the unshuffled question"
            var panel = root.GetComponent<PresentableMcqPanel>();
            if (panel != null && panel.Current != null && panel.Current.sourceOrder != null && name.Length == 4 && name.StartsWith("Row") &&
                char.IsDigit(name[3]))
            {
                var slot = Array.IndexOf(panel.Current.sourceOrder, name[3] - '1');
                if (slot >= 0)
                    name = "Row" + (slot + 1);
            }

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

        // A real fingertip poke: the right glove tip goes into the target's volume and PokeTarget.Update must fire. Falls back to the
        // direct event when there is no glove (then it only proves the wiring, not the reach).
        private IEnumerator FingerPoke(PokeTarget p, string what)
        {
            if (p == null)
            {
                Say(what + ": no poke target (skipped)");
                yield break;
            }

            var hand = GlovedHandPose.Right;
            var fired = false;
            void On(PokeTarget t) => fired = true;
            p.Poked += On;
            var col = p.GetComponentInChildren<Collider>();
            if (hand != null && hand.HasTip && col != null)
            {
                var ctl = hand.transform;
                var target = col.bounds.center + Vector3.forward * 0.02f;
                // leave the volume first so the poke is a fresh entry (a finger already inside does not fire again)
                ctl.position += (target + Vector3.up * 0.4f) - hand.TipPosition;
                yield return null;
                yield return null;
                ctl.position += target - hand.TipPosition;
                var w = 0f;
                while (!fired && w < 1.2f)
                {
                    w += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            p.Poked -= On;
            if (!fired)
            {
                Say(what + ": the real fingertip did NOT fire the poke, using the direct event");
                Poke(p);
            }
            else
            {
                Say(what + ": real fingertip poke fired");
            }

            yield return new WaitForSeconds(0.3f);
        }

        private IEnumerator Sc06Round(string tag, Sc06Lab lab, Sc06Jug jug, Sc06Pour pour, bool shots)
        {
            yield return WaitStage("sc06Hood", 40f);
            yield return new WaitForSeconds(1.2f);
            Say("SC06 " + tag + ": hood on = " + lab.HoodOn + " (expected False), sash open = " + lab.SashOpen + ", vapor particles = " + lab.VaporParticles);
            if (shots)
            {
                yield return Shot("50_sc06_room");
                var camT = Camera.main.transform;
                var keep = camT.localRotation;
                camT.localRotation = Quaternion.Euler(10f, 22f, 0f);
                yield return Shot("51_sc06_panel");
                camT.localRotation = keep;
            }

            // a wrong order first: the sash must refuse to open while the hood is off
            var blocked = false;
            void OnBlocked() => blocked = true;
            lab.SashBlocked += OnBlocked;
            lab.SetSashInteractable(true);
            Poke(lab.SashPoke);
            yield return new WaitForSeconds(0.4f);
            lab.SashBlocked -= OnBlocked;
            lab.SetSashInteractable(false);
            Say("SC06 " + tag + ": sash refused while the hood is off = " + blocked + " (expected True), sash open = " + lab.SashOpen + " (expected False)");

            // LEV arm and general ventilation switches flip, the hood switch starts the airflow
            yield return FingerPoke(lab.SwitchPoke(1), "LEV switch");
            Say("SC06 " + tag + ": LEV on = " + lab.LevOn + " (expected True)");
            yield return FingerPoke(lab.SwitchPoke(1), "LEV switch back");
            yield return FingerPoke(lab.SwitchPoke(0), "fume hood switch");
            yield return new WaitForSeconds(1.8f);
            Say("SC06 " + tag + ": hood on = " + lab.HoodOn + ", air streak particles = " + lab.AirParticles + " (expected > 0), paper strip angle = " +
                lab.StripAngle.ToString("0") + " (expected about 30)");
            // lever down = ON (as at home), the lamp glows
            Say("SC06 " + tag + ": hood lever x angle = " + Mathf.DeltaAngle(0f, lab.Rocker(0).localEulerAngles.x).ToString("0") +
                " (expected +16 = down), LEV lever = " + Mathf.DeltaAngle(0f, lab.Rocker(1).localEulerAngles.x).ToString("0") +
                " (expected -16 = up), lamp glow hood = " + lab.LedGlow(0).ToString("0.00") + " / LEV = " + lab.LedGlow(1).ToString("0.00"));
            if (shots)
                yield return Shot("52_sc06_hood_on");

            // close the open container with its cap at the table: the vapor stops
            yield return WaitStage("sc06Cap", 20f);
            yield return new WaitForSeconds(2.8f);
            Say("SC06 " + tag + ": arrow on the cap = " + lab.ArrowVisible + ", vapor particles before closing = " + lab.VaporParticles + ", cap on = " + jug.CapIsOn + " (expected False)");
            if (shots)
                yield return Shot("52b_sc06_cap_step");
            yield return FingerPoke(jug.CapPoke, "loose cap");
            var wcap = 0f;
            while (!jug.CapIsOn && wcap < 6f)
            {
                wcap += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(5f);
            Say("SC06 " + tag + ": container closed = " + jug.CapIsOn + " (expected True), vapor particles 5 s later = " + lab.VaporParticles + " (expected < 6)");

            yield return WaitStage("sc06Jug", 20f);
            yield return new WaitForSeconds(1.0f);
            Say("SC06 " + tag + ": arrow visible = " + lab.ArrowVisible);
            yield return FingerPoke(jug.Poke, "solvent jug");
            var wj = 0f;
            while (!jug.Held && wj < 4f)
            {
                wj += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("SC06 " + tag + ": jug held = " + jug.Held);
            if (shots)
                yield return Shot("53_sc06_jug_held");
            yield return WaitStage("sc06Sash", 20f);
            yield return new WaitForSeconds(1.5f);
            lab.SetSashInteractable(true);
            yield return FingerPoke(lab.SashPoke, "glass sash");
            var ws = 0f;
            while (!lab.SashOpen && ws < 5f)
            {
                ws += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("SC06 " + tag + ": sash open = " + lab.SashOpen + ", air particles through the opening = " + lab.AirParticles);
            yield return WaitStage("sc06Pour", 20f);
            yield return new WaitForSeconds(0.8f);
            if (shots)
                yield return Shot("54_sc06_sash_open");
            yield return FingerPoke(pour.Poke, "glass jar");
            var wp = 0f;
            var sawStream = false;
            var capOffSeen = false;
            while (!pour.Done && wp < 30f)
            {
                if (pour.Pouring && !jug.CapIsOn)
                    capOffSeen = true;
                if (pour.StreamVisible && pour.Fill > 0.1f)
                    sawStream = true;
                if (shots && pour.Flow > 0.9f && pour.Fill > 0.3f && pour.Fill < 0.4f)
                {
                    yield return Shot("55_sc06_pour");
                }

                wp += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("SC06 " + tag + ": pour done = " + pour.Done + ", stream seen = " + sawStream + ", jar fill = " + pour.Fill.ToString("0.00") +
                ", cap was off while pouring = " + capOffSeen + " (expected True), cap on after = " + jug.CapIsOn + " (expected True), vapor particles after the cap = " + lab.VaporParticles);
        }

        private IEnumerator Sc06Smoke(InstructionPanelView panel, AudioSource voiceSrc, PresentableMcqPanel mcq5)
        {
            yield return WaitStage("sc06Intro", 40f);
            var lab = Get<Sc06Lab>(_director, "lab");
            var jug = Get<Sc06Jug>(_director, "sc06Jug");
            var pour = Get<Sc06Pour>(_director, "sc06Pour");
            var gate = Get<Sc06Gate>(_director, "sc06Gate");
            var mcq6 = Get<PresentableMcqPanel>(_director, "mcqSc06");
            yield return new WaitForSeconds(2.5f);
            yield return Shot("49b_sc06_instruction");
            var cont = FindDeep(panel.transform, "ContinueButton");
            var wc = 0f;
            while (cont != null && !cont.gameObject.activeSelf && wc < 40f)
            {
                wc += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.4f);
            Click(panel.transform, "ContinueButton");

            yield return Sc06Round("round 1", lab, jug, pour, true);
            yield return WaitStage("sc06Gate", 40f);
            var wg = 0f;
            while (!gate.Visible && wg < 15f)
            {
                wg += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(1.5f);
            Say("SC06 gate: head z = " + Camera.main.transform.position.z.ToString("0.00") + " (expected about 1.35: back from the table, whose front edge is at 0.13)");
            Say("SC06 gate card visible = " + gate.Visible + ", sash closed again = " + !lab.SashOpen + ", hood still on = " + lab.HoodOn);
            LayoutCheck("SC06 gate card", gate.Card);
            yield return Shot("56_sc06_gate");
            // BACK runs the inspection again from the start
            Click(gate.transform, "BackButton");
            yield return Sc06Round("round 2 (after BACK)", lab, jug, pour, false);
            yield return WaitStage("sc06Gate", 40f);
            var wg2 = 0f;
            while (!gate.Visible && wg2 < 15f)
            {
                wg2 += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(1.2f);
            Click(gate.transform, "NextButton");

            yield return WaitStage("sc06Question", 20f);
            var q = 0f;
            while (!mcq6.IsOpen && q < 8f)
            {
                q += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(2.4f);
            Say("SC06 MCQ wow look = " + mcq6.Fancy + " (expected True), SC05 MCQ wow look = " + mcq5.Fancy + " (expected True), chevrons = " +
                (mcq6.Fx != null ? mcq6.Fx.ChevronCount : 0) + ", comet pieces = " + (mcq6.Fx != null ? mcq6.Fx.CometCount : 0) +
                ", rows landed = " + (mcq6.Fx != null && mcq6.Fx.RowsLanded));
            LayoutCheck("SC06 MCQ", mcq6.transform);
            yield return Shot("57_sc06_question");
            Click(mcq6.transform, "Row1");
            yield return new WaitForSeconds(1.6f);
            yield return Shot("58_sc06_wrong");
            Click(mcq6.transform, "Close");
            yield return new WaitForSeconds(1.2f);
            Click(mcq6.transform, "Row3");
            var heard = "";
            var h = 0f;
            while (h < 14f)
            {
                if (voiceSrc.clip != null && voiceSrc.clip.name.Contains("Sc06Complete"))
                    heard = voiceSrc.clip.name;
                if (h > 1.2f && h < 1.3f)
                    LayoutCheck("SC06 MCQ correct card", mcq6.transform);
                h += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("scenario 6 complete vo = " + (heard.Length > 0 ? heard : "NOT HEARD"));
            yield return Shot("59_sc06_correct");
            Click(mcq6.transform, "NextButton");
        }

        // Scenario 7: instruction, the fire event (workers walk in, flee), SDS, the three extinguishers by real fingertip, the gate (BACK once,
        // then NEXT), the MCQ (wrong, then right), the hero puts the fire out, completion voice.
        private IEnumerator Sc07Smoke(InstructionPanelView panel, AudioSource voiceSrc)
        {
            yield return WaitStage("sc07Intro", 40f);
            var ext = Get<ExtinguisherInspect>(_director, "extinguishers");
            var gate = Get<Sc06Gate>(_director, "sc07Gate");
            var mcq7 = Get<PresentableMcqPanel>(_director, "mcqSc07");
            var fire = Get<Sc07Fire>(_director, "sc07Fire");
            var crowd = Get<Sc07Crowd>(_director, "sc07Crowd");
            var clip7 = Get<SdsClipboardController>(_director, "clipboard");
            yield return new WaitForSeconds(2.5f);
            Say("SC07 before the event: fire intensity = " + fire.Intensity.ToString("0.00") + " (expected 0), hero active = " + crowd.Hero.gameObject.activeSelf + " (expected False)");
            yield return Shot("60_sc07_instruction");
            var cont = FindDeep(panel.transform, "ContinueButton");
            var wc = 0f;
            while (cont != null && !cont.gameObject.activeSelf && wc < 40f)
            {
                wc += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.4f);
            Click(panel.transform, "ContinueButton");

            // the event: fire catches, alarm, workers walk in, startle, run; the hero stays
            yield return WaitStage("sc07Event", 20f);
            var shots = 0;
            var te = 0f;
            while (_director.CurrentStage == "sc07Event" && te < 90f)
            {
                te += Time.unscaledDeltaTime;
                if (te > 2.5f * (shots + 1) && shots < 14)
                {
                    shots++;
                    var active = 0;
                    for (var i = 0; i < crowd.Count; i++)
                        if (crowd.Hero != null && crowd.gameObject != null)
                            active++;
                    yield return Shot("61_sc07_event_" + shots);
                    Say("SC07 event t=" + te.ToString("0.0") + " fire = " + fire.Intensity.ToString("0.00"));
                }

                yield return null;
            }

            Say("SC07 event over after " + te.ToString("0.0") + " s, fire = " + fire.Intensity.ToString("0.00") + " (expected 1), hero active = " + crowd.Hero.gameObject.activeSelf +
                " (expected True), hero speed = " + crowd.Hero.Speed.ToString("0.00"));

            Say("SC07 fire exit door open after the flight = " + crowd.FleeDoorOpen + " (expected True), people = " + crowd.Count);
            yield return WaitStage("sc07Sds", 20f);
            yield return new WaitForSeconds(1.2f);
            Say("SC07 SDS shown = " + clip7.IsVisible + " (expected True)");
            yield return Shot("64_sc07_sds");
            clip7.Toggle();
            yield return WaitStage("sc07Explore", 20f);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("65_sc07_pillar");
            for (var round = 0; round < 2; round++)
            {
                for (var g = 0; g < 3; g++)
                {
                    var covers = ext.MeshWithinReach(g);
                    Say("SC07 extinguisher " + g + " mesh in reach = " + covers);
                    if (!covers)
                        Debug.LogError("[SMOKE] SC07 extinguisher " + g + " collider does not cover the visible mesh");
                    yield return FingerPoke(ext.PokeOf(g), "extinguisher " + g);
                    var wh = 0f;
                    while (!ext.Holding && wh < 3f)
                    {
                        wh += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    yield return new WaitForSeconds(0.9f);
                    Say("SC07 holding " + g + " = " + ext.Holding + ", card = '" + ext.CardTitle + "' / '" + ext.CardName + "' / '" + ext.CardUse + "', class badge = " + ext.BadgeVisible);
                    var cardT = ext.CardTransform;
                    if (cardT != null)
                        LayoutCheck("SC07 card " + g, cardT);
                    if (round == 0)
                        yield return Shot("66_sc07_card_" + g);
                    Click(ext.transform, "CloseButton");
                    var wb = 0f;
                    while (ext.Holding && wb < 3f)
                    {
                        wb += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }

                yield return WaitStage("sc07Gate", 20f);
                var wg = 0f;
                while (!gate.Visible && wg < 10f)
                {
                    wg += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return new WaitForSeconds(1.2f);
                if (round == 0)
                {
                    LayoutCheck("SC07 gate", gate.Card);
                    yield return Shot("69_sc07_gate");
                    Click(gate.transform, "BackButton");
                    yield return WaitStage("sc07Explore", 10f);
                    yield return new WaitForSeconds(0.6f);
                }
                else
                {
                    Click(gate.transform, "NextButton");
                }
            }

            yield return WaitStage("sc07Question", 20f);
            var q = 0f;
            while (!mcq7.IsOpen && q < 8f)
            {
                q += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(2.4f);
            Say("SC07 MCQ wow look = " + mcq7.Fancy + " (expected True)");
            var camM = Camera.main.transform;
            var toPanel = mcq7.transform.position - camM.position;
            var flatFwd = Vector3.ProjectOnPlane(camM.forward, Vector3.up).normalized;
            var lateral = Vector3.Dot(toPanel, Vector3.Cross(Vector3.up, flatFwd));
            Say("SC07 MCQ placement: distance ahead = " + Vector3.Dot(toPanel, flatFwd).ToString("0.00") + " m (expected 0.70), sideways = " + lateral.ToString("0.00") +
                " m (expected 0), height above the eyes = " + toPanel.y.ToString("0.00") + " m");
            LayoutCheck("SC07 MCQ", mcq7.transform);
            yield return Shot("70_sc07_question");
            Click(mcq7.transform, "Row4"); // foam: wrong
            yield return new WaitForSeconds(1.6f);
            yield return Shot("71_sc07_wrong");
            Click(mcq7.transform, "Close");
            yield return new WaitForSeconds(1.2f);
            Click(mcq7.transform, "Row2"); // CO2: right
            yield return new WaitForSeconds(1.8f);
            yield return Shot("72_sc07_right");
            LayoutCheck("SC07 MCQ right card", mcq7.transform);
            yield return WaitStage("sc07Extinguish", 30f);
            var tx = 0f;
            var sh = 0;
            var minFire = 1f;
            while (_director.CurrentStage == "sc07Extinguish" && tx < 60f)
            {
                tx += Time.unscaledDeltaTime;
                minFire = Mathf.Min(minFire, fire.Intensity);
                if (tx > 2.5f * (sh + 1) && sh < 9)
                {
                    sh++;
                    yield return Shot("73_sc07_hero_" + sh);
                    Say("SC07 extinguishing t=" + tx.ToString("0.0") + " fire = " + fire.Intensity.ToString("0.00") + " hero holds extinguisher = " + crowd.HeroHoldsExtinguisher +
                        " jet = " + (fire.Spray != null && fire.Spray.isPlaying) + " horn separated from the body = " + crowd.HornSeparation.ToString("0.00") + " m (in hand = " + crowd.HornInHand + ")");
                }

                if (voiceSrc.clip != null && voiceSrc.clip.name.Contains("Sc07Complete"))
                    break;
                yield return null;
            }

            Say("SC07 fire out = " + (fire.Intensity < 0.05f) + " (expected True), lowest intensity = " + minFire.ToString("0.00"));
            var heard = "";
            var h = 0f;
            while (h < 16f)
            {
                if (voiceSrc.clip != null && voiceSrc.clip.name.Contains("Sc07Complete"))
                    heard = voiceSrc.clip.name;
                if (heard.Length > 0 && h > 1f)
                    break;
                h += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("scenario 7 complete vo = " + (heard.Length > 0 ? heard : "NOT HEARD"));
            yield return new WaitForSeconds(2.5f);
            yield return Shot("74_sc07_done");
            Click(mcq7.transform, "NextButton");
        }

        // Scenario 7 on the controller ray: small side card + Continue, the fire event, the player is walked to the pillar, the SDS shows 2 s and
        // hides, the three extinguishers come one by one, the gate (BACK = SDS and all three again, then NEXT), the MCQ with tick / cross and a
        // reason under every option, the hero puts the fire out on a right answer (EditorPref ChemSafetySc07Wrong = answer wrong: no animation).
        private IEnumerator Sc07RaySmoke(InstructionPanelView panel, AudioSource voiceSrc)
        {
            Say("SC07 input mode = " + PresentableInput.Mode + ", ray active = " + PresentableInput.RayActive);
            var ext = Get<ExtinguisherInspect>(_director, "extinguishers");
            var gate = Get<Sc06Gate>(_director, "sc07Gate");
            var mcq7 = Get<PresentableMcqPanel>(_director, "mcqSc07");
            var fire = Get<Sc07Fire>(_director, "sc07Fire");
            var crowd = Get<Sc07Crowd>(_director, "sc07Crowd");
            var clip7 = Get<SdsClipboardController>(_director, "clipboard");
            yield return WaitStage("sc07Intro", 60f);
            yield return new WaitForSeconds(2.5f);
            Say("SC07 before the event: fire intensity = " + fire.Intensity.ToString("0.00") + " (expected 0), hero active = " + crowd.Hero.gameObject.activeSelf + " (expected False)");
            yield return Shot("60_sc07_instruction");
            LayoutCheck("SC07 small instruction card", panel.transform);
            var cont = FindDeep(panel.transform, "ContinueButton");
            var wc = 0f;
            while (cont != null && !cont.gameObject.activeSelf && wc < 40f)
            {
                wc += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.4f);
            Click(panel.transform, "ContinueButton");
            yield return WaitStage("sc07Event", 20f);
            var shots = 0;
            var te = 0f;
            while (_director.CurrentStage == "sc07Event" && te < 90f)
            {
                te += Time.unscaledDeltaTime;
                if (te > 4f * (shots + 1) && shots < 6)
                {
                    shots++;
                    yield return Shot("61_sc07_event_" + shots);
                    Say("SC07 event t=" + te.ToString("0.0") + " fire = " + fire.Intensity.ToString("0.00"));
                }

                yield return null;
            }

            Say("SC07 event over after " + te.ToString("0.0") + " s, fire = " + fire.Intensity.ToString("0.00") + " (expected 1), hero active = " + crowd.Hero.gameObject.activeSelf +
                " (expected True), fire exit door open = " + crowd.FleeDoorOpen + " (expected True)");
            var sw = Camera.main.transform.position;
            yield return WaitStage("sc07Walk", 20f);
            yield return WaitStage("sc07Sds", 30f);
            var moved = Vector3.Distance(sw, Camera.main.transform.position);
            Say("SC07 auto walk to the extinguisher pillar: moved " + moved.ToString("0.0") + " m (expected > 2)");
            if (moved < 2f)
                Debug.LogError("[SMOKE] SC07: the player was not walked to the extinguisher pillar");
            yield return new WaitForSeconds(0.7f);
            Say("SC07 SDS shown by itself = " + clip7.IsVisible + " (expected True)");
            yield return Shot("64_sc07_sds");
            yield return new WaitForSeconds(2.2f);
            Say("SC07 SDS hid by itself after about 2 s = " + (!clip7.IsVisible) + " (expected True)");
            if (clip7.IsVisible)
                Debug.LogError("[SMOKE] SC07: the SDS did not hide by itself");
            yield return WaitStage("sc07Explore", 20f);
            for (var round = 0; round < 2; round++)
            {
                var order = "";
                var lastHeld = -2;
                var tt = 0f;
                var cardShots = 0;
                while (_director.CurrentStage != "sc07Gate" && tt < 120f)
                {
                    tt += Time.unscaledDeltaTime;
                    if (ext.Holding && ext.HeldIndex != lastHeld)
                    {
                        lastHeld = ext.HeldIndex;
                        order += lastHeld + " ";
                        yield return new WaitForSeconds(1.0f);
                        Say("SC07 extinguisher " + lastHeld + " came by itself, card = '" + ext.CardTitle + "' / '" + ext.CardName + "' / '" + ext.CardUse + "', badge = " + ext.BadgeVisible);
                        if (ext.CardTransform != null)
                            LayoutCheck("SC07 card " + lastHeld, ext.CardTransform);
                        if (round == 0 && cardShots < 3)
                        {
                            cardShots++;
                            yield return Shot("66_sc07_card_" + lastHeld);
                        }
                    }

                    if (!ext.Holding)
                        lastHeld = -2;
                    yield return null;
                }

                Say("SC07 round " + round + ": extinguishers in the order they came = [" + order.Trim() + "] (expected 3 of them, left to right), seen = " + ext.Seen);
                var wg = 0f;
                while (!gate.Visible && wg < 10f)
                {
                    wg += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return new WaitForSeconds(1.0f);
                if (round == 0)
                {
                    LayoutCheck("SC07 gate", gate.Card);
                    yield return Shot("69_sc07_gate");
                    Click(gate.transform, "BackButton");
                    yield return WaitStage("sc07Sds", 15f);
                    Say("SC07 BACK: the SDS shows again from the start of the inspection");
                    yield return WaitStage("sc07Explore", 20f);
                }
                else
                {
                    Click(gate.transform, "NextButton");
                }
            }

            yield return WaitStage("sc07Question", 20f);
            var q = 0f;
            while (!mcq7.IsOpen && q < 8f)
            {
                q += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(2.8f);
            var camM = Camera.main.transform;
            var toPanel = mcq7.transform.position - camM.position;
            var flatFwd = Vector3.ProjectOnPlane(camM.forward, Vector3.up).normalized;
            Say("SC07 MCQ placement: distance ahead = " + Vector3.Dot(toPanel, flatFwd).ToString("0.00") + " m, sideways = " + Vector3.Dot(toPanel, Vector3.Cross(Vector3.up, flatFwd)).ToString("0.00") +
                " m, height above the eyes = " + toPanel.y.ToString("0.00") + " m");
            yield return Shot("70_sc07_question");
            LayoutCheck("SC07 MCQ", mcq7.transform);
            var cur = mcq7.Current;
            var wrong = false;
#if UNITY_EDITOR
            wrong = UnityEditor.EditorPrefs.GetBool("ChemSafetySc07Wrong", false);
#endif
            var target = wrong ? (cur.correctIndex + 1) % cur.options.Length : cur.correctIndex;
            Say("SC07 answer = " + (wrong ? "WRONG" : "CORRECT") + " inline reveal = " + cur.inlineReveal + " fancy = " + mcq7.Fancy);
            var row = FindDeep(mcq7.transform, "Row" + (target + 1));
            if (row != null)
                row.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            if (!wrong)
            {
                yield return WaitStage("sc07Extinguish", 30f);
                var tx = 0f;
                var sh = 0;
                var minFire = 1f;
                while (_director.CurrentStage == "sc07Extinguish" && tx < 70f)
                {
                    tx += Time.unscaledDeltaTime;
                    minFire = Mathf.Min(minFire, fire.Intensity);
                    if (tx > 3f * (sh + 1) && sh < 8)
                    {
                        sh++;
                        yield return Shot("73_sc07_hero_" + sh);
                        Say("SC07 extinguishing t=" + tx.ToString("0.0") + " fire = " + fire.Intensity.ToString("0.00") + " hero holds extinguisher = " + crowd.HeroHoldsExtinguisher);
                    }

                    yield return null;
                }

                Say("SC07 fire out = " + (fire.Intensity < 0.05f) + " (expected True), lowest intensity = " + minFire.ToString("0.00"));
            }
            else
            {
                Say("SC07 wrong answer: the hero animation is not played (stage = " + _director.CurrentStage + ")");
                if (_director.CurrentStage == "sc07Extinguish")
                    Debug.LogError("[SMOKE] SC07: the extinguish animation played after a wrong answer");
            }

            yield return new WaitForSeconds(2.2f);
            yield return Shot("72_sc07_reveal");
            var reasons = 0;
            foreach (var tmp in mcq7.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
                if (tmp.name.StartsWith("Reason") && tmp.gameObject.activeInHierarchy && tmp.text.Length > 0)
                    reasons++;
            Say("SC07 reveal: reasons shown = " + reasons + "/" + cur.options.Length);
            LayoutCheck("SC07 MCQ reveal", mcq7.transform);
            Click(mcq7.transform, "NextButton");
        }

        private IEnumerator Sc07Dispatch(InstructionPanelView panel, AudioSource voiceSrc)
        {
            if (PresentableInput.AllowedIn(7) && Get<ExtinguisherInspect>(_director, "extinguishers") != null)
                yield return Sc07RaySmoke(panel, voiceSrc);
            else
                yield return Sc07Smoke(panel, voiceSrc);
        }

        private static void Poke(PokeTarget p)
        {
            typeof(PokeTarget).GetMethod("Fire", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(p, new object[] { false });
        }

        // The fingertip must rest on a panel instead of sinking into it, and still press the button.
        // Scenario 8: instruction, PPE station (four items by real fingertip), fume hood switch, tray, sash, mix (acid into water, stirring),
        // the gate (BACK once, then NEXT), the MCQ (wrong, then right), completion voice.
        // Scenario 8 (controller ray, in the Scenario 6 factory room): PPE station ticks by itself, hood, fan, containers, water first,
        // glass rod, acid slowly; PREVIEW replays it; NEXT opens the inline-reveal question with the client's four options.
        private IEnumerator Sc08RaySmoke(AudioSource voiceSrc)
        {
            Say("SC08 input mode = " + PresentableInput.Mode + ", ray active = " + PresentableInput.RayActive + ", scenario = " + PresentableInput.ActiveScenario);
            var lab = Get<Sc06Lab>(_director, "lab");
            var fan = Get<Sc06ExhaustFan>(_director, "sc06Fan");
            var dil = Get<Sc08AutoDilute>(_director, "sc08Dilute");
            var ppe = Get<Sc08Ppe>(_director, "sc08PpeRay");
            var gate = Get<Sc06Gate>(_director, "sc08GateRay");
            var mcq8 = Get<PresentableMcqPanel>(_director, "mcqSc08");
            yield return WaitStage("sc08Intro", 80f);
            yield return new WaitForSeconds(2.2f);
            yield return Shot("90_sc08_instruction");
            var ip = FindFirstObjectByType<InstructionPanelView>(FindObjectsInactive.Include);
            if (ip != null)
                LayoutCheck("SC08 small instruction card", ip.transform);
            yield return WaitStage("sc08Auto", 60f);
            yield return WaitStage("sc08Ppe", 30f);
            var order = "";
            var lastTaken = 0;
            var t0 = 0f;
            while (!ppe.AllDone && t0 < 90f)
            {
                t0 += Time.unscaledDeltaTime;
                if (ppe.Taken != lastTaken)
                {
                    lastTaken = ppe.Taken;
                    order += lastTaken + "(checks:";
                    for (var i = 0; i < ppe.Count; i++)
                        order += ppe.CheckVisible(i) ? "1" : "0";
                    order += ") ";
                    if (lastTaken == 2)
                    {
                        yield return Shot("91_sc08_ppe_half");
                        // the head turns left to the board on the left wall (a screenshot only; the rig is turned back afterwards)
                        var camT = Camera.main.transform;
                        var rigT = camT.parent != null ? camT.parent : camT;
                        rigT.RotateAround(camT.position, Vector3.up, -70f);
                        yield return new WaitForSeconds(0.6f);
                        yield return Shot("91b_sc08_ppe_board");
                        rigT.RotateAround(camT.position, Vector3.up, 70f);
                    }
                }

                yield return null;
            }

            Say("SC08 PPE: taken one by one = " + order.Trim() + ", all done = " + ppe.AllDone + ", panel open = " + ppe.PanelOpen);
            if (!ppe.AllDone)
                Debug.LogError("[SMOKE] SC08: the PPE items were not all taken automatically");
            yield return WaitStage("sc08Hood", 30f);
            yield return new WaitForSeconds(1.4f);
            Say("SC08 auto: hood on = " + lab.HoodOn + " (expected True)");
            yield return WaitStage("sc08Fan", 30f);
            yield return new WaitForSeconds(3.2f);
            Say("SC08 auto: fan on = " + lab.LevOn + ", fan speed = " + (fan != null ? fan.Speed01.ToString("0.00") : "n/a") + " (expected > 0)");
            if (fan == null || fan.Speed01 < 0.05f)
                Debug.LogError("[SMOKE] SC08: the exhaust fan did not start turning");
            yield return WaitStage("sc08Move", 40f);
            var steps = "";
            var last = -1;
            var t = 0f;
            var shotWater = false;
            var shotAcid = false;
            var levelAfterWater = -1f;
            while (!dil.Finished && t < 120f)
            {
                t += Time.unscaledDeltaTime;
                if (dil.Step != last)
                {
                    last = dil.Step;
                    steps += last + " ";
                    if (last == 5)
                        levelAfterWater = dil.Level;
                }

                if (!shotWater && dil.Step == 4)
                {
                    shotWater = true;
                    yield return new WaitForSeconds(2.0f);
                    yield return Shot("92_sc08_water");
                    Say("SC08 pour view: head z = " + Camera.main.transform.position.z.ToString("0.00") + " (expected well behind the table, near the hood, z < 0)");
                }

                if (!shotAcid && dil.Step == 6)
                {
                    shotAcid = true;
                    yield return new WaitForSeconds(3.0f);
                    yield return Shot("93_sc08_acid_stir");
                    Say("SC08 stirring = " + dil.Stirring + " (expected True), rod active = " + (dil.Rod != null && dil.Rod.gameObject.activeInHierarchy));
                }

                yield return null;
            }

            Say("SC08 auto: steps seen in order = [" + steps.Trim() + "] (expected 1 2 3 4 5 6), water level before the acid = " + levelAfterWater.ToString("0.00") +
                " (expected ~0.55), finished = " + dil.Finished + ", final level = " + dil.Level.ToString("0.00") + ", closest two containers = " +
                dil.MinSeparation.ToString("0.00") + " m (must be >= 0.3)");
            if (dil.MinSeparation < 0.3f)
                Debug.LogError("[SMOKE] SC08: two containers came closer than 0.3 m");
            if (!dil.Finished)
                Debug.LogError("[SMOKE] SC08: the automatic dilution never finished");
            var g = 0f;
            while (!gate.Visible && g < 20f)
            {
                g += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(1.0f);
            Say("SC08 gate view: head z = " + Camera.main.transform.position.z.ToString("0.00") + " (expected back at the start spot, about 3.0)");
            LayoutCheck("SC08 gate card", gate.Card);
            var label = FindDeep(gate.Card, "BackButton");
            var lab8 = label != null ? label.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            Say("SC08 gate: left button text = [" + (lab8 != null ? lab8.text : "?") + "] (expected PREVIEW), visible = " + gate.Visible);
            yield return Shot("94_sc08_gate");
            Click(gate.Card, "BackButton");
            yield return new WaitForSeconds(2.0f);
            Say("SC08 preview: back at the start, hood on = " + lab.HoodOn + " (expected False), jar level = " + dil.Level.ToString("0.00") + " (expected 0)");
            yield return WaitStage("sc08Gate", 220f);
            var g2 = 0f;
            while (!gate.Visible && g2 < 20f)
            {
                g2 += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.8f);
            Click(gate.Card, "NextButton");
            yield return WaitStage("sc08Question", 20f);
            var q = 0f;
            while (!mcq8.IsOpen && q < 8f)
            {
                q += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(2.8f);
            yield return Shot("95_sc08_question");
            LayoutCheck("SC08 MCQ", mcq8.transform);
            var cur = mcq8.Current;
            Say("SC08 question: options = " + cur.options.Length + ", correct index = " + cur.correctIndex + " (expected 2), inline reveal = " + cur.inlineReveal);
            var wrong = false;
#if UNITY_EDITOR
            wrong = UnityEditor.EditorPrefs.GetBool("ChemSafetySc08Wrong", false);
#endif
            var target = wrong ? (cur.correctIndex + 1) % cur.options.Length : cur.correctIndex;
            var row = FindDeep(mcq8.transform, "Row" + (target + 1));
            if (row != null)
                row.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return new WaitForSeconds(2.2f);
            yield return Shot("96_sc08_reveal");
            var reasons = 0;
            foreach (var tmp in mcq8.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
            {
                if (tmp.name.StartsWith("Reason") && tmp.gameObject.activeInHierarchy && tmp.text.Length > 0)
                    reasons++;
            }

            Say("SC08 reveal: reasons shown = " + reasons + "/" + cur.options.Length);
            LayoutCheck("SC08 MCQ reveal", mcq8.transform);
            Click(mcq8.transform, "NextButton");
        }

        private IEnumerator Sc08Smoke(InstructionPanelView panel, AudioSource voiceSrc)
        {
            if (PresentableInput.AllowedIn(8) && Get<Sc08AutoDilute>(_director, "sc08Dilute") != null)
            {
                yield return Sc08RaySmoke(voiceSrc);
                yield break;
            }

            yield return WaitStage("sc08Intro", 40f);
            var lab = Get<Sc06Lab>(_director, "sc08Lab");
            var mix = Get<Sc08Mix>(_director, "sc08Mix");
            var ppe = Get<Sc08Ppe>(_director, "sc08Ppe");
            var gate = Get<Sc06Gate>(_director, "sc08Gate");
            var mcq8 = Get<PresentableMcqPanel>(_director, "mcqSc08");
            yield return new WaitForSeconds(2.5f);
            yield return Shot("75_sc08_instruction");
            var cont = FindDeep(panel.transform, "ContinueButton");
            var wc = 0f;
            while (cont != null && !cont.gameObject.activeSelf && wc < 40f)
            {
                wc += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.4f);
            Click(panel.transform, "ContinueButton");

            for (var round = 0; round < 2; round++)
            {
                var tag = round == 0 ? "round 1" : "round 2 (after BACK)";
                var shots = round == 0;
                yield return WaitStage("sc08Ppe", 40f);
                yield return new WaitForSeconds(1.2f);
                Say("SC08 " + tag + ": PPE panel open = " + ppe.PanelOpen + " (expected True), taken = " + ppe.Taken + " (expected 0)");
                if (shots)
                    yield return Shot("76_sc08_ppe");
                for (var i = 0; i < ppe.Count; i++)
                {
                    var cur = ppe.Current;
                    yield return FingerPoke(ppe.PokeOf(cur), "PPE item " + cur);
                    var wt = 0f;
                    while (ppe.Taken <= i && wt < 6f)
                    {
                        wt += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    if (shots && i == 1)
                        yield return Shot("77_sc08_ppe_two");
                    Say("SC08 " + tag + ": item " + cur + " taken, check shown = " + ppe.CheckVisible(cur) + " (expected True), taken = " + ppe.Taken);
                    yield return new WaitForSeconds(0.3f);
                }

                var wd = 0f;
                while (!ppe.AllDone && wd < 6f)
                {
                    wd += Time.unscaledDeltaTime;
                    yield return null;
                }

                Say("SC08 " + tag + ": all PPE taken = " + ppe.AllDone + " (expected True)");

                yield return WaitStage("sc08Hood", 20f);
                yield return new WaitForSeconds(1.4f);
                Say("SC08 " + tag + ": hood arrow = " + lab.ArrowVisible + " (expected True), hood on = " + lab.HoodOn + " (expected False)");
                if (shots)
                    yield return Shot("78_sc08_hood_switch");
                yield return FingerPoke(lab.SwitchPoke(0), "fume hood switch");
                yield return new WaitForSeconds(1.5f);
                Say("SC08 " + tag + ": hood on = " + lab.HoodOn + " (expected True), air streaks = " + lab.AirParticles + ", acid vapor particles = " + lab.VaporParticles);

                yield return WaitStage("sc08Tray", 20f);
                yield return new WaitForSeconds(1.2f);
                if (shots)
                    yield return Shot("79_sc08_tray");
                yield return FingerPoke(mix.TrayPoke, "tray");
                var wtr = 0f;
                while (!mix.Carrying && wtr < 4f)
                {
                    wtr += Time.unscaledDeltaTime;
                    yield return null;
                }

                Say("SC08 " + tag + ": tray carried = " + mix.Carrying + " (expected True)");
                if (shots)
                    yield return Shot("80_sc08_tray_carried");

                yield return WaitStage("sc08Sash", 20f);
                yield return new WaitForSeconds(1.5f);
                Say("SC08 " + tag + ": tray follows the head, distance = " + Vector3.Distance(mix.Tray.position, Camera.main.transform.position).ToString("0.00") + " m");
                if (shots)
                    yield return Shot("81_sc08_hood_stand");
                lab.SetSashInteractable(true);
                yield return FingerPoke(lab.SashPoke, "glass sash");
                var ws = 0f;
                while (!mix.Placed && ws < 8f)
                {
                    ws += Time.unscaledDeltaTime;
                    yield return null;
                }

                Say("SC08 " + tag + ": sash open = " + lab.SashOpen + ", tray in the hood = " + mix.Placed + " (expected True)");

                yield return WaitStage("sc08Mix", 20f);
                yield return new WaitForSeconds(0.8f);
                if (shots)
                    yield return Shot("82_sc08_tray_in_hood");
                var acidStart = mix.AcidFill;
                var waterStart = mix.WaterFill;
                yield return FingerPoke(mix.WaterPoke, "water beaker");
                var wp = 0f;
                var sawStream = false;
                var sawStir = false;
                var shotPour = false;
                while (!mix.Done && wp < 40f)
                {
                    if (mix.StreamVisible && mix.Flow > 0.3f)
                        sawStream = true;
                    if (mix.Stirring)
                        sawStir = true;
                    if (shots && !shotPour && mix.Flow > 0.9f && mix.WaterFill > 0.62f)
                    {
                        shotPour = true;
                        yield return Shot("83_sc08_pour");
                    }

                    wp += Time.unscaledDeltaTime;
                    yield return null;
                }

                Say("SC08 " + tag + ": mix done = " + mix.Done + ", stream seen = " + sawStream + ", stirring seen = " + sawStir +
                    ", water fill " + waterStart.ToString("0.00") + " -> " + mix.WaterFill.ToString("0.00") + " (expected higher), acid fill " +
                    acidStart.ToString("0.00") + " -> " + mix.AcidFill.ToString("0.00") + " (expected lower), acid beaker upright again = " +
                    (Vector3.Angle(mix.AcidBeaker.up, Vector3.up) < 2f));

                yield return WaitStage("sc08Gate", 40f);
                var wg = 0f;
                while (!gate.Visible && wg < 15f)
                {
                    wg += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return new WaitForSeconds(1.3f);
                Say("SC08 " + tag + ": gate card visible = " + gate.Visible + ", sash closed again = " + !lab.SashOpen + ", head z = " +
                    Camera.main.transform.position.z.ToString("0.00"));
                LayoutCheck("SC08 gate card", gate.Card);
                if (shots)
                    yield return Shot("84_sc08_gate");
                if (round == 0)
                {
                    Click(gate.transform, "BackButton");
                }
                else
                {
                    Click(gate.transform, "NextButton");
                }
            }

            yield return WaitStage("sc08Question", 20f);
            var q = 0f;
            while (!mcq8.IsOpen && q < 8f)
            {
                q += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(2.4f);
            Say("SC08 MCQ wow look = " + mcq8.Fancy + " (expected True)");
            LayoutCheck("SC08 MCQ", mcq8.transform);
            yield return Shot("85_sc08_question");
            Click(mcq8.transform, "Row1");
            yield return new WaitForSeconds(1.6f);
            yield return Shot("86_sc08_wrong");
            Click(mcq8.transform, "Close");
            yield return new WaitForSeconds(1.2f);
            Click(mcq8.transform, "Row2");
            var heard = "";
            var h = 0f;
            while (h < 14f)
            {
                if (voiceSrc.clip != null && voiceSrc.clip.name.Contains("Sc08Complete"))
                    heard = voiceSrc.clip.name;
                if (h > 1.2f && h < 1.3f)
                    LayoutCheck("SC08 MCQ correct card", mcq8.transform);
                h += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("scenario 8 complete vo = " + (heard.Length > 0 ? heard : "NOT HEARD"));
            yield return Shot("87_sc08_correct");
            Click(mcq8.transform, "NextButton");
        }

        private IEnumerator Sc09Smoke(InstructionPanelView panel, AudioSource voiceSrc)
        {
            yield return WaitStage("sc09Intro", 40f);
            var flask = Get<Sc09Flask>(_director, "sc09Flask");
            var shower = Get<Sc09Shower>(_director, "sc09Shower");
            var visor = Get<Sc09Visor>(_director, "sc09Visor");
            var guide = Get<Sc09Guide>(_director, "sc09Guide");
            var gate = Get<Sc06Gate>(_director, "sc09Gate");
            var mcq9 = Get<PresentableMcqPanel>(_director, "mcqSc09");
            var clipboard = Get<SdsClipboardController>(_director, "clipboard");
            yield return new WaitForSeconds(2.5f);
            yield return Shot("88_sc09_instruction");
            yield return ClickContinue(panel, 40f);

            for (var round = 0; round < 2; round++)
            {
                var tag = round == 0 ? "round 1" : "round 2 (after BACK)";
                var shots = round == 0;
                yield return WaitStage("sc09Flask", 40f);
                yield return new WaitForSeconds(1.5f);
                Say("SC09 " + tag + ": flask arrow = " + guide.Visible + " (expected True), broken = " + flask.Broken + " (expected False), visor dirty = " + visor.Dirty + " (expected False)");
                if (shots)
                    yield return Shot("89_sc09_bench");
                yield return FingerPoke(flask.Poke, "purple flask");
                var wc = 0f;
                while (!flask.Carrying && wc < 4f)
                {
                    wc += Time.unscaledDeltaTime;
                    yield return null;
                }

                Say("SC09 " + tag + ": flask lifted = " + flask.Carrying + " (expected True)");
                yield return new WaitForSeconds(2.2f);
                if (shots)
                    yield return Shot("90_sc09_pour");
                var wb = 0f;
                while (!flask.Broken && wb < 10f)
                {
                    wb += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return new WaitForSeconds(0.5f);
                if (shots)
                    yield return Shot("91_sc09_breaking");
                yield return new WaitForSeconds(1.6f);
                Say("SC09 " + tag + ": flask broken = " + flask.Broken + " (expected True), visor dirty = " + visor.Dirty + " (expected True), strength = " + visor.Strength.ToString("0.00"));
                if (shots)
                    yield return Shot("92_sc09_broken_visor");

                yield return WaitStage("sc09Spill", 20f);
                yield return new WaitForSeconds(2.5f);
                if (shots)
                    yield return Shot("93_sc09_spill_card");
                yield return ClickContinue(panel, 40f);

                yield return WaitStage("sc09Sds", 20f);
                yield return new WaitForSeconds(1.8f);
                Say("SC09 " + tag + ": SDS visible = " + clipboard.IsVisible + " (expected True)");
                if (shots)
                    yield return Shot("94_sc09_sds");
                clipboard.SetVisible(false);

                yield return WaitStage("sc09Walk", 20f);
                yield return new WaitForSeconds(1.2f);
                if (shots)
                    yield return Shot("95_sc09_stand_here");
                yield return WaitStage("sc09Wash", 20f);
                yield return new WaitForSeconds(1.2f);
                Say("SC09 " + tag + ": push arrow = " + guide.Visible + " (expected True), water running = " + shower.Running + " (expected False)");
                if (shots)
                    yield return Shot("96_sc09_push");
                yield return FingerPoke(shower.PushPoke, "push plate");
                yield return new WaitForSeconds(3.0f);
                Say("SC09 " + tag + ": water running = " + shower.Running + " (expected True), jets = " + shower.ActiveJets + ", hologram = " + shower.HoloVisible +
                    " (expected True), visor strength = " + visor.Strength.ToString("0.00") + ", clean = " + visor.Clean.ToString("0.00"));
                if (shots)
                    yield return Shot("97_sc09_water_start");
                yield return new WaitForSeconds(4.5f);
                if (shots)
                    yield return Shot("98_sc09_washing");
                var ww = 0f;
                while (!shower.WashDone && ww < 25f)
                {
                    ww += Time.unscaledDeltaTime;
                    yield return null;
                }

                Say("SC09 " + tag + ": wash done = " + shower.WashDone + " (expected True), visor clean = " + visor.Clean.ToString("0.00") + " (expected 1.00)");

                yield return WaitStage("sc09Gate", 40f);
                var wg = 0f;
                while (!gate.Visible && wg < 15f)
                {
                    wg += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return new WaitForSeconds(1.3f);
                Say("SC09 " + tag + ": gate card visible = " + gate.Visible + ", visor hidden = " + !visor.Dirty);
                LayoutCheck("SC09 gate card", gate.Card);
                if (shots)
                    yield return Shot("99_sc09_gate");
                Click(gate.transform, round == 0 ? "BackButton" : "NextButton");
            }

            yield return WaitStage("sc09Question", 20f);
            var q = 0f;
            while (!mcq9.IsOpen && q < 8f)
            {
                q += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(2.4f);
            Say("SC09 MCQ wow look = " + mcq9.Fancy + " (expected True)");
            LayoutCheck("SC09 MCQ", mcq9.transform);
            yield return Shot("100_sc09_question");
            Click(mcq9.transform, "Row1");
            yield return new WaitForSeconds(1.6f);
            yield return Shot("101_sc09_wrong");
            Click(mcq9.transform, "Close");
            yield return new WaitForSeconds(1.2f);
            Click(mcq9.transform, "Row2");
            var heard = "";
            var h = 0f;
            while (h < 16f)
            {
                if (voiceSrc.clip != null && voiceSrc.clip.name.Contains("Sc09Complete"))
                    heard = voiceSrc.clip.name;
                if (h > 1.2f && h < 1.3f)
                    LayoutCheck("SC09 MCQ correct card", mcq9.transform);
                h += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("scenario 9 complete vo = " + (heard.Length > 0 ? heard : "NOT HEARD"));
            yield return Shot("102_sc09_correct");
            Click(mcq9.transform, "NextButton");
        }

        // Gate cards: the simulated right glove rests where the card's BACK button appears and pressed it by itself in the
        // chained run (the director then looped SC10 a third time). The test clicks the buttons itself, so the fingertips are off meanwhile.
        private void FingerGuard(bool on)
        {
            var fps = _director != null ? _director.GetComponent<FingerPokeSystem>() : null;
            if (fps != null)
                fps.enabled = on;
        }

        private IEnumerator Sc10Wait(System.Func<bool> done, float timeout)
        {
            var t = 0f;
            while (!done() && t < timeout)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator Sc10Icon(Sc10Spill spill, string stage, string tag, System.Func<bool> finished, string shotName)
        {
            yield return WaitStage(stage, 30f);
            yield return Sc10Wait(() => spill.IconVisible, 10f);
            yield return new WaitForSeconds(0.8f);
            Say("SC10 " + tag + ": " + stage + " icon visible = " + spill.IconVisible + " (expected True)");
            if (shotName != null)
                yield return Shot(shotName + "_icon");
            yield return FingerPoke(spill.IconPoke, stage + " icon");
            yield return new WaitForSeconds(1.8f);
            if (shotName != null)
                yield return Shot(shotName + "_anim");
            yield return Sc10Wait(finished, 25f);
            yield return new WaitForSeconds(0.4f);
            if (shotName != null)
                yield return Shot(shotName + "_done");
        }

        private IEnumerator Sc10Smoke(InstructionPanelView panel, AudioSource voiceSrc)
        {
            yield return WaitStage("sc10Intro", 40f);
            var lab = Get<Sc06Lab>(_director, "sc10Lab");
            var ppe = Get<Sc08Ppe>(_director, "sc10Ppe");
            var spill = Get<Sc10Spill>(_director, "sc10Spill");
            var gate = Get<Sc06Gate>(_director, "sc10Gate");
            var mcq10 = Get<PresentableMcqPanel>(_director, "mcqSc10");
            var clipboard = Get<SdsClipboardController>(_director, "clipboard");
            yield return new WaitForSeconds(2.5f);
            yield return Shot("103_sc10_instruction");
            yield return ClickContinue(panel, 40f);

            for (var round = 0; round < 2; round++)
            {
                var tag = round == 0 ? "round 1" : "round 2 (after BACK)";
                var shots = round == 0;
                yield return WaitStage("sc10Sds", 40f);
                yield return new WaitForSeconds(1.8f);
                Say("SC10 " + tag + ": SDS visible = " + clipboard.IsVisible + " (expected True)");
                if (shots)
                    yield return Shot("104_sc10_sds");
                clipboard.SetVisible(false);

                yield return WaitStage("sc10Ppe", 40f);
                yield return new WaitForSeconds(1.2f);
                Say("SC10 " + tag + ": PPE panel open = " + ppe.PanelOpen + " (expected True), taken = " + ppe.Taken + " (expected 0)");
                if (shots)
                    yield return Shot("105_sc10_ppe");
                for (var i = 0; i < ppe.Count; i++)
                {
                    var cur = ppe.Current;
                    yield return FingerPoke(ppe.PokeOf(cur), "PPE item " + cur);
                    yield return Sc10Wait(() => ppe.Taken > cur || ppe.AllDone, 6f);
                    yield return new WaitForSeconds(0.3f);
                }

                Say("SC10 " + tag + ": all PPE taken = " + ppe.AllDone + " (expected True)");

                yield return WaitStage("sc10Vent", 20f);
                yield return new WaitForSeconds(1.4f);
                Say("SC10 " + tag + ": hood arrow = " + lab.ArrowVisible + " (expected True), hood on = " + lab.HoodOn + " (expected False)");
                if (shots)
                    yield return Shot("106_sc10_vent");
                yield return FingerPoke(lab.SwitchPoke(0), "fume hood switch");
                yield return new WaitForSeconds(1.5f);
                Say("SC10 " + tag + ": hood on = " + lab.HoodOn + " (expected True)");

                yield return WaitStage("sc10Kit", 20f);
                yield return new WaitForSeconds(1.2f);
                if (shots)
                    yield return Shot("107_sc10_kit");
                yield return FingerPoke(spill.KitPoke, "spill kit door");
                yield return Sc10Wait(() => spill.KitOpen, 4f);
                yield return new WaitForSeconds(1.4f);
                Say("SC10 " + tag + ": kit open = " + spill.KitOpen + " (expected True)");
                if (shots)
                    yield return Shot("108_sc10_kit_open");
                var doorGo = GameObject.Find("Sc10KitDoor");
                var cabGo = GameObject.Find("Sc10FlamCabinetRig");
                if (doorGo != null && cabGo != null)
                {
                    var clear = true;
                    foreach (var dr in doorGo.GetComponentsInChildren<Renderer>())
                        foreach (var cr in cabGo.GetComponentsInChildren<Renderer>())
                            if (dr.bounds.Intersects(cr.bounds))
                                clear = false;
                    Say("SC10 " + tag + ": open kit door clear of the cabinet = " + clear + " (expected True)");
                    if (!clear)
                        Debug.LogError("[SMOKE] SC10 the open kit door goes into the cabinet");
                }

                yield return WaitStage("sc10Collect", 20f);
                yield return new WaitForSeconds(1.0f);
                Say("SC10 " + tag + ": checklist visible = " + spill.PanelVisible + " (expected True)");
                // a hand resting at the kit must not pick anything up; take it away, then every item has to be touchable
                var restHand = GlovedHandPose.Right;
                if (restHand != null && restHand.HasTip)
                {
                    // towards the head (never sideways: in a full run a fixed sideways jump can land the simulated fingertip inside a kit item)
                    var headT = Camera.main != null ? Camera.main.transform : null;
                    var away = headT != null ? (headT.position - restHand.TipPosition).normalized : Vector3.back;
                    restHand.transform.position += away * 0.9f;
                }
                yield return new WaitForSeconds(0.5f);
                for (var ti = 0; ti < 5; ti++)
                {
                    var ip = spill.ItemPoke(ti);
                    var touchable = ip != null && ip.Interactable;
                    Say("SC10 " + tag + ": kit item " + ti + " touchable = " + touchable + " (expected True)");
                    if (!touchable && spill.IsCollected(ti))
                        Say("SC10 " + tag + ": kit item " + ti + " was already collected by the resting simulated glove (harness artefact of the chained run)");
                    else if (!touchable)
                        Debug.LogError("[SMOKE] SC10 kit item " + ti + " cannot be touched in the collect step (" + spill.DebugState + ")");
                }
                // take the items one by one, whatever order the simulated glove already took some in (a poke while the previous
                // item is still flying is ignored by the game, so keep going until all five are in)
                for (var guard = 0; guard < 14 && !spill.AllCollected; guard++)
                {
                    var idx = -1;
                    for (var k = 0; k < 5; k++)
                    {
                        if (!spill.IsCollected(k))
                        {
                            idx = k;
                            break;
                        }
                    }

                    if (idx < 0)
                        break;
                    var before = spill.Collected;
                    yield return FingerPoke(spill.ItemPoke(idx), "kit item " + idx);
                    yield return Sc10Wait(() => spill.Collected > before, 6f);
                    if (shots && spill.Collected == 2)
                        yield return Shot("109_sc10_collect");
                    yield return new WaitForSeconds(1.0f);
                }

                Say("SC10 " + tag + ": all collected = " + spill.AllCollected + " (expected True)");

                yield return Sc10Icon(spill, "sc10Contain", tag, () => spill.SockLaid, shots ? "110_sc10_contain" : null);
                Say("SC10 " + tag + ": sock laid = " + spill.SockLaid + " (expected True)");
                yield return Sc10Icon(spill, "sc10Neutralize", tag, () => spill.Neutralized, shots ? "111_sc10_neutralize" : null);
                Say("SC10 " + tag + ": neutralized = " + spill.Neutralized + " (expected True), puddle alpha = " + spill.PuddleAlpha.ToString("0.00"));
                yield return Sc10Icon(spill, "sc10Absorb", tag, () => spill.Absorbed, shots ? "112_sc10_absorb" : null);
                Say("SC10 " + tag + ": absorbed = " + spill.Absorbed + " (expected True), puddle alpha = " + spill.PuddleAlpha.ToString("0.00") + " | " + spill.PadInfo);
                yield return Sc10Icon(spill, "sc10Clean", tag, () => spill.GlassCleaned, shots ? "113_sc10_clean" : null);
                Say("SC10 " + tag + ": glass cleaned = " + spill.GlassCleaned + " (expected True), pieces in pan = " + spill.ShardsInPan);
                yield return Sc10Icon(spill, "sc10Dispose", tag, () => spill.Disposed, shots ? "114_sc10_dispose" : null);
                Say("SC10 " + tag + ": disposed = " + spill.Disposed + " (expected True)");

                FingerGuard(false); // the resting simulated glove must not press the gate by itself (harness artefact)
                yield return WaitStage("sc10Gate", 40f);
                yield return Sc10Wait(() => gate.Visible, 15f);
                yield return new WaitForSeconds(1.3f);
                Say("SC10 " + tag + ": gate card visible = " + gate.Visible);
                LayoutCheck("SC10 gate card", gate.Card);
                if (shots)
                    yield return Shot("115_sc10_gate");
                Click(gate.transform, round == 0 ? "BackButton" : "NextButton");
                FingerGuard(true);
            }

            yield return WaitStage("sc10Question", 20f);
            yield return Sc10Wait(() => mcq10.IsOpen, 8f);
            yield return new WaitForSeconds(2.4f);
            Say("SC10 MCQ wow look = " + mcq10.Fancy + " (expected True)");
            LayoutCheck("SC10 MCQ", mcq10.transform);
            yield return Shot("116_sc10_question");
            Click(mcq10.transform, "Row1");
            yield return new WaitForSeconds(1.6f);
            yield return Shot("117_sc10_wrong");
            Click(mcq10.transform, "Close");
            yield return new WaitForSeconds(1.2f);
            Click(mcq10.transform, "Row2");
            var heard = "";
            var h = 0f;
            while (h < 16f)
            {
                if (voiceSrc.clip != null && voiceSrc.clip.name.Contains("Sc10Complete"))
                    heard = voiceSrc.clip.name;
                if (h > 1.2f && h < 1.3f)
                    LayoutCheck("SC10 MCQ correct card", mcq10.transform);
                h += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("scenario 10 complete vo = " + (heard.Length > 0 ? heard : "NOT HEARD"));
            yield return Shot("118_sc10_correct");
            Click(mcq10.transform, "NextButton");
        }

        private IEnumerator Sc11Smoke(InstructionPanelView panel, AudioSource voiceSrc)
        {
            yield return WaitStage("sc11Intro", 40f);
            var yard = Get<Sc11Yard>(_director, "sc11Yard");
            var gate = Get<Sc06Gate>(_director, "sc11Gate");
            var mcq11 = Get<PresentableMcqPanel>(_director, "mcqSc11");
            var clipboard = Get<SdsClipboardController>(_director, "clipboard");
            yield return new WaitForSeconds(2.5f);
            yield return Shot("119_sc11_instruction");
            yield return ClickContinue(panel, 40f);

            for (var round = 0; round < 2; round++)
            {
                var tag = round == 0 ? "round 1" : "round 2 (after BACK)";
                var shots = round == 0;
                yield return WaitStage("sc11Sds", 40f);
                yield return new WaitForSeconds(1.8f);
                Say("SC11 " + tag + ": SDS visible = " + clipboard.IsVisible + " (expected True)");
                if (shots)
                    yield return Shot("120_sc11_sds");

                yield return WaitStage("sc11Alarm", 40f);
                yield return Sc10Wait(() => yard.IconVisible, 10f);
                yield return new WaitForSeconds(0.8f);
                Say("SC11 " + tag + ": exit sign = " + yard.ExitShown + ", siren icon visible = " + yard.IconVisible + " (expected True)");
                if (shots)
                    yield return Shot("121_sc11_alarm_icon");
                yield return FingerPoke(yard.IconPoke, "siren icon");
                yield return new WaitForSeconds(2.0f);
                Say("SC11 " + tag + ": alarm on = " + yard.AlarmOn + ", siren sound = " + yard.SirenPlaying + " (expected True)");
                if (shots)
                    yield return Shot("122_sc11_alarm_on");

                yield return WaitStage("sc11Evacuate", 20f);
                yield return new WaitForSeconds(3.0f);
                if (shots)
                    yield return Shot("123_sc11_evacuate");
                yield return WaitStage("sc11Call", 60f);
                Say("SC11 " + tag + ": workers out = " + yard.WorkersOut + "/" + yard.WorkerCount + " (expected all)");
                yield return Sc10Wait(() => yard.IconVisible, 10f);
                yield return new WaitForSeconds(0.8f);
                if (shots)
                    yield return Shot("124_sc11_call_icon");
                yield return FingerPoke(yard.IconPoke, "call icon");

                yield return WaitStage("sc11Radio", 20f);
                yield return Sc10Wait(() => yard.RadioVisible, 6f);
                yield return new WaitForSeconds(2.4f);
                Say("SC11 " + tag + ": radio in hand = " + yard.RadioVisible + ", calling = " + yard.Calling + " (expected True)");
                if (shots)
                    yield return Shot("125_sc11_radio");
                yield return new WaitForSeconds(5.5f);
                if (shots)
                    yield return Shot("126_sc11_radio_talk");

                FingerGuard(false); // the resting simulated glove must not press the gate by itself (harness artefact)
                yield return WaitStage("sc11Gate", 60f);
                yield return Sc10Wait(() => gate.Visible, 15f);
                yield return new WaitForSeconds(1.3f);
                Say("SC11 " + tag + ": gate card visible = " + gate.Visible);
                LayoutCheck("SC11 gate card", gate.Card);
                if (shots)
                    yield return Shot("127_sc11_gate");
                Click(gate.transform, round == 0 ? "BackButton" : "NextButton");
                FingerGuard(true);
            }

            yield return WaitStage("sc11Question", 20f);
            yield return Sc10Wait(() => mcq11.IsOpen, 8f);
            yield return new WaitForSeconds(2.4f);
            Say("SC11 MCQ wow look = " + mcq11.Fancy + " (expected True)");
            LayoutCheck("SC11 MCQ", mcq11.transform);
            yield return Shot("128_sc11_question");
            Click(mcq11.transform, "Row2");
            yield return new WaitForSeconds(1.6f);
            yield return Shot("129_sc11_wrong");
            Click(mcq11.transform, "Close");
            yield return new WaitForSeconds(1.2f);
            Click(mcq11.transform, "Row1");
            var heard = "";
            var h = 0f;
            while (h < 16f)
            {
                if (voiceSrc.clip != null && voiceSrc.clip.name.Contains("Sc11Complete"))
                    heard = voiceSrc.clip.name;
                if (h > 1.2f && h < 1.3f)
                    LayoutCheck("SC11 MCQ correct card", mcq11.transform);
                h += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("scenario 11 complete vo = " + (heard.Length > 0 ? heard : "NOT HEARD"));
            yield return Shot("130_sc11_correct");
            Click(mcq11.transform, "NextButton");
        }

        // The text on a row ("Row0".."Row3" under root): the label child's text.
        private static string RowText(Transform root, string rowName)
        {
            var row = FindDeep(root, rowName);
            var label = row != null ? row.Find("Label") : null;
            var tmp = label != null ? label.GetComponent<TMPro.TMP_Text>() : null;
            return tmp != null ? tmp.text : "";
        }

        // SC13-SC18 (Supervisor Pre-Task Safety Check): drives the runner stage by stage. Question 2 is answered wrong on purpose,
        // the SDS panel is opened once per question, every panel gets a layout check; shots are taken in the first scenario only.
        // Pose photos of the supervisor (Play mode: the skinning is up to date): front, both sides and a close-up of the hands.
        private IEnumerator SupPhotos(string tag)
        {
            var sup = FindFirstObjectByType<LabSupervisor>();
            if (sup == null)
                yield break;
            yield return new WaitForSeconds(3f);
            yield return new WaitForEndOfFrame();
            Say("SUP " + tag + " " + sup.DebugDump());
            var hide = SessionState.GetString("SupHide", ""); // debugging: comma list of supervisor parts to switch off for the photos
            if (!string.IsNullOrEmpty(hide))
                foreach (var r in sup.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if (Array.IndexOf(hide.Split(','), r.name) >= 0)
                        r.enabled = false;
            var camGo = new GameObject("SupPhotoCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(900, 900, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(900, 900, TextureFormat.RGB24, false);
            var main = Camera.main;
            var toP = main != null ? main.transform.position - sup.transform.position : sup.transform.forward;
            toP.y = 0f;
            toP.Normalize();
            var right = Vector3.Cross(Vector3.up, toP);
            var look = sup.transform.position + Vector3.up * 1.2f;
            var views = new[] { toP * 1.9f + Vector3.up * 1.5f, toP * 1.5f + right * 1.1f + Vector3.up * 1.4f, toP * 1.5f - right * 1.1f + Vector3.up * 1.4f, toP * 0.9f + Vector3.up * 1.5f };
            for (var i = 0; i < views.Length; i++)
            {
                cam.fieldOfView = i == 3 ? 40f : 34f;
                cam.transform.position = sup.transform.position + views[i];
                cam.transform.LookAt(i == 3 ? sup.transform.position + Vector3.up * 1.15f : look);
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(_dir, "sup_" + tag + "_" + i + ".png"), tex.EncodeToPNG());
            }

            if (main != null)
            {
                // the machine wing, as the player sees it when looking left
                foreach (var yaw in new[] { -35f, -70f, 28f })
                {
                    cam.fieldOfView = yaw > 0f ? 55f : 80f;
                    cam.transform.SetPositionAndRotation(main.transform.position, Quaternion.Euler(yaw > 0f ? 22f : 8f, main.transform.eulerAngles.y + yaw, 0f));
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(_dir, "sup_" + tag + "_wing" + (int)-yaw + ".png"), tex.EncodeToPNG());
                }
            }

            Destroy(camGo);
            Destroy(rt);
            Destroy(tex);
        }

        private IEnumerator ScCheckSmoke()
        {
            var runner = Get<ScCheckRunner>(_director, "scCheck");
            if (runner == null)
            {
                Say("ScCheck runner missing (expected present)");
                yield break;
            }

            for (var n = 13; n <= 18; n++)
            {
                yield return WaitStage("sc" + n, 60f);
                var shots = n == 13;
                var settle = 0f;
                while (n > 13 && runner.Stage == "done" && settle < 30f)
                {
                    settle += Time.unscaledDeltaTime;
                    yield return null;
                }

                var last = "";
                var guard = 0f;
                var sdsTried = false;
                var answered = false;
                var score = 0;
                while (runner.Stage != "done" && guard < 300f)
                {
                    guard += Time.unscaledDeltaTime;
                    var st = runner.Stage;
                    if (st != last)
                    {
                        last = st;
                        sdsTried = false;
                        answered = false;
                        Say("SC" + n + " runner stage " + st);
                        if (n == 13 && (st == "intro" || st == "q1") && SessionState.GetBool("SupPhotos", false))
                            yield return SupPhotos(st);
                    }

                    if (st == "intro" || st == "startCard" || st == "bottle")
                    {
                        yield return new WaitForSeconds(1.5f);
                        if (shots)
                            yield return Shot("150_sccheck_" + st);
                        for (var w = 0f; w < 40f && runner.Stage == st; w += Time.unscaledDeltaTime)
                        {
                            runner.PressCard();
                            yield return null;
                        }
                    }
                    else if (st.Length == 2 && st[0] == 'q' && runner.QuestionVisible)
                    {
                        if (!sdsTried)
                        {
                            sdsTried = true;
                            yield return new WaitForSeconds(1.0f);
                            LayoutCheck("SC" + n + " " + st, runner.MainPanel, true);
                            ScSupervisorClear("SC" + n + " " + st, runner);
                            runner.ToggleSds();
                            yield return new WaitForSeconds(0.8f);
                            Say("SC" + n + " " + st + " SDS open = " + runner.SdsOpen + " (expected True)");
                            ScDragScrollCheck("SC" + n + " " + st, runner);
                            ScSupervisorClear("SC" + n + " " + st + " (SDS open)", runner);
                            if (shots)
                                yield return Shot("151_sccheck_" + st + "_sds");
                            runner.ScrollSds(-0.5f);
                            runner.ToggleSds();
                            yield return new WaitForSeconds(0.6f);
                            Say("SC" + n + " " + st + " SDS open = " + runner.SdsOpen + " (expected False)");
                        }
                        else if (!answered)
                        {
                            answered = true;
                            var wrong = st == "q2";
                            runner.SelectOption(wrong ? (runner.CorrectRow + 1) % 4 : runner.CorrectRow);
                            if (!wrong)
                                score++;
                            yield return new WaitForSeconds(0.8f);
                            if (shots)
                                yield return Shot("152_sccheck_" + st + "_answered");
                        }
                        else if (runner.AnswerLocked)
                        {
                            runner.PressNext();
                        }
                    }
                    else if (st == "results")
                    {
                        yield return new WaitForSeconds(1.5f);
                        if (shots)
                            yield return Shot("153_sccheck_results");
                        for (var w = 0f; w < 40f && runner.Stage == st; w += Time.unscaledDeltaTime)
                        {
                            runner.PressNextRound();
                            yield return null;
                        }
                    }

                    yield return null;
                }

                Say("SC" + n + " finished, score expected 2 of 3, runner reports " + runner.LastScore);
            }
        }

        private IEnumerator Sc12Smoke(InstructionPanelView panel, AudioSource voiceSrc)
        {
            yield return WaitStage("sc12Intro", 40f);
            var station = Get<Sc12Station>(_director, "sc12Station");
            var gate = Get<Sc06Gate>(_director, "sc12Gate");
            var assess = Get<Sc12Assessment>(_director, "assessmentSc12");
            var clipboard = Get<SdsClipboardController>(_director, "clipboard");
            yield return new WaitForSeconds(2.5f);
            yield return Shot("131_sc12_instruction");
            yield return ClickContinue(panel, 40f);

            for (var round = 0; round < 2; round++)
            {
                var tag = round == 0 ? "round 1" : "round 2 (after BACK)";
                var shots = round == 0;
                yield return WaitStage("sc12Sds", 40f);
                yield return new WaitForSeconds(1.8f);
                Say("SC12 " + tag + ": SDS visible = " + clipboard.IsVisible + " (expected True)");
                if (shots)
                    yield return Shot("132_sc12_sds");

                yield return WaitStage("sc12Stand", 40f);
                yield return new WaitForSeconds(1.5f);
                if (shots)
                    yield return Shot("133_sc12_stand");

                for (var pick = 0; pick < 4; pick++)
                {
                    yield return WaitStage("sc12Pick" + pick, 40f);
                    yield return new WaitForSeconds(1.6f);
                    var right = Sc12Content.Options(pick)[0];
                    Say("SC12 " + tag + " pick " + pick + ": panel open = " + station.PanelOpen + " (expected True), right item = " + right);
                    LayoutCheck("SC12 pick panel " + pick, FindDeep(station.transform, "Sc12Panel"));
                    if (shots && pick == 0)
                        yield return Shot("134_sc12_pick");
                    var rightRow = -1;
                    var wrongRow = -1;
                    for (var r = 0; r < 4; r++)
                    {
                        if (RowText(station.transform, "Row" + r) == right)
                            rightRow = r;
                        else if (wrongRow < 0)
                            wrongRow = r;
                    }

                    if (rightRow < 0)
                    {
                        Say("FAIL SC12 pick " + pick + ": the right option is not on the panel");
                        continue;
                    }

                    if (shots)
                    {
                        Click(station.transform, "Row" + wrongRow);
                        yield return Sc10Wait(() => station.Result == 2, 10f);
                        Say("SC12 pick " + pick + ": wrong pick result = " + station.Result + " (expected 2)");
                        LayoutCheck("SC12 wrong card " + pick, FindDeep(station.transform, "Sc12Card"));
                        if (pick == 0)
                            yield return Shot("135_sc12_wrong");
                        yield return Sc10Wait(() => station.Result == 0, 6f);
                        yield return new WaitForSeconds(0.4f);
                    }

                    Click(station.transform, "Row" + rightRow);
                    if (shots && pick == 0)
                    {
                        yield return new WaitForSeconds(0.9f);
                        yield return Shot("136_sc12_right_held");
                    }

                    var worn0 = station.WornCount;
                    yield return Sc10Wait(() => station.WornCount > worn0, 10f);
                    Say("SC12 " + tag + " pick " + pick + ": worn items = " + station.WornCount + " (expected " + (pick + 1) + ")");
                }

                yield return WaitStage("sc12Review", 30f);
                yield return new WaitForSeconds(3.4f);
                LayoutCheck("SC12 review", FindDeep(station.transform, "Sc12Review"));
                if (shots)
                    yield return Shot("137_sc12_review");
                Click(station.transform, "NextButton");

                FingerGuard(false); // the resting simulated glove must not press the gate by itself (harness artefact)
                yield return WaitStage("sc12Gate", 30f);
                yield return Sc10Wait(() => gate.Visible, 10f);
                yield return new WaitForSeconds(1.3f);
                Say("SC12 " + tag + ": gate card visible = " + gate.Visible);
                LayoutCheck("SC12 gate card", gate.Card);
                if (shots)
                    yield return Shot("138_sc12_gate");
                Click(gate.transform, round == 0 ? "BackButton" : "NextButton");
                FingerGuard(true);
            }

            yield return WaitStage("sc12Question", 20f);
            yield return Sc10Wait(() => assess.Visible, 8f);
            yield return new WaitForSeconds(1.2f);
            Say("SC12 assessment visible = " + assess.Visible + " (expected True)");
            LayoutCheck("SC12 assessment", FindDeep(assess.transform, "Sc12AssessPanel"));
            yield return Shot("139_sc12_question");

            var labels = Sc12Content.OptionLabels;
            var boots = -1;
            var good = new System.Collections.Generic.List<int>();
            for (var r = 0; r < assess.RowCount; r++)
            {
                var text = RowText(assess.transform, "Row" + r);
                if (text == labels[3])
                    boots = r;
                else
                    good.Add(r);
            }

            Say("SC12 shown rows: boots at row " + boots + ", right rows " + good.Count + " (expected 3)");
            // wrong 1: only boots ticked, wrong 2: one right item missing, then the exact set
            assess.ToggleRow(boots);
            assess.PressSubmit();
            yield return new WaitForSeconds(1.2f);
            Say("SC12 boots only: won = " + assess.Won + " (expected False)");
            yield return Shot("140_sc12_wrong");
            LayoutCheck("SC12 wrong popup", FindDeep(assess.transform, "Sc12AssessPopup"));
            Say("SC12 wrong popup visible = " + assess.PopupVisible + " (expected True)");
            Click(assess.transform, "Close");
            yield return new WaitForSeconds(0.8f);
            assess.ToggleRow(boots);
            assess.ToggleRow(good[0]);
            assess.ToggleRow(good[1]);
            assess.PressSubmit();
            yield return new WaitForSeconds(1.2f);
            Say("SC12 two of three right items: won = " + assess.Won + " (expected False)");
            Click(assess.transform, "Close");
            yield return new WaitForSeconds(0.8f);
            assess.ToggleRow(good[2]);
            assess.PressSubmit();
            var heard = "";
            var h = 0f;
            while (h < 16f)
            {
                if (voiceSrc.clip != null && voiceSrc.clip.name.Contains("Sc12Complete"))
                    heard = voiceSrc.clip.name;
                if (h > 1.2f && h < 1.3f)
                    LayoutCheck("SC12 correct popup", FindDeep(assess.transform, "Sc12AssessPopup"));
                h += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("SC12 assessment won = " + assess.Won + " (expected True)");
            Say("scenario 12 complete vo = " + (heard.Length > 0 ? heard : "NOT HEARD"));
            yield return Shot("141_sc12_correct");
            Click(assess.transform, "NextButton");
        }

        private IEnumerator ClickContinue(InstructionPanelView panel, float timeout)
        {
            var cont = FindDeep(panel.transform, "ContinueButton");
            var wc = 0f;
            while (cont != null && !cont.gameObject.activeSelf && wc < timeout)
            {
                wc += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.4f);
            Click(panel.transform, "ContinueButton");
        }

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
#if UNITY_EDITOR
            // menu "Dev: start at Scenario 7": the director starts straight in Scenario 7, so only that part is tested
            if (UnityEditor.EditorPrefs.GetBool("ChemSafetyScCheckOnly", false))
            {
                Say("ScCheck-only run (dev start)");
                yield return ScCheckSmoke();
                yield return new WaitForSeconds(1f);
                Say("DONE runtime errors/exceptions = " + _errors);
                UnityEditor.EditorApplication.isPlaying = false;
                yield break;
            }

            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc12Only", false))
            {
                Say("SC12-only run (dev start)");
                yield return Sc12Smoke(panel, voiceSrc);
                yield return new WaitForSeconds(1f);
                Say("DONE runtime errors/exceptions = " + _errors);
                UnityEditor.EditorApplication.isPlaying = false;
                yield break;
            }

            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc11Only", false))
            {
                Say("SC11-only run (dev start)");
                yield return Sc11Smoke(panel, voiceSrc);
                yield return new WaitForSeconds(1f);
                Say("DONE runtime errors/exceptions = " + _errors);
                UnityEditor.EditorApplication.isPlaying = false;
                yield break;
            }

            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc10Only", false))
            {
                Say("SC10-only run (dev start)");
                yield return Sc10Smoke(panel, voiceSrc);
                yield return new WaitForSeconds(1f);
                Say("DONE runtime errors/exceptions = " + _errors);
                UnityEditor.EditorApplication.isPlaying = false;
                yield break;
            }

            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc09Only", false))
            {
                Say("SC09-only run (dev start)");
                yield return Sc09Smoke(panel, voiceSrc);
                yield return new WaitForSeconds(1f);
                Say("DONE runtime errors/exceptions = " + _errors);
                UnityEditor.EditorApplication.isPlaying = false;
                yield break;
            }

            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc08Only", false))
            {
                Say("SC08-only run (dev start)");
                yield return Sc08Smoke(panel, voiceSrc);
                yield return new WaitForSeconds(1f);
                Say("DONE runtime errors/exceptions = " + _errors);
                UnityEditor.EditorApplication.isPlaying = false;
                yield break;
            }

            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc07Only", false))
            {
                Say("SC07-only run (dev start)");
                yield return Sc07Dispatch(panel, voiceSrc);
                yield return new WaitForSeconds(1f);
                Say("DONE runtime errors/exceptions = " + _errors);
                UnityEditor.EditorApplication.isPlaying = false;
                yield break;
            }

            // ChemSafetySc06Only: director starts at Scenario 6 (controller ray when the input mode allows it)
            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc06Only", false) && PresentableInput.AllowedIn(6))
            {
                Say("SC06-only run (dev start)");
                yield return Sc06RaySmoke(voiceSrc);
                yield return new WaitForSeconds(1f);
                Say("DONE runtime errors/exceptions = " + _errors);
                UnityEditor.EditorApplication.isPlaying = false;
                yield break;
            }

            // ChemSafetySc05Only: director starts at Scenario 5 (no login / SC01-04)
            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc05Only", false))
            {
                Say("SC05-only run (dev start)");
                yield return Sc05Smoke(panel, voiceSrc);
                yield return new WaitForSeconds(1f);
                Say("DONE runtime errors/exceptions = " + _errors);
                UnityEditor.EditorApplication.isPlaying = false;
                yield break;
            }
#endif
            var login = FindFirstObjectByType<LoginScreen>();
            if (login != null)
            {
                yield return WaitStage("login", 30f);
                yield return new WaitForSeconds(1.5f);
                yield return Shot("00_login_en");
                yield return LoginErgoCheck(login);
                Say("login: name with digits rejected = " + !login.SubmitForTest("Ra5im", "01712345678", ChemicalSafety.Localization.Lang.En));
                Say("login: 10-digit phone rejected = " + !login.SubmitForTest("Rahim", "0171234567", ChemicalSafety.Localization.Lang.En));
                Say("login: 12-digit phone rejected = " + !login.SubmitForTest("Rahim", "017123456789", ChemicalSafety.Localization.Lang.En));
                Say("login: bad e-mail rejected = " + !login.SubmitForTest("Rahim", "rahim@mail", ChemicalSafety.Localization.Lang.En));
                Say("login: letter-only contact uses email path = " + !login.SubmitForTest("Rahim", "cfgds", ChemicalSafety.Localization.Lang.En));
                yield return new WaitForSeconds(0.5f);
                yield return Shot("00_login_errors");
                login.SubmitForTest("Rahim", "0171234567", ChemicalSafety.Localization.Lang.Bn);
                yield return new WaitForSeconds(0.8f);
                yield return Shot("00_login_bn_errors");
                Say("login: +88 phone accepted = " + login.SubmitForTest("Rahim Uddin", "+8801712345678", UnityEditor.EditorPrefs.GetBool("ChemSafetySmokeBangla", false) ? ChemicalSafety.Localization.Lang.Bn : ChemicalSafety.Localization.Lang.En));
                yield return WaitStage("salam", 30f);
            }

            yield return WaitStage("instruction");
            yield return new WaitForSeconds(1.5f);
            var cont =FindDeep(panel.transform, "ContinueButton");
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

            var voice = Get<AudioSource>(_director, "voice");
            var rig = Get<Transform>(_director, "rig");
            // controller-ray flow (PresentableInput): no stand-here marker, no drum label picking, no carrier button
            var rayMode = ChemicalSafety.Presentable.PresentableInput.AllowedIn(1);
            Say("SC01 input mode = " + (rayMode ? "ControllerRay" : "FingerTouch"));
            if (rayMode)
            {
                yield return WaitStage("explore");
                yield return new WaitForSeconds(0.8f);
                Say("ray: rig position = " + rig.position + " head=" + Camera.main.transform.position);
                var instr = Get<InstructionPanelView>(_director, "instructionPanel");
                Say("ray: instruction card distance from head = " +
                    Vector3.Distance(Camera.main.transform.position, instr.transform.position).ToString("0.00"));
                yield return Shot("02_ray_view");
            }
            else
            {
                var hs1 = Get<FloorHotspot>(_director, "hotspotSc01");
                Say("Hotspot_SC01 position = " + hs1.transform.position);
                hs1.Activate();
                yield return WaitStage("pickLabel");
                yield return new WaitForSeconds(0.5f);
                Say("voice clip after hotspot = " + (voice.clip != null ? voice.clip.name : "none") +
                    " playing=" + voice.isPlaying);
                yield return new WaitForSeconds(2.2f);
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
            }

            yield return WaitStage("mcq1");
            yield return new WaitForSeconds(2f);
            var mcq1 = Get<PresentableMcqPanel>(_director, "mcqSc01");
            Say("mcq1 open = " + mcq1.IsOpen);
            LayoutCheck("SC01 MCQ", mcq1.transform);
            yield return Shot("07_question");
            // no wrong popup / retry in Scenario 1: the answer is marked on the rows with a reason under each, then NEXT
            // EditorPref ChemSafetySc01Wrong: pick the wrong option (Faded label) instead of the right one. Run the smoke test twice to see both cases.
            var wrongRun = false;
#if UNITY_EDITOR
            wrongRun = UnityEditor.EditorPrefs.GetBool("ChemSafetySc01Wrong", false);
#endif
            Say("SC01 answer case = " + (wrongRun ? "WRONG (Row1 faded label)" : "CORRECT (Row4 GHS vs SDS matched)"));
            Click(mcq1.transform, wrongRun ? "Row1" : "Row4");
            var heard = "";
            var heardWrongNext = "";
            var sawCorrect1 = false;
            var waited = 0f;
            // right answer: the drum goes onto the trolley first (about 7 s), the voice starts together with NEXT
            var nextFrame = 0.8f;
            var frameNo = 0;
            while (waited < (wrongRun ? 8f : 22f))
            {
                // the trolley sequence as a strip of frames (Temp/Smoke/09t_N.png), to look for the drum / trolley overlap
                if (!wrongRun && frameNo < 9 && waited >= nextFrame)
                {
                    yield return Shot("09t_" + frameNo);
                    frameNo++;
                    nextFrame += 1.0f;
                }

                if (voice.clip != null && voice.clip.name.Contains("Sc01Complete"))
                    heard = voice.clip.name;
                if (voice.clip != null && voice.clip.name.Contains("Sc01WrongNext"))
                    heardWrongNext = voice.clip.name;
                if (voice.clip != null && voice.clip.name == "VO_Correct_bn")
                    sawCorrect1 = true;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            var reasonShown = 0;
            foreach (var tmp in mcq1.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
            {
                if (tmp.name.StartsWith("Reason") && tmp.gameObject.activeInHierarchy && tmp.text.Length > 0)
                    reasonShown++;
            }

            var nextShown = false;
            foreach (var tr1 in mcq1.GetComponentsInChildren<Transform>())
            {
                if (tr1.name == "NextButton" && tr1.gameObject.activeInHierarchy)
                    nextShown = true;
            }

            Say("SC01 reveal: reasons shown = " + reasonShown + "/4, NEXT visible = " + nextShown + ", wrong popup open = " +
                (FindDeep(mcq1.transform, "WrongPopup") != null && FindDeep(mcq1.transform, "WrongPopup").gameObject.activeInHierarchy));
            Say("SC01 vo: wrong-next clip = " + (heardWrongNext.Length > 0 ? heardWrongNext : "not heard") +
                ", scenario complete clip = " + (heard.Length > 0 ? heard : "not heard") + " (expected: " +
                (wrongRun ? "wrong-next only" : "complete") + ")");
            Say("scenario complete vo = " + (heard.Length > 0 ? heard : "NOT HEARD") + ", common correct-answer voice heard = " + sawCorrect1);
            Say("SC01 fancy MCQ = " + (mcq1.Current != null && mcq1.Current.fancy) +
                ", fx active = " + (mcq1.Fx != null && mcq1.Fx.Active) +
                ", chevrons = " + (mcq1.Fx != null ? mcq1.Fx.ChevronCount : 0) +
                ", comet = " + (mcq1.Fx != null ? mcq1.Fx.CometCount : 0));
            yield return Shot(wrongRun ? "09_wrong" : "09_correct");
            if (rayMode && !wrongRun)
            {
                var rt = Get<GameObject>(_director, "trolley");
                yield return new WaitForSeconds(4f);
                Say("ray: trolley active = " + rt.activeSelf + " pos=" + rt.transform.position);
                yield return Shot("09b_ray_trolley");
            }

            LayoutCheck(wrongRun ? "SC01 MCQ wrong reveal" : "SC01 MCQ correct reveal", mcq1.transform);
            Click(mcq1.transform, "NextButton");

#if UNITY_EDITOR
            // EditorPref ChemSafetySc01Only: login + Scenario 1 only, then stop (does NOT skip login)
            if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc01Only", false))
            {
                Say("SC01-only run (login + Scenario 1)");
                yield return new WaitForSeconds(1f);
                Say("DONE runtime errors/exceptions = " + _errors);
                UnityEditor.EditorApplication.isPlaying = false;
                yield break;
            }
#endif

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

            if (Get<bool>(_director, "sc02CalloutFlow"))
            {
                yield return Sc02CalloutSmoke();
#if UNITY_EDITOR
                // EditorPref ChemSafetySc02Only: login + Scenarios 1 and 2 only, then stop
                if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc02Only", false))
                {
                    Say("SC02-only run (login + Scenarios 1 and 2)");
                    yield return new WaitForSeconds(1f);
                    Say("DONE runtime errors/exceptions = " + _errors);
                    UnityEditor.EditorApplication.isPlaying = false;
                    yield break;
                }
#endif
                goto sc03Start;
            }

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

            var placement = Get<ContainerPlacement>(_director, "placement");
            var inspect = Get<ContainerInspect>(_director, "inspect");
            var doors = Get<CompartmentDoor[]>(_director, "doors");
            var chart = Get<ChartViewer>(_director, "chart");
            // controller-ray flow (PresentableInput): fixed stand, Take Decision box, inspect with CLOSE, popup, automatic placement
            var rayMode2 = PresentableInput.AllowedIn(2);
            Say("SC02 input mode = " + (rayMode2 ? "ControllerRay" : "FingerTouch"));
            if (rayMode2)
            {
                var dec = Get<Sc02DecisionBox>(_director, "sc02Decision");
                var popup2 = Get<LabelPickPopup>(_director, "sc02DestPopup");
                var wd = 0f;
                while (!dec.Visible && wd < 30f)
                {
                    wd += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return new WaitForSeconds(1.0f);
                var head2 = Camera.main.transform.position;
                var stand2 = Get<Transform>(_director, "sc02RayStand").position;
                Say("ray: head at the stand = " + Vector2.Distance(new Vector2(head2.x, head2.z), new Vector2(stand2.x, stand2.z)).ToString("0.00") + " m off");
                yield return Shot("11d_sc02_take_decision");
                LayoutCheck("SC02 Take Decision", dec.Card);
                dec.PressInspection();
                yield return new WaitForSeconds(0.6f);
                var icard = FindDeep(inspect.transform, "InspectCard");
                for (var ci = 0; ci < inspect.Count; ci++)
                {
                    Poke(inspect.PokeOf(ci));
                    yield return new WaitForSeconds(1.0f);
                    var spill2 = inspect.CheckCardFits();
                    Say("ray: card " + ci + " (" + inspect.HeldName + ") text fits = " + (spill2.Length == 0) +
                        (spill2.Length > 0 ? " FAIL spill: " + spill2 : ""));
                    if (ci == 0)
                        yield return Shot("13g_sc02_ray_inspect");
                    Click(icard, "CloseButton");
                    yield return new WaitForSeconds(0.7f);
                }

                var wd2 = 0f;
                while (!dec.Visible && wd2 < 10f)
                {
                    wd2 += Time.unscaledDeltaTime;
                    yield return null;
                }

                Say("ray: Take Decision back after all six inspected = " + dec.Visible);
                dec.PressContinue();
                yield return new WaitForSeconds(0.6f);
                var comp2 = new[] { 0, 1, 2, 3, 3, 3 };
                var order2 = new[] { 2, 0, 1, 3, 4, 5 };
                for (var oi = 0; oi < order2.Length; oi++)
                {
                    var c2 = order2[oi];
                    var wp = 0f;
                    // the previous container is still being stored (door closing) until the next pick is allowed again
                    while ((placement.IsBusy || inspect.HasHeld || !inspect.PokeOf(c2).Interactable) && wp < 15f)
                    {
                        wp += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    Poke(inspect.PokeOf(c2));
                    yield return new WaitForSeconds(1.2f);
                    Say("ray: container " + c2 + " held = " + inspect.HeldName + ", compartment popup open = " + popup2.IsOpen);
                    if (oi == 0)
                    {
                        LayoutCheck("SC02 compartment popup", popup2.transform);
                        yield return Shot("12e_sc02_popup");
                        var wrongIdx = comp2[c2] == 0 ? 1 : 0;
                        Click(popup2.transform, "Option" + (wrongIdx + 1));
                        yield return new WaitForSeconds(1.8f);
                        Say("ray: wrong compartment door state = " + doors[wrongIdx].State + ", still holding = " + inspect.HasHeld);
                        yield return Shot("12b_wrong_placement");
                        var wr = 0f;
                        while (!popup2.IsOpen && wr < 8f)
                        {
                            wr += Time.unscaledDeltaTime;
                            yield return null;
                        }

                        Say("ray: popup reopened after the warning = " + popup2.IsOpen + ", placed = " + placement.Placed);
                    }

                    Click(popup2.transform, "Option" + (comp2[c2] + 1));
                    var before2 = placement.Placed;
                    yield return new WaitForSeconds(1.0f);
                    if (oi == 0)
                        yield return Shot("12f_sc02_flight");
                    var tw2 = 0f;
                    while (placement.Placed == before2 && tw2 < 10f)
                    {
                        tw2 += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    Say("ray: placed " + placement.Placed + " / " + placement.Total);
                    if (oi == 5)
                        yield return Shot("12d_placed_6");
                }
            }
            else
            {
                var hsPick = Get<FloorHotspot>(_director, "hotspotSc02Pick");
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
            }

            var mcq2 = Get<PresentableMcqPanel>(_director, "mcqSc02");
            var t = 0f;
            while (!mcq2.IsOpen && t < 20f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(2.4f); // the option rows slide in one after the other; measure when they have settled
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
            sc03Start:
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

            if (PresentableInput.AllowedIn(3))
            {
                // controller-ray flow: one stand, automatic scan and report, inline-reveal question, protocol animation
                yield return Sc03RaySmoke(mcq3, det);
#if UNITY_EDITOR
                // EditorPref ChemSafetySc03Only: login + Scenarios 1, 2 and 3 only, then stop
                if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc03Only", false))
                {
                    Say("SC03-only run (login + Scenarios 1 to 3)");
                    yield return new WaitForSeconds(1f);
                    Say("DONE runtime errors/exceptions = " + _errors);
                    UnityEditor.EditorApplication.isPlaying = false;
                    yield break;
                }
#endif
                goto sc04Start;
            }

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
            sc04Start:
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
            if (PresentableInput.AllowedIn(4))
            {
                // controller-ray flow: no arrows, each glove is shown by itself, then the gate and the inline-reveal question
                yield return Sc04RaySmoke(gloves);
#if UNITY_EDITOR
                // EditorPref ChemSafetySc04Only: stop after Scenario 4 (login + Scenarios 1 to 4)
                if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc04Only", false))
                {
                    Say("SC04-only run (login + Scenarios 1 to 4)");
                    yield return new WaitForSeconds(1f);
                    Say("DONE runtime errors/exceptions = " + _errors);
                    UnityEditor.EditorApplication.isPlaying = false;
                    yield break;
                }
#endif
                goto sc05Start;
            }

            for (var g = 0; g < 3; g++)
            {
                var covers = gloves.MeshWithinReach(g);
                Say("SC04 glove " + g + " mesh in reach = " + covers);
                if (!covers)
                    Debug.LogError("[SMOKE] SC04 glove " + g + " collider does not cover the visible mesh");
                var arrow4 = gloves.ArrowOf(g);
                var armed4 = arrow4 != null && arrow4.IsArmed;
                Say("SC04 glove " + g + " floating arrow armed = " + armed4);
                if (!armed4)
                    Debug.LogError("[SMOKE] SC04 glove " + g + " has no armed floating arrow");
                var gb = gloves.GloveBounds(g);
                var cl = GameObject.Find("Cloth");
                if (cl != null)
                {
                    var cb = cl.GetComponent<Renderer>().bounds;
                    var inside = gb.min.x >= cb.min.x && gb.max.x <= cb.max.x && gb.min.z >= cb.min.z && gb.max.z <= cb.max.z &&
                                 gb.min.y >= cb.max.y - 0.02f;
                    Say("SC04 glove " + g + " fully on the cloth = " + inside + " glove " + gb.min.ToString("F2") + gb.max.ToString("F2") +
                        " cloth " + cb.min.ToString("F2") + cb.max.ToString("F2"));
                    if (!inside)
                        Debug.LogError("[SMOKE] SC04 glove " + g + " is not fully on the cloth");
                }

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
                var hb = gloves.GloveBounds(g);
                var upright = hb.size.y > hb.size.z;
                Say("SC04 held glove " + g + " upright = " + upright + " size " + hb.size.ToString("F2"));
                if (!upright)
                    Debug.LogError("[SMOKE] SC04 held glove " + g + " is not upright");
                var cardT = FindDeep(gloves.transform, "GloveCard");
                if (cardT != null)
                    LayoutCheck("SC04 glove card " + g, cardT);
                if (g == 2)
                    yield return Shot("31b_sc04_glove_card");
                Click(gloves.transform, "CloseButton");
                var waitBack = 0f;
                while (gloves.Holding && waitBack < 3f)
                {
                    waitBack += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            yield return WaitStage("sc04Gate", 20f);
            var gate4 = Get<Sc06Gate>(_director, "sc04Gate");
            var gateWait = 0f;
            while (!gate4.Visible && gateWait < 8f)
            {
                gateWait += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.6f);
            Say("SC04 gloves seen = " + gloves.Seen + " / 3, gate visible = " + gate4.Visible);
            Say("SC04 gate: head z = " + Camera.main.transform.position.z.ToString("0.00") + " (expected about 3.15: back from the glove table)");
            LayoutCheck("SC04 gate", gate4.Card);
            yield return Shot("32_sc04_gate");
            gate4.PressNext();
            yield return WaitStage("sc04Question", 20f);
            var mcq4 = Get<PresentableMcqPanel>(_director, "mcqSc04");
            var q4 = 0f;
            while (!mcq4.IsOpen && q4 < 8f)
            {
                q4 += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(2.4f);
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

            // Scenario 5 (dyeing factory) follows the SC04 NEXT
            sc05Start:
            PresentableMcqPanel mcq5 = null;
            yield return WaitStage("sc05Intro", 30f);
            if (PresentableInput.AllowedIn(5))
            {
                // controller-ray flow: a small instruction card, the masks show by themselves, gate, inline-reveal question
                yield return Sc05RaySmoke(voiceSrc);
                mcq5 = Get<PresentableMcqPanel>(_director, "mcqSc05");
#if UNITY_EDITOR
                // EditorPref ChemSafetySc05Only: also stops here if the long chain somehow reaches SC05 with the pref set
                if (UnityEditor.EditorPrefs.GetBool("ChemSafetySc05Only", false))
                {
                    Say("SC05-only run (stop after Scenario 5)");
                    yield return new WaitForSeconds(1f);
                    Say("DONE runtime errors/exceptions = " + _errors);
                    UnityEditor.EditorApplication.isPlaying = false;
                    yield break;
                }
#endif
                goto sc06Start;
            }

            var amb = Get<Sc05Ambience>(_director, "sc05Ambience");
            yield return new WaitForSeconds(2.5f);
            Say("SC05 machine sound on = " + amb.IsOn + ", voice playing = " + voiceSrc.isPlaying + ", target volume under the voice = " +
                amb.TargetVolume.ToString("0.00") + " (expected about 0.09)");
            yield return Shot("40_sc05_instruction");
            var cont5 = FindDeep(panel.transform, "ContinueButton");
            var w5 = 0f;
            while (cont5 != null && !cont5.gameObject.activeSelf && w5 < 40f)
            {
                w5 += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.4f);
            Say("SC05 voice ended: playing = " + voiceSrc.isPlaying + ", target volume = " + amb.TargetVolume.ToString("0.00") +
                " (expected 0.32 or the hint voice 0.09), fastest fan = " +
                FindFirstObjectByType<Sc05FactoryFx>(FindObjectsInactive.Include).FastestSpin.ToString("0") + " deg/s");
            Click(panel.transform, "ContinueButton");
            yield return WaitStage("sc05Explore", 20f);
            var masks5 = Get<MaskInspect>(_director, "masks");
            yield return new WaitForSeconds(0.8f);
            yield return Shot("41_sc05_rail");
            var camT5 = Camera.main.transform;
            var keep5 = camT5.localRotation;
            camT5.localRotation = Quaternion.Euler(-4f, -55f, 0f);
            yield return Shot("47_sc05_left_hall");
            camT5.localRotation = Quaternion.Euler(-12f, -100f, 0f);
            yield return Shot("48_sc05_far_left");
            camT5.localRotation = Quaternion.Euler(-35f, -30f, 0f);
            yield return Shot("49_sc05_ceiling");
            camT5.localRotation = keep5;
            for (var k = 0; k < masks5.Count; k++)
            {
                var idx = masks5.CurrentIndex;
                var reach = masks5.MeshWithinReach(idx);
                Say("SC05 mask " + idx + " mesh in reach = " + reach);
                if (!reach)
                    Debug.LogError("[SMOKE] SC05 mask " + idx + " collider does not cover the visible mesh");
                var pk = masks5.PokeOf(idx);
                if (pk != null)
                    Poke(pk);
                var wm = 0f;
                while (!masks5.Holding && wm < 3f)
                {
                    wm += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return new WaitForSeconds(0.7f);
                Say("SC05 holding " + masks5.HeldIndex + ": card = [" + masks5.CardName + "] / [" + masks5.CardCompat + "], voice = " +
                    (voiceSrc.isPlaying && voiceSrc.clip != null ? voiceSrc.clip.name : "none") + ", machine target volume = " +
                    amb.TargetVolume.ToString("0.00"));
                var pictureName = masks5.CardPicture != null ? masks5.CardPicture.name : "none";
                Say("SC05 mask " + idx + ": picture = " + pictureName + " (expected MaskIcon_" + idx + "), workflow style = " +
                    masks5.WorkflowStyle + ", overlaps another mask = " + masks5.HeldOverlapsOthers() + " (expected False)");
                LayoutCheck("SC05 mask card " + idx, masks5.CardTransform);
                yield return Shot("42_sc05_mask_" + idx);
                // the speaker on the card plays the voice-over again
                voiceSrc.Stop();
                yield return null;
                Click(masks5.transform, "SpeakerButton");
                yield return new WaitForSeconds(0.5f);
                Say("SC05 mask " + idx + " speaker click: voice = " +
                    (voiceSrc.isPlaying && voiceSrc.clip != null ? voiceSrc.clip.name : "none"));
                Click(masks5.transform, "CloseButton");
                yield return new WaitForSeconds(0.2f);
                Say("SC05 mask " + idx + " after close: voice playing = " + voiceSrc.isPlaying + " (expected False)");
                var wb = 0f;
                while (masks5.Holding && wb < 3f)
                {
                    wb += Time.unscaledDeltaTime;
                    yield return null;
                }

                yield return new WaitForSeconds(0.3f);
            }

            var gate5 = 0f;
            var next5 = FindDeep(masks5.transform, "NextButton");
            while ((next5 == null || !next5.gameObject.activeInHierarchy) && gate5 < 8f)
            {
                gate5 += Time.unscaledDeltaTime;
                next5 = FindDeep(masks5.transform, "NextButton");
                yield return null;
            }

            Say("SC05 masks seen = " + masks5.Seen + " / " + masks5.Count);
            LayoutCheck("SC05 gate card", masks5.GateTransform);
            yield return Shot("43_sc05_gate");
            Click(masks5.transform, "NextButton");
            yield return WaitStage("sc05Question", 20f);
            mcq5 = Get<PresentableMcqPanel>(_director, "mcqSc05");
            var q5 = 0f;
            while (!mcq5.IsOpen && q5 < 8f)
            {
                q5 += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(2.4f);
            LayoutCheck("SC05 MCQ", mcq5.transform);
            yield return Shot("44_sc05_question");
            // rows are named Row1..Row4 (A..D): A (N95) is wrong, B (half-face) is right
            Click(mcq5.transform, "Row1");
            yield return new WaitForSeconds(1.6f);
            yield return Shot("45_sc05_wrong");
            Click(mcq5.transform, "Close");
            yield return new WaitForSeconds(1.2f);
            Click(mcq5.transform, "Row2");
            var heard5 = "";
            var h5 = 0f;
            while (h5 < 14f)
            {
                if (voiceSrc.clip != null && voiceSrc.clip.name.Contains("Sc05Complete"))
                    heard5 = voiceSrc.clip.name;
                if (h5 > 1.2f && h5 < 1.3f)
                    LayoutCheck("SC05 MCQ correct card", mcq5.transform);
                h5 += Time.unscaledDeltaTime;
                yield return null;
            }

            Say("scenario 5 complete vo = " + (heard5.Length > 0 ? heard5 : "NOT HEARD"));
            yield return Shot("46_sc05_correct");
            Click(mcq5.transform, "NextButton");

            // Scenario 6 (chemistry lab, fume hood) follows the SC05 NEXT
            sc06Start:
            yield return Sc06Smoke(panel, voiceSrc, mcq5);
            yield return Sc07Dispatch(panel, voiceSrc);
            yield return Sc08Smoke(panel, voiceSrc);
            yield return Sc09Smoke(panel, voiceSrc);
            yield return Sc10Smoke(panel, voiceSrc);
            yield return Sc11Smoke(panel, voiceSrc);
            yield return Sc12Smoke(panel, voiceSrc);
            yield return ScCheckSmoke();
            yield return WaitStage("complete", 30f);
            yield return new WaitForSeconds(1f);
            yield return Shot("12_complete");

            Say("DONE runtime errors/exceptions = " + _errors);
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
