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
        [Tooltip("Designed frames. Green opens on a right answer, red on a wrong answer.")]
        public Sprite frameCorrect;
        public Sprite frameWrong;
        public Sprite frameWide;
        public Sprite optionBar;
        public Sprite optionBarHot;
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
        public Sprite sdsSc02;
        public Sprite chartSc02;
        public Sprite sdsSc03;
        public Sprite sdsSc04;
        public Sprite sdsSc05;
        [Tooltip("Enlarged drum label shown inside the scan frame (SC03).")]
        public Sprite sc03Label;
        [Tooltip("White corner brackets for the Selected Label scan (SC03).")]
        public Sprite scanFrame;
        [Tooltip("Thin glowing gold ring for the SC03 inspection.")]
        public Sprite ringThin;

        [Header("Scenario 5 hologram cards")]
        public Sprite holoPanel;
        public Sprite holoButton;
        public Sprite holoButtonPrimary;
        public Sprite iconBack;
        public Sprite iconNext;
        public Sprite iconClose;
        public Sprite iconSpeaker;
        [Tooltip("Upright line-art picture of each respirator: N95, half-face, surgical, full-face.")]
        public Sprite[] maskIcons = new Sprite[4];

        [Header("Bangla text (pre-shaped images)")]
        public Sprite bnSalam;
        public Sprite bnWelcomeSub;
        public Sprite bnInstructionSub;
        public Sprite bnInstructionPanel;
        public Sprite bnStandHere;
        [Tooltip("Stand Here badge (English + Bangla) over the floor marker.")]
        public Sprite standBadge;
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
        public Sprite bnSc02Instruction;
        public Sprite bnSc02InstructionPanel;
        public Sprite bnSc03InstructionPanel;
        public Sprite bnSc03Touch;
        public Sprite bnSc04InstructionPanel;
        public Sprite bnSc05InstructionPanel;
        public Sprite sdsSc06;
        [Tooltip("Scenario 4 GLOVE PROPERTIES card icons: glove outline, material, rating, permeation time.")]
        public Sprite gloveCardGlove;
        public Sprite gloveCardMaterial;
        public Sprite gloveCardRating;
        public Sprite gloveCardTime;
        public Sprite gatePanel;
        public Sprite gateButton;
        public Sprite gateButtonPrimary;
        public Sprite bnSc06InstructionPanel;
        public Sprite bnSc06HoodHint;
        public Sprite bnSc06JugHint;
        public Sprite bnSc06SashHint;
        public Sprite bnSc06PourHint;
        public Sprite bnSc06HoodWarn;
        public Sprite bnSc06CapHint;

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
        public AudioClip voSc01CompleteBn;
        public AudioClip voSc01CompleteEn;
        public AudioClip voSc02Instruction;
        public AudioClip voSc02CompleteBn;
        public AudioClip voSc02PlaceHint;
        public AudioClip voSc02CompleteEn;
        public AudioClip voSc03Instruction;
        public AudioClip voSc03Touch;
        public AudioClip voSc03Inspect;
        public AudioClip voSc03Wrong;
        public AudioClip voSc03CompleteBn;
        public AudioClip voSc03CompleteEn;
        public AudioClip voSc04Instruction;
        public AudioClip voSc04CompleteBn;
        public AudioClip voSc04CompleteEn;
        public AudioClip voSc05Instruction;
        [Tooltip("Bangla voice when a respirator is opened: N95, half-face, surgical, full-face (Sc05Content order).")]
        public AudioClip[] voSc05Mask = new AudioClip[4];
        public AudioClip voSc05CompleteBn;
        public AudioClip voSc05CompleteEn;
        public AudioClip voSc06Instruction;
        public AudioClip voSc06Hood;
        public AudioClip voSc06HoodOn;
        public AudioClip voSc06HoodWarn;
        public AudioClip voSc06Jug;
        public AudioClip voSc06Cap;
        public AudioClip voSc06Sash;
        public AudioClip voSc06Pour;
        public AudioClip voSc06Gate;
        public AudioClip voSc06CompleteBn;
        public AudioClip voSc06CompleteEn;
        [Header("Scenario 7")]
        public Sprite sdsSc07;
        public Sprite bnSc07InstructionPanel;
        public Sprite extIconExtinguisher;
        public Sprite extIconFlammable;
        public Sprite extBadgeBc;
        public AudioClip voSc07Instruction;
        [Tooltip("Water, CO2, ABC: what the extinguisher is and what it is for.")]
        public AudioClip[] voSc07Ext = new AudioClip[3];
        public AudioClip voSc07Gate;
        public AudioClip voSc07Hero;
        public AudioClip voSc07CompleteBn;
        public AudioClip voSc07CompleteEn;
        [Header("Scenario 8")]
        public Sprite sdsSc08;
        public Sprite bnSc08InstructionPanel;
        public Sprite bnSc08PpeHint;
        public Sprite bnSc08TrayHint;
        public Sprite bnSc08SashHint;
        public Sprite bnSc08MixHint;
        public AudioClip voSc08Instruction;
        public AudioClip voSc08Ppe;
        public AudioClip voSc08PpeDone;
        public AudioClip voSc08Hood;
        public AudioClip voSc08HoodOn;
        public AudioClip voSc08Sash;
        public AudioClip voSc08HoodWarn;
        public AudioClip voSc08Mix;
        public AudioClip voSc08Gate;
        public AudioClip voSc08CompleteBn;
        public AudioClip voSc08CompleteEn;
        [Tooltip("VO_WrongLabel_bn.mp3")]
        public AudioClip voWrongLabel;

        [Header("Fonts")]
        public TMP_FontAsset englishFont;
    }
}
