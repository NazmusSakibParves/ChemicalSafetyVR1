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
        public const string InstructionEnglish =
            "<color=#5FD8FF><b>VOICE-OVER:</b></color> You find an old drum of organic peroxide that has been " +
            "sitting on the back shelf for years. Expiry date is over 2 years past. Crystallization visible " +
            "around the cap. You must decide what to do.";

        // ---- subtitles
        public const string SdsHint = "Press X on the left controller to open or hide the SDS.";
        public const string TouchHint = "Touch the glowing arrow in front of the drum to inspect it.";

        // ---- DRUM INSPECTION REPORT card (step04): label / value pairs, the dot colour is set in Sc03Inspect
        public const string ReportTitle = "DRUM INSPECTION REPORT";
        public const string ScanCaption = "Selected Label";
        public const string ContinueLabel = "Continue";

        public static readonly string[] ReportLabels =
        {
            "CHEMICAL TYPE:", "CONDITION:", "LABEL STATUS:", "DATE CODE:"
        };

        public static readonly string[] ReportValues =
        {
            "Organic Peroxide",
            "Crystallized residue detected",
            "Expired",
            "12/08/2024"
        };

        // ---- question (step05 - step08)
        public const string QuestionHeader = "IMMEDIATE ACTION PROTOCOL";
        public const string QuestionPrompt = "Question 3: What is the correct immediate action?";
        public const string CorrectTitle = "CORRECT ACTION!";
        public const string FinishLabel = "NEXT";
        public const int CorrectIndex = 1;

        public static readonly string[] OptionLabels =
        {
            "Open the drum and use the chemical as normal",
            "Do not open. Isolate area, notify supervisor, contact emergency response",
            "Move the drum to another shelf and continue work",
            "Dispose of it in regular chemical waste"
        };

        /// <summary>Text of the green card (index = CorrectIndex) or of the red card (every other index).</summary>
        public static readonly string[] OptionFeedback =
        {
            "Wrong. Crystallised organic peroxides can detonate on opening or friction.",
            "Standard response for degraded/unstable peroxides. Preserves life and enables trained disposal.",
            "Wrong. Moving the drum causes shock and friction. Crystallised peroxides are shock-sensitive.",
            "Wrong. Degraded peroxides need trained, specialist disposal. Regular waste can trigger a violent reaction."
        };

        /// <summary>Red card title per option (the correct index is ignored).</summary>
        public static readonly string[] OptionWrongTitle =
        {
            "DANGER: DETONATION CONFIRMED",
            "",
            "DANGER: SHOCK DETONATION",
            "DANGER: VIOLENT REACTION"
        };

        public const string ModuleComplete = "Scenario 3 completed successfully.";
    }
}
