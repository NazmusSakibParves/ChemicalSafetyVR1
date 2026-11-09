#if UNITY_EDITOR
using ChemicalSafety.Presentable;
using UnityEditor;
using UnityEngine;

namespace ChemicalSafety.Editor
{
    /// <summary>
    /// Voice artist's Bangla recordings (test, 2026-11-10): login salam (Bangla + English clip) and the Scenario 1 instruction.
    /// ONE switch, <see cref="UseArtistVoice"/>:
    /// true  = those three clips play (files in Assets/Art/Audio/VO/ArtistBn, denoised, gain matched).
    /// false = + Build Presentable Scene gives exactly the previous voices (nothing was deleted or overwritten).
    /// The artist clips keep the original clip names, so the English twin lookup (Resources/VoEn) still works.
    /// </summary>
    public static partial class PresentableSceneBuilder
    {
        private const bool UseArtistVoice = true;

        private static void AssignArtistClips(PresentableArt art)
        {
            art.useArtistVoice = UseArtistVoice;
            void Keep(string file, System.Action<AudioClip> assign)
            {
                var path = "Assets/Art/Audio/VO/ArtistBn/" + file;
                ConfigureVoiceImport(path);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                    Debug.LogWarning("[Presentable] Artist clip missing, previous voice stays: " + file);
                assign(clip);
            }

            Keep("VO_AssalamuAlaikum_bn.mp3", c => art.artistSalamBn = c);
            Keep("VO_SC01_Instruction_bn.mp3", c => art.artistSc01Instruction = c);
            Keep("VO_Sc01Complete_bn.mp3", c => art.artistSc01Complete = c);
            Keep("VO_Sc01WrongNext_bn.mp3", c => art.artistSc01WrongNext = c);
            Keep("VO_ContinueHint_bn.mp3", c => art.artistContinueHint = c);
            Keep("VO_Correct_bn.mp3", c => art.artistCorrect = c);
            Keep("VO_Wrong_bn.mp3", c => art.artistWrong = c);
            var all = new System.Collections.Generic.List<AudioClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Art/Audio/VO/ArtistBn" }))
            {
                var cp = AssetDatabase.GUIDToAssetPath(guid);
                ConfigureVoiceImport(cp);
                var cl = AssetDatabase.LoadAssetAtPath<AudioClip>(cp);
                if (cl != null)
                    all.Add(cl);
            }

            art.artistClips = all.ToArray();
            art.artistBnInstructionCards = new Sprite[9];
            for (var n = 2; n <= 8; n++)
                art.artistBnInstructionCards[n] = LoadSprite("2D/BanglaText/BN_Sc0" + n + "InstructionPanelArtist");
            art.artistBnInstructionPanel = LoadSprite("2D/BanglaText/BN_Sc01InstructionPanelArtist");
            art.artistBnInstructionSub = LoadSprite("2D/BanglaText/BN_Sc01InstructionSubArtist");
            Debug.Log("[Presentable] Artist Bangla voice = " + (UseArtistVoice ? "ON (" + all.Count + " clips)" : "off (previous voices)"));
        }
    }
}
#endif
