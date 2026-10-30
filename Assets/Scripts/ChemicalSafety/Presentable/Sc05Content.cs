using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 5 copy (dyeing factory, respirator selection). Names and compatibility come from the client's
    /// "Respirator Properties" sheet; the question and feedback texts from the Scene 05 workflow.
    /// The Bangla voice and the instruction image are files, not strings here.
    /// </summary>
    public static class Sc05Content
    {
        public static string InstructionEnglish => Loc.T("SC05", "InstructionEnglish");

        /// <summary>
        /// true: the MASK INFORMATION and RESPIRATOR INSPECTION COMPLETE cards copy the designer's hologram look
        /// (Scenario 5 only). false: the shared blue boxes used by the other scenarios. One line to switch back.
        /// </summary>
        public const bool WorkflowStyleCards = true;

        public static string CardTitle => Loc.T("SC05", "CardTitle");
        public static string NameLabel => Loc.T("SC05", "NameLabel");
        public static string CompatLabel => Loc.T("SC05", "CompatLabel");

        public static string GateTitle => Loc.T("SC05", "GateTitle");
        public static string GateQuestion => Loc.T("SC05", "GateQuestion");
        public static string GateBody => Loc.T("SC05", "GateBody");

        public static string QuestionHeader => Loc.T("SC05", "QuestionHeader");
        public static string QuestionPrompt => Loc.T("SC05", "QuestionPrompt");
        public static string CorrectTitle => Loc.T("SC05", "CorrectTitle");
        public static string FinishLabel => Loc.T("SC05", "FinishLabel");
        public const int CorrectIndex = 1;

        /// <summary>Mask order in the question and on the cards: N95, half-face, surgical, full-face.</summary>
        public static string[] MaskNames => Loc.A("SC05", "MaskNames");

        public static readonly bool[] Compatible = { false, true, false, false };

        public static string Incompatible => Loc.T("SC05", "Incompatible");
        public static string CompatibleText => Loc.T("SC05", "CompatibleText");

        public static string Compatibility(int i) => Compatible[i] ? CompatibleText : Incompatible;

        public static string[] OptionLabels => Loc.A("SC05", "OptionLabels");

        public static string[] OptionFeedback => Loc.A("SC05", "OptionFeedback");

        public static string[] OptionWrongTitle => Loc.A("SC05", "OptionWrongTitle");
    }
}
