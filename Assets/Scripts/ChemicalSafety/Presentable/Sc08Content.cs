using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 8 copy (diluting sulfuric acid with water). Texts follow the Scene 08 workflow pictures.
    /// The Bangla voice-overs and the Bangla images are files, not strings here.
    /// </summary>
    public static class Sc08Content
    {
        public static string InstructionEnglish => Loc.T("SC08", "InstructionEnglish");

        public static string PpeHint => Loc.T("SC08", "PpeHint");
        public static string HoodHint => Loc.T("SC08", "HoodHint");
        public static string TrayHint => Loc.T("SC08", "TrayHint");
        public static string SashHint => Loc.T("SC08", "SashHint");
        public static string HoodWarn => Loc.T("SC08", "HoodWarn");
        public static string MixHint => Loc.T("SC08", "MixHint");

        // PPE SELECTION PANEL (workflow step03), in the order the items are taken
        public static string PanelTitle => Loc.T("SC08", "PanelTitle");

        public static string[] PpeNames => Loc.A("SC08", "PpeNames");

        // "Dilution practice complete" card (workflow step09)
        public static string GateTitle => Loc.T("SC08", "GateTitle");
        public static string GateQuestion => Loc.T("SC08", "GateQuestion");
        public static string GateBody => Loc.T("SC08", "GateBody");

        // MCQ (workflow step10..13)
        public static string QuestionHeader => Loc.T("SC08", "QuestionHeader");
        public static string QuestionPrompt => Loc.T("SC08", "QuestionPrompt");
        public static string CorrectTitle => Loc.T("SC08", "CorrectTitle");
        public static string CorrectBody => Loc.T("SC08", "CorrectBody");
        public static string FinishLabel => Loc.T("SC08", "FinishLabel");
        public const int CorrectIndex = 1;

        public static string[] OptionLabels => Loc.A("SC08", "OptionLabels");

        public static string[] OptionFeedback => Loc.A("SC08", "OptionFeedback");

        public static string[] OptionWrongTitle => Loc.A("SC08", "OptionWrongTitle");

        public static string ModuleComplete => Loc.T("SC08", "ModuleComplete");
    }
}
