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
        public Sprite iconTouch;

        [Header("Frames (white, tint in code)")]
        public Sprite roundedPanel;
        public Sprite roundedSolid;
        public Sprite softGlow;
        public Sprite popupFrame;
        public Sprite answerBox;
        public Sprite pop02;
        public Sprite button;
        [Header("Red variants (generated from the designer frames)")]
        public Sprite popupFrameRed;
        public Sprite answerBoxRed;
        public Sprite buttonRed;

        [Header("Drum thumbnails")]
        [Tooltip("Blank drum body; label sprites are layered on top.")]
        public Sprite thumbNoLabel;
        public Sprite labelFaded;
        public Sprite labelMethanol;
        public Sprite labelIsopropanol;

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
        public Sprite bnPickLabel;
        public Sprite bnWrongLabel;
        public Sprite bnContinueHint;
        public Sprite bnTouchDrum;
        public Sprite bnCarrier;

        [Header("Voice")]
        public AudioClip voSalam;
        public AudioClip voInstruction;
        public AudioClip voCorrect;
        public AudioClip voWrong;
        [Tooltip("VO_PickLabel_bn.mp3 - drop the clip here if it is added after the scene build.")]
        public AudioClip voPickLabel;
        [Tooltip("VO_ContinueHint_bn.mp3 - played when the Continue button appears.")]
        public AudioClip voContinueHint;
        public AudioClip voTouchDrum;
        public AudioClip voCarrier;
        [Tooltip("VO_WrongLabel_bn.mp3")]
        public AudioClip voWrongLabel;

        [Header("Fonts")]
        public TMP_FontAsset englishFont;
    }
}
