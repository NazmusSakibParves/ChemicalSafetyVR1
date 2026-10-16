using TMPro;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// All sprites / clips / fonts used by the Presentable demo, assigned by PresentableSceneBuilder.
    /// </summary>
    [CreateAssetMenu(menuName = "Chemical Safety/Presentable Art", fileName = "PresentableArt")]
    public sealed class PresentableArt : ScriptableObject
    {
        [Header("FX")]
        public Sprite ring;
        public Sprite ringDashed;
        public Sprite radialGlow;
        public Sprite beam;
        public Sprite arrowDown;

        [Header("Icons")]
        public Sprite speaker;
        public Sprite check;
        public Sprite cross;
        public Sprite circle;
        public Sprite dot;
        public Sprite iconGhs;
        public Sprite iconSds;

        [Header("Frames (white, tint in code)")]
        public Sprite roundedPanel;
        public Sprite roundedSolid;
        public Sprite softGlow;
        public Sprite popupFrame;
        public Sprite answerBox;
        public Sprite pop02;

        [Header("Drum thumbnails")]
        public Sprite thumbFaded;
        public Sprite thumbNoLabel;
        public Sprite thumbMismatch;
        public Sprite thumbCorrect;

        [Header("Props")]
        public Sprite sdsClipboard;

        [Header("Bangla text (pre-shaped images)")]
        public Sprite bnSalam;
        public Sprite bnWelcomeSub;
        public Sprite bnInstructionSub;
        public Sprite bnInstructionPanel;
        public Sprite bnStandHere;
        public Sprite bnCorrectSub;
        public Sprite bnWrongSub;
        public Sprite bnClipboardHint;
        public Sprite bnZoomHint;
        public Sprite bnNextZone;

        [Header("Voice")]
        public AudioClip voSalam;
        public AudioClip voInstruction;
        public AudioClip voCorrect;
        public AudioClip voWrong;

        [Header("Fonts")]
        public TMP_FontAsset englishFont;
    }
}
