using System;
using System.Collections;
using System.Collections.Generic;
using ChemicalSafety.Localization;
using ChemicalSafety.Results;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// First screen of the game: name, e-mail or phone, language toggle, START. Built at run time from the sprites in
    /// Resources/Login (Tools/generate_login_ui.py) and the texts in Resources/Content/LOGIN.json. Input is finger-only
    /// touch (a touch keyboard on a second panel; the desk keyboard also works in the editor). A lab backdrop, floating
    /// 3D molecules, glowing tubes with bubbles and sparks surround the panel.
    /// </summary>
    public sealed class LoginScreen : MonoBehaviour
    {
        private const string Scn = "LOGIN";
        private const float Px = 0.0009f; // metres per canvas pixel
        private const float PanelDistance = 0.74f; // inside arm's reach so the fingertip can touch it
        private const float PanelDrop = 0.06f; // centre a little below the eyes
        private const float ArcDegrees = 112f;
        private const float ArcRadius = 2.25f;
        private const float ArcHeight = 2.3f;

        [SerializeField] private PresentableArt art;
        [SerializeField] private Material backdropMat;
        [SerializeField] private Material domeMat;
        [SerializeField] private Material atomMat;
        [SerializeField] private Material glowMat;

        private enum Field { None, Name, Contact }

        private static readonly Color TitleCol = new Color(0.75f, 0.85f, 1f);
        private static readonly Color SubCol = new Color(0.5f, 0.69f, 1f);
        private static readonly Color LabelCol = new Color(0.78f, 0.82f, 0.9f);
        private static readonly Color ErrCol = new Color(1f, 0.45f, 0.4f);

        private Transform _root;
        private Transform _head;
        private float _floorY;
        private bool _done;
        private bool _closing;

        private Field _focus = Field.None;
        private string _name = string.Empty;
        private string _contact = string.Empty;
        private TextMeshProUGUI _nameTmp;
        private TextMeshProUGUI _contactTmp;
        private LocText _nameHint;
        private LocText _contactHint;
        private GameObject _nameFocusImg;
        private GameObject _contactFocusImg;
        private GameObject _nameErrImg;
        private GameObject _contactErrImg;
        private LocText _nameErr;
        private LocText _contactErr;
        private float _nameErrUntil;
        private bool _shift;
        private CanvasGroup _kbGroup;
        private readonly List<KeyView> _keys = new List<KeyView>();
        private RectTransform _toggleOn;
        private LocText _langBn;
        private LocText _langEn;
        private Image _startHot;
        private RectTransform _startRt;
        private readonly List<CanvasGroup> _groups = new List<CanvasGroup>();
        private readonly List<Transform> _molecules = new List<Transform>();
        private readonly List<float> _molPhase = new List<float>();
        private readonly List<Transform> _rings = new List<Transform>();
        private float _enterT;
        private float _toggleX;

        private sealed class KeyView
        {
            public Button button;
            public TextMeshProUGUI label;
            public char ch;
            public bool isLetter;
            public bool isDigit;
            public Image image;
        }

        public bool IsDone => _done;

        /// <summary>Shows the login screen around the head, runs until START is accepted, then removes it.</summary>
        public IEnumerator Run(Transform head, float floorY, ScreenFader fader)
        {
            _head = head;
            _floorY = floorY;
            _done = false;
            _closing = false;
            _focus = Field.None;
            _name = string.Empty;
            _contact = string.Empty;
            Loc.SetLanguage(Lang.En);
            Build();
            Subscribe(true);
            yield return fader.FadeTo(0f, 1.0f);
            while (!_done)
                yield return null;
            Subscribe(false);
            yield return fader.FadeTo(1f, 0.6f);
            Close();
        }

        /// <summary>Removes everything the screen created (called after the fade to black).</summary>
        public void Close()
        {
            if (_root != null)
                Destroy(_root.gameObject);
            _root = null;
            _keys.Clear();
            _groups.Clear();
            _molecules.Clear();
            _molPhase.Clear();
            _rings.Clear();
        }

        /// <summary>Test hook (smoke test): fills the fields and presses START.</summary>
        public bool SubmitForTest(string name, string contact, Lang lang)
        {
            Loc.SetLanguage(lang);
            _name = name;
            _contact = contact;
            Refresh();
            return TrySubmit();
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            var fwd = Vector3.ProjectOnPlane(_head.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f)
                fwd = Vector3.forward;
            var rootRot = Quaternion.LookRotation(fwd, Vector3.up);
            _root = new GameObject("LoginStage").transform;
            _root.SetPositionAndRotation(_head.position, rootRot);

            BuildEnvironment();
            BuildPanel();
            BuildKeyboardAndLogos();
            BuildLights();
            Refresh();
            _enterT = 0f;
        }

        private Vector3 Local(Vector3 p) => _root.TransformPoint(p);

        private Canvas MakeCanvas(string name, Vector2 size, Vector3 localPos, float pitch)
        {
            var canvas = PresentableUi.WorldCanvas(name, _root, size, Px, true, 5);
            var t = canvas.transform;
            t.position = Local(localPos);
            var dir = (Local(localPos) - _head.position).normalized;
            t.rotation = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f);
            var g = canvas.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            _groups.Add(g);
            return canvas;
        }

        private static RectTransform Place(Transform parent, string name, float x0, float y0, float x1, float y1, float cw, float ch)
        {
            var rt = PresentableUi.Centered(name, parent, new Vector2((x0 + x1) * 0.5f - cw * 0.5f, ch * 0.5f - (y0 + y1) * 0.5f),
                new Vector2(x1 - x0, y1 - y0));
            return rt;
        }

        private static Sprite Spr(string n) => Resources.Load<Sprite>("Login/" + n);

        private Image Pic(RectTransform rt, string sprite, Color? c = null)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Spr(sprite);
            img.color = c ?? Color.white;
            img.raycastTarget = false;
            return img;
        }

        private LocText Label(Transform parent, string name, float x0, float y0, float x1, float y1, float cw, float ch,
            string key, float font, Color color, bool bold, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Place(parent, name, x0, y0, x1, y1, cw, ch);
            var lt = LocText.Create(rt, art != null ? art.englishFont : null, font, color, align, bold);
            lt.Bind(Scn, key);
            return lt;
        }

        private Button MakeButton(RectTransform rt, Action onClick)
        {
            var img = rt.gameObject.GetComponent<Image>();
            if (img == null)
            {
                img = rt.gameObject.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0f);
            }

            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            rt.gameObject.AddComponent<HoverLift>().Init(rt);
            return b;
        }

        private void BuildPanel()
        {
            const float cw = 860f, ch = 860f;
            var canvas = MakeCanvas("LoginPanel", new Vector2(cw, ch), new Vector3(0f, -PanelDrop, PanelDistance), 0f);
            var host = canvas.transform;

            Pic(PresentableUi.Rect("Frame", host, Vector2.zero, Vector2.one), "Login_Panel");
            Label(host, "Title", 40, 52, 820, 126, cw, ch, "title", 62f, TitleCol, true);
            Label(host, "Subtitle", 40, 126, 820, 178, cw, ch, "subtitle", 38f, SubCol, true);
            Label(host, "Welcome", 40, 214, 820, 272, cw, ch, "welcome", 46f, new Color(0.78f, 0.8f, 0.9f), true);

            Pic(Place(host, "IconUser", 112, 296, 222, 406, cw, ch), "Login_IconUser");
            Label(host, "NameLabel", 240, 290, 735, 328, cw, ch, "name_label", 28f, LabelCol, true, TextAlignmentOptions.Left);
            BuildField(host, cw, ch, 330, Field.Name, "name_hint", out _nameTmp, out _nameHint, out _nameFocusImg, out _nameErrImg);
            _nameErr = Label(host, "NameError", 240, 404, 820, 442, cw, ch, "err_name_empty", 28f, ErrCol, false,
                TextAlignmentOptions.Left);
            _nameErr.gameObject.SetActive(false);

            Pic(Place(host, "IconMail", 112, 456, 222, 540, cw, ch), "Login_IconMail");
            Label(host, "ContactLabel", 240, 436, 735, 472, cw, ch, "contact_label", 28f, LabelCol, true, TextAlignmentOptions.Left);
            BuildField(host, cw, ch, 472, Field.Contact, "contact_hint", out _contactTmp, out _contactHint, out _contactFocusImg,
                out _contactErrImg);
            _contactErr = Label(host, "ContactError", 240, 546, 820, 584, cw, ch, "err_contact_empty", 28f, ErrCol, false,
                TextAlignmentOptions.Left);
            _contactErr.gameObject.SetActive(false);

            // START
            _startRt = Place(host, "Start", 78, 550, 782, 700, cw, ch);
            // sprite has 40 px halo margins: centre it on the 624x70 button rect (118..742, 585..655)
            _startRt.anchoredPosition = new Vector2(430f - 430f, 430f - 620f);
            _startRt.sizeDelta = new Vector2(704f, 150f);
            Pic(_startRt, "Login_Start");
            var hotRt = PresentableUi.Rect("Hot", _startRt, Vector2.zero, Vector2.one);
            _startHot = Pic(hotRt, "Login_StartHot", new Color(1f, 1f, 1f, 0f));
            var startLabel = PresentableUi.Rect("Label", _startRt, new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.8f));
            var lt = LocText.Create(startLabel, art != null ? art.englishFont : null, 48f, new Color(0.4f, 0.85f, 0.9f),
                TextAlignmentOptions.Center, true);
            lt.Bind(Scn, "start");
            var hit = PresentableUi.Rect("Hit", _startRt, new Vector2(0.06f, 0.22f), new Vector2(0.94f, 0.78f));
            MakeButton(hit, OnStart);

            // language toggle
            var bg = Place(host, "ToggleBg", 118, 700, 512, 754, cw, ch);
            Pic(bg, "Login_ToggleBg");
            var onRt = PresentableUi.Centered("ToggleOn", bg, new Vector2(-98f, 0f), new Vector2(230f, 84f));
            Pic(onRt, "Login_ToggleOn");
            _toggleOn = onRt;
            _toggleX = 98f; // English is the default (right segment)
            onRt.anchoredPosition = new Vector2(_toggleX, 0f);
            _langBn = Label(bg, "LangBn", 6, 6, 196, 48, 394f, 54f, "lang_bn", 28f, Color.white, true);
            _langEn = Label(bg, "LangEn", 198, 6, 388, 48, 394f, 54f, "lang_en", 28f, Color.white, true);
            var hitBn = PresentableUi.Rect("HitBn", bg, new Vector2(0f, 0f), new Vector2(0.5f, 1f));
            MakeButton(hitBn, () => SetLanguage(Lang.Bn));
            var hitEn = PresentableUi.Rect("HitEn", bg, new Vector2(0.5f, 0f), new Vector2(1f, 1f));
            MakeButton(hitEn, () => SetLanguage(Lang.En));

            Label(host, "DevBy", 520, 716, 700, 744, cw, ch, "developed_by", 22f, new Color(0.8f, 0.85f, 0.92f), true,
                TextAlignmentOptions.Right);
            var badge = Place(host, "Bmqas", 704, 688, 800, 770, cw, ch);
            var bi = Pic(badge, "Login_Bmqas");
            bi.preserveAspect = true;
        }

        private void BuildField(Transform host, float cw, float ch, float top, Field which, string hintKey, out TextMeshProUGUI valueTmp,
            out LocText hint, out GameObject focusImg, out GameObject errImg)
        {
            var rt = Place(host, which + "Field", 240, top, 736, top + 72, cw, ch);
            Pic(rt, "Login_Field");
            var f = PresentableUi.Centered("Focus", rt, Vector2.zero, new Vector2(556f, 132f));
            Pic(f, "Login_FieldFocus");
            f.gameObject.SetActive(false);
            focusImg = f.gameObject;
            var e = PresentableUi.Centered("Error", rt, Vector2.zero, new Vector2(556f, 132f));
            Pic(e, "Login_FieldError");
            e.gameObject.SetActive(false);
            errImg = e.gameObject;

            var vRt = PresentableUi.Rect("Value", rt, Vector2.zero, Vector2.one, new Vector2(20f, 4f), new Vector2(-16f, -4f));
            valueTmp = vRt.gameObject.AddComponent<TextMeshProUGUI>();
            if (art != null && art.englishFont != null)
                valueTmp.font = art.englishFont;
            valueTmp.fontSize = 34f;
            valueTmp.color = Color.white;
            valueTmp.alignment = TextAlignmentOptions.MidlineLeft;
            valueTmp.textWrappingMode = TextWrappingModes.NoWrap;
            valueTmp.enableAutoSizing = true;
            valueTmp.fontSizeMin = 18f;
            valueTmp.fontSizeMax = 34f;
            valueTmp.raycastTarget = false;

            var hRt = PresentableUi.Rect("Hint", rt, Vector2.zero, Vector2.one, new Vector2(20f, 4f), new Vector2(-16f, -4f));
            hint = LocText.Create(hRt, art != null ? art.englishFont : null, 32f, new Color(0.8f, 0.88f, 0.94f, 0.78f),
                TextAlignmentOptions.MidlineLeft, false);
            hint.Bind(Scn, hintKey);

            var hit = PresentableUi.Rect("Hit", rt, Vector2.zero, Vector2.one);
            MakeButton(hit, () => Focus(which));
        }

        private void BuildKeyboardAndLogos()
        {
            // partner logos under the panel
            var logoCanvas = MakeCanvas("LoginLogos", new Vector2(860f, 120f), new Vector3(0f, -0.485f, PanelDistance - 0.04f), -8f);
            var logo = PresentableUi.Rect("Logos", logoCanvas.transform, Vector2.zero, Vector2.one);
            var li = logo.gameObject.AddComponent<Image>();
            li.sprite = Spr("Login_Logos");
            li.preserveAspect = true;
            li.raycastTarget = false;

            // touch keyboard
            const float cw = 860f, ch = 470f;
            var kb = MakeCanvas("LoginKeyboard", new Vector2(cw, ch), new Vector3(0f, -0.77f, PanelDistance - 0.12f), -22f);
            _kbGroup = kb.GetComponent<CanvasGroup>();
            _groups.Remove(_kbGroup); // the keyboard fades with focus, not with the entrance
            _kbGroup.alpha = 0f;
            _kbGroup.blocksRaycasts = false;
            var host = kb.transform;

            AddRow(host, ch, 0, new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" }, null);
            AddRow(host, ch, 1, new[] { "q", "w", "e", "r", "t", "y", "u", "i", "o", "p" }, null);
            AddRow(host, ch, 2, new[] { "a", "s", "d", "f", "g", "h", "j", "k", "l", "'" }, null);
            AddRow(host, ch, 3, new[] { "SHIFT", "z", "x", "c", "v", "b", "n", "m", "BACK" }, new[] { 1.5f, 1, 1, 1, 1, 1, 1, 1, 1.5f });
            AddRow(host, ch, 4, new[] { "@", ".", "_", "-", "+", "SPACE", "DONE" }, new[] { 1, 1, 1, 1, 1, 3f, 2f });
            RefreshKeys();
        }

        private void AddRow(Transform host, float ch, int row, string[] keys, float[] widths)
        {
            const float unit = 76f, gap = 8f;
            var total = 0f;
            for (var i = 0; i < keys.Length; i++)
            {
                var u = widths != null ? widths[i] : 1f;
                total += u * unit + (u - 1f) * gap;
            }

            total += (keys.Length - 1) * gap;
            var x = -total * 0.5f;
            var y = ch * 0.5f - 10f - row * (unit + gap) - unit * 0.5f;
            for (var i = 0; i < keys.Length; i++)
            {
                var u = widths != null ? widths[i] : 1f;
                var w = u * unit + (u - 1f) * gap;
                var rt = PresentableUi.Centered("Key_" + keys[i], host, new Vector2(x + w * 0.5f, y), new Vector2(w, unit));
                var accent = keys[i] == "DONE" || keys[i] == "SHIFT";
                var img = Pic(rt, accent ? "Login_KeyAccent" : "Login_Key");
                var lRt = PresentableUi.Rect("L", rt, Vector2.zero, Vector2.one);
                var tmp = lRt.gameObject.AddComponent<TextMeshProUGUI>();
                if (art != null && art.englishFont != null)
                    tmp.font = art.englishFont;
                tmp.fontSize = 34f;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;
                tmp.fontStyle = FontStyles.Bold;
                var kv = new KeyView { label = tmp, image = img, ch = keys[i].Length == 1 ? keys[i][0] : '\0' };
                kv.isDigit = keys[i].Length == 1 && char.IsDigit(keys[i][0]);
                kv.isLetter = keys[i].Length == 1 && char.IsLetter(keys[i][0]);
                var id = keys[i];
                tmp.text = id == "SHIFT" ? "⇧" : id == "BACK" ? "⌫" : id == "SPACE" ? string.Empty : id == "DONE" ? "OK" : id;
                kv.button = MakeButton(rt, () => OnKey(id));
                _keys.Add(kv);
                x += w + gap;
            }
        }

        // ------------------------------------------------------------------ environment (3D)

        private Vector3 ArcPoint(float u, float v, float r)
        {
            var ang = (u - 0.5f) * ArcDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(ang) * r, (0.5f - v) * ArcHeight * (r / ArcRadius), Mathf.Cos(ang) * r);
        }

        private void BuildEnvironment()
        {
            // dark dome and floor disc hide the training room behind the login
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "Dome";
            Destroy(dome.GetComponent<Collider>());
            dome.transform.SetParent(_root, false);
            dome.transform.localScale = Vector3.one * 5.4f;
            if (domeMat != null)
                dome.GetComponent<Renderer>().sharedMaterial = domeMat;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Floor";
            Destroy(floor.GetComponent<Collider>());
            floor.transform.SetParent(_root, false);
            floor.transform.position = new Vector3(_head.position.x, _floorY + 0.012f, _head.position.z);
            floor.transform.localScale = new Vector3(5.2f, 0.01f, 5.2f);
            if (domeMat != null)
                floor.GetComponent<Renderer>().sharedMaterial = domeMat;

            // curved lab backdrop
            var arc = new GameObject("Backdrop");
            arc.transform.SetParent(_root, false);
            arc.AddComponent<MeshFilter>().sharedMesh = BuildArcMesh(48);
            var mr = arc.AddComponent<MeshRenderer>();
            mr.sharedMaterial = backdropMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // floating ball-and-stick molecules between the panel and the backdrop
            var rng = new System.Random(11);
            var spots = new[]
            {
                new Vector3(-0.78f, 0.20f, 1.25f), new Vector3(-0.62f, -0.26f, 1.05f), new Vector3(-0.98f, -0.02f, 1.6f),
                new Vector3(0.80f, 0.30f, 1.3f), new Vector3(0.66f, -0.30f, 1.0f), new Vector3(1.0f, 0.02f, 1.55f)
            };
            for (var i = 0; i < spots.Length; i++)
                _molecules.Add(BuildMolecule(spots[i], rng, 6 + rng.Next(0, 4)));

            // glowing tubes of the backdrop: bubbles and spark particles at their image positions
            if (glowMat != null)
            {
                Bubbles(ArcPoint(862f / 1024f, 0.62f, ArcRadius - 0.05f), new Color(0.5f, 1f, 0.6f, 0.8f));
                Bubbles(ArcPoint(978f / 1024f, 0.62f, ArcRadius - 0.05f), new Color(0.5f, 0.85f, 1f, 0.8f));
                Sparks(new Vector3(0f, 0.15f, 1.5f), new Color(1f, 0.6f, 0.25f, 0.9f), 18f, new Vector3(2.4f, 1.4f, 0.8f));
                Sparks(new Vector3(0f, 0.2f, 0.9f), new Color(0.5f, 0.9f, 1f, 0.7f), 10f, new Vector3(1.4f, 0.9f, 0.4f));
            }

            // rotating HUD rings at the right console of the reference
            if (art != null && art.ring != null)
            {
                var p = ArcPoint(715f / 1024f, 0.52f, ArcRadius - 0.06f);
                for (var i = 0; i < 2; i++)
                {
                    var go = new GameObject("HudRing" + i);
                    go.transform.SetParent(_root, false);
                    go.transform.localPosition = p;
                    go.transform.localRotation = Quaternion.LookRotation(-new Vector3(p.x, 0f, p.z));
                    go.transform.localScale = Vector3.one * (i == 0 ? 0.55f : 0.4f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = i == 0 ? (art.ringDashed != null ? art.ringDashed : art.ring) : art.ring;
                    sr.color = new Color(0.4f, 0.9f, 1f, i == 0 ? 0.75f : 0.55f);
                    _rings.Add(go.transform);
                }
            }
        }

        private Mesh BuildArcMesh(int seg)
        {
            var verts = new Vector3[(seg + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var tris = new int[seg * 6];
            for (var i = 0; i <= seg; i++)
            {
                var u = i / (float)seg;
                verts[i * 2] = ArcPoint(u, 1f, ArcRadius);
                verts[i * 2 + 1] = ArcPoint(u, 0f, ArcRadius);
                uvs[i * 2] = new Vector2(u, 0f);
                uvs[i * 2 + 1] = new Vector2(u, 1f);
            }

            for (var i = 0; i < seg; i++)
            {
                var a = i * 2;
                var t = i * 6;
                // seen from inside (the head is at the centre)
                tris[t] = a; tris[t + 1] = a + 1; tris[t + 2] = a + 2;
                tris[t + 3] = a + 2; tris[t + 4] = a + 1; tris[t + 5] = a + 3;
            }

            var m = new Mesh { name = "LoginArc", vertices = verts, uv = uvs, triangles = tris };
            m.RecalculateBounds();
            return m;
        }

        private static readonly Color[] AtomCols =
        {
            new Color(0.62f, 0.55f, 0.48f), // C
            new Color(0.92f, 0.16f, 0.14f), // O
            new Color(0.25f, 0.38f, 0.98f), // N
            new Color(0.9f, 0.92f, 0.95f) // H
        };

        private Transform BuildMolecule(Vector3 localPos, System.Random rng, int atoms)
        {
            var root = new GameObject("Molecule");
            root.transform.SetParent(_root, false);
            root.transform.localPosition = localPos;
            var pts = new List<Vector3> { Vector3.zero };
            var kinds = new List<int> { 0 };
            for (var i = 1; i < atoms; i++)
            {
                var parent = rng.Next(0, pts.Count);
                var dir = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f)
                    .normalized;
                var p = pts[parent] + dir * 0.055f;
                pts.Add(p);
                var k = rng.Next(0, 10);
                kinds.Add(k < 4 ? 0 : k < 6 ? 1 : k < 8 ? 2 : 3);
                MakeBond(root.transform, pts[parent], p);
            }

            for (var i = 0; i < pts.Count; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(root.transform, false);
                s.transform.localPosition = pts[i];
                s.transform.localScale = Vector3.one * (kinds[i] == 3 ? 0.03f : 0.05f);
                Tint(s, AtomCols[kinds[i]]);
            }

            return root.transform;
        }

        private void MakeBond(Transform parent, Vector3 a, Vector3 b)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(c.GetComponent<Collider>());
            c.transform.SetParent(parent, false);
            c.transform.localPosition = (a + b) * 0.5f;
            c.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            c.transform.localScale = new Vector3(0.011f, (b - a).magnitude * 0.5f, 0.011f);
            Tint(c, new Color(0.8f, 0.85f, 0.9f));
        }

        private void Tint(GameObject go, Color col)
        {
            var r = go.GetComponent<Renderer>();
            if (atomMat != null)
                r.sharedMaterial = atomMat;
            var pb = new MaterialPropertyBlock();
            pb.SetColor("_BaseColor", col);
            r.SetPropertyBlock(pb);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private ParticleSystem MakeParticles(string name, Vector3 localPos, Color col, float rate, float life, float size, Vector3 box)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = col;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 160;
            var em = ps.emission;
            em.rateOverTime = rate;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = box;
            var col2 = ps.colorOverLifetime;
            col2.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col2.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = glowMat;
            return ps;
        }

        private void Bubbles(Vector3 p, Color col)
        {
            var ps = MakeParticles("Bubbles", p + new Vector3(0f, -0.28f, -0.03f), col, 14f, 3.2f, 0.035f, new Vector3(0.1f, 0.02f, 0.05f));
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.Local;
            v.y = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
            v.x = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);
            v.z = new ParticleSystem.MinMaxCurve(-0.001f, 0.001f);
        }

        private void Sparks(Vector3 p, Color col, float rate, Vector3 box)
        {
            var ps = MakeParticles("Sparks", p, col, rate, 3.5f, 0.016f, box);
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.Local;
            v.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            v.y = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);
            v.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
        }

        private void BuildLights()
        {
            var sun = new GameObject("LoginKeyLight").AddComponent<Light>();
            sun.transform.SetParent(_root, false);
            sun.type = LightType.Directional;
            sun.transform.localRotation = Quaternion.Euler(35f, -25f, 0f);
            sun.color = new Color(1f, 0.95f, 0.9f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.None;
            var cyan = new GameObject("LoginCyanLight").AddComponent<Light>();
            cyan.transform.SetParent(_root, false);
            cyan.type = LightType.Point;
            cyan.transform.localPosition = new Vector3(0f, 0.3f, 0.5f);
            cyan.color = new Color(0.4f, 0.85f, 1f);
            cyan.intensity = 1.6f;
            cyan.range = 3.5f;
            cyan.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ interaction

        private void Subscribe(bool on)
        {
            var kb = Keyboard.current;
            if (kb == null)
                return;
            if (on)
                kb.onTextInput += OnTextInput;
            else
                kb.onTextInput -= OnTextInput;
        }

        private void OnTextInput(char c)
        {
            if (_focus == Field.None || _closing)
                return;
            if (c == '\b')
                Backspace();
            else if (c == '\n' || c == '\r')
                Done();
            else if (!char.IsControl(c))
                Type(c);
        }

        private void SetLanguage(Lang lang)
        {
            if (Loc.Current == lang)
                return;
            Loc.SetLanguage(lang);
            PresentableFx.Play(PresentableFx.Tick, 0.8f);
        }

        private void Focus(Field f)
        {
            _focus = f;
            _shift = false;
            Refresh();
        }

        private void OnKey(string id)
        {
            if (_focus == Field.None)
                return;
            switch (id)
            {
                case "SHIFT": _shift = !_shift; RefreshKeys(); break;
                case "BACK": Backspace(); break;
                case "SPACE": Type(' '); break;
                case "DONE": Done(); break;
                default: Type(id[0]); break;
            }
        }

        private bool Upper()
        {
            if (_shift)
                return true;
            if (_focus != Field.Name)
                return false;
            return _name.Length == 0 || _name[_name.Length - 1] == ' ';
        }

        private void Type(char c)
        {
            if (_focus == Field.Name)
            {
                if (LoginValidator.IsDigit(c))
                {
                    ShowNameError("err_name_digit");
                    return;
                }

                if (!LoginValidator.IsNameCharAllowed(c) || _name.Length >= 60 || (c == ' ' && (_name.Length == 0 || _name.EndsWith(" "))))
                    return;
                _name += char.IsLetter(c) && Upper() ? char.ToUpperInvariant(c) : c;
                ClearNameError();
            }
            else if (_focus == Field.Contact)
            {
                if (_contact.Length >= 50 || c == ' ')
                    return;
                _contact += char.IsLetter(c) && _shift ? char.ToUpperInvariant(c) : c;
                ClearContactError();
            }

            _shift = false;
            Refresh();
        }

        private void Backspace()
        {
            if (_focus == Field.Name && _name.Length > 0)
                _name = _name.Substring(0, _name.Length - 1);
            else if (_focus == Field.Contact && _contact.Length > 0)
                _contact = _contact.Substring(0, _contact.Length - 1);
            Refresh();
        }

        private void Done()
        {
            _focus = _focus == Field.Name ? Field.Contact : Field.None;
            _shift = false;
            Refresh();
        }

        private void OnStart() => TrySubmit();

        private bool TrySubmit()
        {
            if (_closing)
                return false;
            var nameErr = LoginValidator.ValidateName(_name, out var cleanName);
            var contactErr = LoginValidator.ValidateContact(_contact, out var contact, out var isEmail, out _);
            ShowFieldErrors(nameErr, contactErr);
            if (nameErr != LoginError.None || contactErr != LoginError.None)
            {
                PresentableFx.Haptic(true, 0.5f, 0.12f);
                return false;
            }

            _closing = true;
            _focus = Field.None;
            Refresh();
            SessionResults.StartSession(cleanName, isEmail ? contact.ToLowerInvariant() : string.Empty, isEmail ? string.Empty : contact,
                Loc.Suffix());
            StartCoroutine(Finish());
            return true;
        }

        private IEnumerator Finish()
        {
            yield return PresentableFx.Wait(0.5f);
            _done = true;
        }

        private static string NameKey(LoginError e)
        {
            switch (e)
            {
                case LoginError.NameEmpty: return "err_name_empty";
                case LoginError.NameTooShort: return "err_name_short";
                case LoginError.NameHasDigit: return "err_name_digit";
                default: return "err_name_char";
            }
        }

        private static string ContactKey(LoginError e)
        {
            switch (e)
            {
                case LoginError.ContactEmpty: return "err_contact_empty";
                case LoginError.EmailInvalid: return "err_email";
                case LoginError.PhoneTooShort: return "err_phone_short";
                case LoginError.PhoneTooLong: return "err_phone_long";
                default: return "err_phone";
            }
        }

        private void ShowFieldErrors(LoginError n, LoginError c)
        {
            if (n != LoginError.None)
                ShowNameError(NameKey(n), 0f);
            else
                ClearNameError();
            if (c != LoginError.None)
            {
                _contactErr.Bind(Scn, ContactKey(c));
                _contactErr.gameObject.SetActive(true);
                _contactErrImg.SetActive(true);
            }
            else
            {
                ClearContactError();
            }
        }

        private void ShowNameError(string key, float seconds = 2.2f)
        {
            _nameErr.Bind(Scn, key);
            _nameErr.gameObject.SetActive(true);
            _nameErrImg.SetActive(true);
            _nameErrUntil = seconds > 0f ? Time.unscaledTime + seconds : float.MaxValue;
            PresentableFx.Haptic(true, 0.35f, 0.08f);
        }

        private void ClearNameError()
        {
            if (_nameErr == null)
                return;
            _nameErr.gameObject.SetActive(false);
            _nameErrImg.SetActive(false);
            _nameErrUntil = 0f;
        }

        private void ClearContactError()
        {
            if (_contactErr == null)
                return;
            _contactErr.gameObject.SetActive(false);
            _contactErrImg.SetActive(false);
        }

        private void Refresh()
        {
            if (_nameTmp == null)
                return;
            var caret = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? "|" : " ";
            var nt = _name + (_focus == Field.Name ? caret : string.Empty);
            var ct = _contact + (_focus == Field.Contact ? caret : string.Empty);
            if (_nameTmp.text != nt)
                _nameTmp.text = nt;
            if (_contactTmp.text != ct)
                _contactTmp.text = ct;
            _nameHint.gameObject.SetActive(_name.Length == 0 && _focus != Field.Name);
            _contactHint.gameObject.SetActive(_contact.Length == 0 && _focus != Field.Contact);
            _nameFocusImg.SetActive(_focus == Field.Name);
            _contactFocusImg.SetActive(_focus == Field.Contact);
            RefreshKeys();
        }

        private void RefreshKeys()
        {
            if (_kbGroup == null)
                return;
            var up = Upper();
            foreach (var k in _keys)
            {
                if (k.isLetter)
                {
                    var t = (up ? char.ToUpperInvariant(k.ch) : char.ToLowerInvariant(k.ch)).ToString();
                    if (k.label.text != t)
                        k.label.text = t;
                }
                if (k.isDigit)
                {
                    var off = _focus == Field.Name;
                    k.button.interactable = !off;
                    k.label.alpha = off ? 0.25f : 1f;
                }
            }
        }

        // ------------------------------------------------------------------ per-frame animation

        private void Update()
        {
            if (_root == null)
                return;

            _enterT = Mathf.Min(1f, _enterT + Time.unscaledDeltaTime / 0.9f);
            var ease = 1f - Mathf.Pow(1f - _enterT, 3f);
            foreach (var g in _groups)
            {
                if (g != null)
                    g.alpha = ease;
            }

            var kbTarget = _focus != Field.None && !_closing ? 1f : 0f;
            _kbGroup.alpha = Mathf.MoveTowards(_kbGroup.alpha, kbTarget, Time.unscaledDeltaTime * 5f);
            _kbGroup.blocksRaycasts = kbTarget > 0f;

            // caret blink
            if (_focus != Field.None)
                Refresh();

            if (_nameErrUntil > 0f && _nameErrUntil < float.MaxValue && Time.unscaledTime > _nameErrUntil)
                ClearNameError();

            // language toggle: the lit segment slides to the chosen language
            var want = Loc.IsBangla ? -98f : 98f;
            _toggleX = Mathf.Lerp(_toggleX, want, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            _toggleOn.anchoredPosition = new Vector2(_toggleX, 0f);
            _langBn.SetColor(Loc.IsBangla ? Color.white : new Color(0.55f, 0.65f, 0.75f));
            _langEn.SetColor(Loc.IsBangla ? new Color(0.55f, 0.65f, 0.75f) : Color.white);

            // START breathes
            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f);
            _startHot.color = new Color(1f, 1f, 1f, pulse * 0.55f);
            _startRt.localScale = Vector3.one * (1f + pulse * 0.012f);

            // molecules drift and turn
            for (var i = 0; i < _molecules.Count; i++)
            {
                var m = _molecules[i];
                if (m == null)
                    continue;
                m.Rotate(new Vector3(8f + i * 3f, 14f + i * 4f, 5f) * Time.unscaledDeltaTime, Space.Self);
                var lp = m.localPosition;
                lp.y += Mathf.Sin(Time.unscaledTime * (0.5f + i * 0.07f) + i) * 0.0004f;
                m.localPosition = lp;
            }

            for (var i = 0; i < _rings.Count; i++)
                _rings[i].Rotate(0f, 0f, (i == 0 ? 25f : -40f) * Time.unscaledDeltaTime, Space.Self);
        }
    }
}
