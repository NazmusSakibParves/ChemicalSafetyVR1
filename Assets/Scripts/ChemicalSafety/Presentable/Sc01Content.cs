using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 1 copy (drum label selection). The texts live in Assets/Resources/Content/SC01.json (English and Bangla side by
    /// side); the member name is the JSON key. They used to be inline in PresentableSequenceDirector (RunScenario1,
    /// BuildSc01Question) and in the panel defaults (InstructionPanelView, PresentableMcqPanel).
    /// </summary>
    public static class Sc01Content
    {
        public static string InstructionEnglish => Loc.T("SC01", "InstructionEnglish");
        public static string WelcomeSub => Loc.T("SC01", "WelcomeSub");
        public static string InstructionSub => Loc.T("SC01", "InstructionSub");
        public static string ContinueHint => Loc.T("SC01", "ContinueHint");
        public static string PickLabelHint => Loc.T("SC01", "PickLabelHint");
        public static string TouchDrumHint => Loc.T("SC01", "TouchDrumHint");
        public static string WrongLabelHint => Loc.T("SC01", "WrongLabelHint");
        public static string CarrierHint => Loc.T("SC01", "CarrierHint");
        public static string QuestionHeader => Loc.T("SC01", "QuestionHeader");
        public static string QuestionPrompt => Loc.T("SC01", "QuestionPrompt");
        public static string CorrectTitle => Loc.T("SC01", "CorrectTitle");
        public static string WrongTitle => Loc.T("SC01", "WrongTitle");
        public static string FinishLabel => Loc.T("SC01", "FinishLabel");

        /// <summary>Index of the correct option in the unshuffled order (4. Correctly Labelled).</summary>
        public const int CorrectIndex = 3;

        public static string[] OptionLabels => Loc.A("SC01", "OptionLabels");
        public static string[] OptionWrongLabels => Loc.A("SC01", "OptionWrongLabels");
        public static string[] OptionFeedback => Loc.A("SC01", "OptionFeedback");
    }
}
