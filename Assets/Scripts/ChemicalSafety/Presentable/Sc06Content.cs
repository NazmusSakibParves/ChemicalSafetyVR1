using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 6 copy (chemistry lab, fume hood, engineering controls). Texts follow the Scene 06 workflow pictures.
    /// The Bangla voice-overs and the Bangla images are files, not strings here.
    /// </summary>
    public static class Sc06Content
    {
        public static string InstructionEnglish => Loc.T("SC06", "InstructionEnglish");

        public static string HoodHint => Loc.T("SC06", "HoodHint");
        public static string HoodOnHint => Loc.T("SC06", "HoodOnHint");
        public static string CapHint => Loc.T("SC06", "CapHint");
        public static string JugHint => Loc.T("SC06", "JugHint");
        public static string SashHint => Loc.T("SC06", "SashHint");
        public static string HoodWarn => Loc.T("SC06", "HoodWarn");
        public static string PourHint => Loc.T("SC06", "PourHint");

        // "Inspection completed" card (workflow step08)
        public static string GateTitle => Loc.T("SC06", "GateTitle");
        public static string GateQuestion => Loc.T("SC06", "GateQuestion");
        public static string GateBody => Loc.T("SC06", "GateBody");

        // MCQ 6.1 (workflow step09..12)
        public static string QuestionHeader => Loc.T("SC06", "QuestionHeader");
        public static string QuestionPrompt => Loc.T("SC06", "QuestionPrompt");
        public static string CorrectTitle => Loc.T("SC06", "CorrectTitle");
        public static string CorrectBody => Loc.T("SC06", "CorrectBody");
        public static string FinishLabel => Loc.T("SC06", "FinishLabel");
        public const int CorrectIndex = 2;

        public static string[] OptionLabels => Loc.A("SC06", "OptionLabels");

        // The designer's picture shows the passive-ventilation sentence with option A selected; it fits D (the open window),
        // so A gets its own sentence. Change here if the user wants the picture's wording on A.
        public static string[] OptionFeedback => Loc.A("SC06", "OptionFeedback");

        public static string[] OptionWrongTitle => Loc.A("SC06", "OptionWrongTitle");
    }
}
