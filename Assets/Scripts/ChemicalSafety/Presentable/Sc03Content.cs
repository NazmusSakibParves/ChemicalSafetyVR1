using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Every on-screen English text of Scenario 3 in one place. After editing a string here run
    /// Chemical Safety/Presentable/Build Presentable Scene (the scene stores nothing of these texts, but it
    /// recompiles and re-wires). The Bangla voice-over and Bangla images are separate files:
    ///   voice  -> Tools/generate_vo.py   (clip names VO_SC03_Instruction_bn, VO_Sc03Touch_bn, VO_Sc03Inspect_bn,
    ///                                     VO_Sc03Wrong_bn, VO_Sc03Complete_bn/en); run with --only &lt;clip&gt;
    ///   images -> Tools/render_bangla_text.py (BN_Sc03InstructionPanel, BN_Sc03Touch)
    /// </summary>
    public static class Sc03Content
    {
        // ---- instruction card (step02)
        public static string InstructionEnglish => Loc.T("SC03", "InstructionEnglish");

        // ---- subtitles
        public static string SdsHint => Loc.T("SC03", "SdsHint");
        public static string TouchHint => Loc.T("SC03", "TouchHint");

        // ---- DRUM INSPECTION REPORT card (step04): label / value pairs, the dot colour is set in Sc03Inspect
        public static string ReportTitle => Loc.T("SC03", "ReportTitle");
        public static string ScanCaption => Loc.T("SC03", "ScanCaption");
        public static string ContinueLabel => Loc.T("SC03", "ContinueLabel");

        public static string[] ReportLabels => Loc.A("SC03", "ReportLabels");

        public static string[] ReportValues => Loc.A("SC03", "ReportValues");

        // ---- question (step05 - step08)
        public static string QuestionHeader => Loc.T("SC03", "QuestionHeader");
        public static string QuestionPrompt => Loc.T("SC03", "QuestionPrompt");
        public static string CorrectTitle => Loc.T("SC03", "CorrectTitle");
        public static string FinishLabel => Loc.T("SC03", "FinishLabel");
        public const int CorrectIndex = 1;

        public static string[] OptionLabels => Loc.A("SC03", "OptionLabels");

        /// <summary>Text of the green card (index = CorrectIndex) or of the red card (every other index).</summary>
        public static string[] OptionFeedback => Loc.A("SC03", "OptionFeedback");

        /// <summary>Red card title per option (the correct index is ignored).</summary>
        public static string[] OptionWrongTitle => Loc.A("SC03", "OptionWrongTitle");

        public static string ModuleComplete => Loc.T("SC03", "ModuleComplete");
    }
}
