using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 2 copy (chemical storage placement). The texts live in Assets/Resources/Content/SC02.json (English and Bangla
    /// side by side); the member name is the JSON key. They used to be inline in PresentableSequenceDirector (RunScenario1 tail,
    /// RunPlacement, BuildSc02Question). Card texts of the placement game are in GENERAL.json.
    /// </summary>
    public static class Sc02Content
    {
        public static string InstructionEnglish => Loc.T("SC02", "InstructionEnglish");
        public static string PlaceHint => Loc.T("SC02", "PlaceHint");
        public static string PlaceHint2 => Loc.T("SC02", "PlaceHint2");
        public static string QuestionHeader => Loc.T("SC02", "QuestionHeader");
        public static string QuestionPrompt => Loc.T("SC02", "QuestionPrompt");
        public static string CorrectTitle => Loc.T("SC02", "CorrectTitle");
        public static string WrongTitle => Loc.T("SC02", "WrongTitle");
        public static string FinishLabel => Loc.T("SC02", "FinishLabel");

        public const int CorrectIndex = 1;

        public static string[] OptionLabels => Loc.A("SC02", "OptionLabels");
        public static string[] OptionFeedback => Loc.A("SC02", "OptionFeedback");
    }
}
