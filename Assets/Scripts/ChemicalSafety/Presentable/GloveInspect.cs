using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 4: one pulsing arrow moves left to right across three gloves. Poking the marked glove
    /// brings it to the hand and opens a properties card at eye level on the right. The other two stay put.
    /// </summary>
    public sealed class GloveInspect : MonoBehaviour
    {
        [SerializeField] private PresentableArt art;
        [SerializeField] private Transform[] gloves = new Transform[3];
        [SerializeField] private Transform arrow;

        private readonly bool[] _seen = new bool[3];
        private Vector3[] _homePos;
        private Quaternion[] _homeRot;
        private PokeTarget[] _pokes;
        private int _current;
        private int _held = -1;
        private bool _busy;
        private bool _on;
        private Transform _card;
        private TMP_Text _cardBody;
        private Transform _gate;

        public bool NextChosen { get; private set; }
        public int Seen { get; private set; }
        public bool Holding => _held >= 0;

        private void Start()
        {
            _homePos = new Vector3[gloves.Length];
            _homeRot = new Quaternion[gloves.Length];
            _pokes = new PokeTarget[gloves.Length];
            for (var i = 0; i < gloves.Length; i++)
            {
                if (gloves[i] == null)
                    continue;
                _homePos[i] = gloves[i].position;
                _homeRot[i] = gloves[i].rotation;
                _pokes[i] = gloves[i].GetComponent<PokeTarget>();
                if (_pokes[i] != null)
                {
                    var index = i;
                    _pokes[i].Poked += _ => OnPoke(index);
                    _pokes[i].SetInteractable(false);
                }
            }

            BuildCard();
            BuildGate();
            if (arrow != null)
                arrow.gameObject.SetActive(false);
        }

        public void Begin()
        {
            NextChosen = false;
            Seen = 0;
            for (var i = 0; i < _seen.Length; i++)
                _seen[i] = false;
            _on = true;
            _current = 0;
            HideCard();
            HideGate();
            PlaceArrow();
            SetPokes(true);
        }

        private void Update()
        {
            if (arrow == null || !arrow.gameObject.activeSelf)
                return;
            var s = 1f + 0.06f * Mathf.Sin(Time.time * 2.2f);
            arrow.localScale = new Vector3(0.16f, 0.22f, 1f) * s;
        }

        private void OnPoke(int index)
        {
            if (!_on || _busy || _held >= 0 || index != _current)
                return;
            StartCoroutine(Hold(index));
        }

        private IEnumerator Hold(int index)
        {
            _busy = true;
            _held = index;
            SetPokes(false);
            if (arrow != null)
                arrow.gameObject.SetActive(false);
            var glove = gloves[index];
            var from = glove.position;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            var dest = (cam != null ? cam.position : from) + fwd * 0.5f - right * 0.16f + Vector3.down * 0.08f;
            for (var t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                glove.position = Vector3.Lerp(from, dest, Mathf.SmoothStep(0f, 1f, t / 0.4f));
                yield return null;
            }

            glove.position = dest;
            ShowCard(index);
            _busy = false;
        }

        public void CloseCard()
        {
            if (_held < 0 || _busy)
                return;
            StartCoroutine(PutBack(_held));
        }

        private IEnumerator PutBack(int index)
        {
            _busy = true;
            HideCard();
            var glove = gloves[index];
            var from = glove.position;
            for (var t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                glove.position = Vector3.Lerp(from, _homePos[index], Mathf.SmoothStep(0f, 1f, t / 0.35f));
                yield return null;
            }

            glove.SetPositionAndRotation(_homePos[index], _homeRot[index]);
            _held = -1;
            _busy = false;
            if (!_seen[index])
            {
                _seen[index] = true;
                Seen++;
            }

            if (Seen >= gloves.Length)
            {
                _on = false;
                ShowGate();
            }
            else
            {
                _current = NextUnseen();
                PlaceArrow();
                SetPokes(true);
            }
        }

        private int NextUnseen()
        {
            for (var i = 0; i < _seen.Length; i++)
                if (!_seen[i])
                    return i;
            return 0;
        }

        private void SetPokes(bool on)
        {
            for (var i = 0; i < _pokes.Length; i++)
                if (_pokes[i] != null)
                    _pokes[i].SetInteractable(on && i == _current);
        }

        private void PlaceArrow()
        {
            if (arrow == null || _current < 0 || _current >= gloves.Length || gloves[_current] == null)
                return;
            var b = BoundsOf(gloves[_current]);
            arrow.gameObject.SetActive(true);
            arrow.position = new Vector3(b.center.x, b.max.y + 0.18f, b.center.z);
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            arrow.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        private void ShowCard(int index)
        {
            if (_card == null)
                return;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            _card.position = (cam != null ? cam.position : Vector3.zero) + fwd * 0.72f + right * 0.28f;
            _card.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            _cardBody.text = "MATERIAL\n" + Sc04Content.Materials[index] +
                             "\n\nRATING\n" + Sc04Content.Ratings[index] +
                             "\n\nPERMEATION TIME\n" + Sc04Content.Times[index];
            _card.gameObject.SetActive(true);
        }

        private void HideCard()
        {
            if (_card != null)
                _card.gameObject.SetActive(false);
        }

        private void ShowGate()
        {
            if (arrow != null)
                arrow.gameObject.SetActive(false);
            if (_gate == null)
                return;
            var cam = Camera.main != null ? Camera.main.transform : null;
            var fwd = cam != null ? Flat(cam.forward) : Vector3.forward;
            _gate.position = (cam != null ? cam.position : Vector3.zero) + fwd * 0.75f;
            _gate.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            _gate.gameObject.SetActive(true);
        }

        private void HideGate()
        {
            if (_gate != null)
                _gate.gameObject.SetActive(false);
        }

        private void OnBack()
        {
            HideGate();
            Begin();
        }

        private void OnNext()
        {
            NextChosen = true;
            HideGate();
        }

        private void BuildCard()
        {
            var canvas = PresentableUi.WorldCanvas("GloveCard", transform, new Vector2(720f, 520f), 0.00058f, true, 30);
            _card = canvas.transform;
            var frame = PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, art.roundedPanel,
                new Color(0.55f, 0.9f, 1f, 0.92f));
            frame.raycastTarget = true;
            PresentableUi.Text("Title", canvas.transform, new Vector2(0.06f, 0.82f), new Vector2(0.78f, 0.96f),
                "GLOVE PROPERTIES", 34f, Color.white, TextAlignmentOptions.Left, true, art.englishFont);
            var body = PresentableUi.Rect("Body", canvas.transform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.8f));
            _cardBody = PresentableUi.Text(body, "", 30f, Color.white, TextAlignmentOptions.TopLeft, false, art.englishFont);
            _cardBody.margin = new Vector4(8f, 8f, 8f, 8f);
            var closeRt = PresentableUi.Centered("CloseButton", canvas.transform, new Vector2(300f, 210f), new Vector2(72f, 72f));
            PresentableUi.Img(closeRt, art.cross != null ? art.cross : art.circle, Color.white, raycast: true);
            var close = closeRt.gameObject.AddComponent<Button>();
            close.targetGraphic = closeRt.GetComponent<Image>();
            close.onClick.AddListener(CloseCard);
            PresentableUi.AlwaysOnTop(canvas);
            _card.gameObject.SetActive(false);
        }

        private void BuildGate()
        {
            var canvas = PresentableUi.WorldCanvas("GloveGate", transform, new Vector2(860f, 360f), 0.00062f, true, 31);
            _gate = canvas.transform;
            PresentableUi.Img("Frame", canvas.transform, Vector2.zero, Vector2.one, art.roundedPanel,
                new Color(0.55f, 0.9f, 1f, 0.92f));
            PresentableUi.Text("Title", canvas.transform, new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.92f),
                Sc04Content.GateTitle, 32f, Color.white, TextAlignmentOptions.Center, true, art.englishFont);
            PresentableUi.Text("Body", canvas.transform, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.62f),
                Sc04Content.GateBody, 24f, Color.white, TextAlignmentOptions.Center, false, art.englishFont);
            MakeButton("BackButton", canvas.transform, new Vector2(-180f, -90f), "BACK", OnBack);
            MakeButton("NextButton", canvas.transform, new Vector2(180f, -90f), "NEXT", OnNext);
            PresentableUi.AlwaysOnTop(canvas);
            _gate.gameObject.SetActive(false);
        }

        private void MakeButton(string name, Transform parent, Vector2 pos, string label, UnityEngine.Events.UnityAction action)
        {
            var rt = PresentableUi.Centered(name, parent, pos, new Vector2(280f, 88f));
            var img = PresentableUi.Img(rt, art.button, Color.white, raycast: true);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(action);
            PresentableUi.Text("Label", rt, Vector2.zero, Vector2.one, label, 28f, new Color(0.05f, 0.12f, 0.16f),
                TextAlignmentOptions.Center, true, art.englishFont);
        }

        private static Bounds BoundsOf(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0)
                return new Bounds(t.position, Vector3.one * 0.2f);
            var b = rs[0].bounds;
            for (var i = 1; i < rs.Length; i++)
                b.Encapsulate(rs[i].bounds);
            return b;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
        }
    }
}
