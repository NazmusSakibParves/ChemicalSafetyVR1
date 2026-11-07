using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 4 copy. The properties card uses the sheet supplied for this scenario.
    /// Bangla voice and the instruction image are files, not strings here.
    /// </summary>
    public static class Sc04Content
    {
        public static string InstructionEnglish => Loc.T("SC04", "InstructionEnglish");

        public static string QuestionHeader => Loc.T("SC04", "QuestionHeader");
        public static string QuestionPrompt => Loc.T("SC04", "QuestionPrompt");
        public static string CorrectTitle => Loc.T("SC04", "CorrectTitle");
        public static string FinishLabel => Loc.T("SC04", "FinishLabel");
        public const int CorrectIndex = 2;
        public static string ModuleComplete => Loc.T("SC04", "ModuleComplete");
        public static string GateTitle => Loc.T("SC04", "GateTitle");
        public static string GateQuestion => Loc.T("SC04", "GateQuestion");
        public static string GateBody => Loc.T("SC04", "GateBody");

        public static string[] Materials => Loc.A("SC04", "Materials");

        public static string[] Ratings => Loc.A("SC04", "Ratings");
        public static string[] Times => Loc.A("SC04", "Times");

        public static string[] RowLabels => Loc.A("SC04", "RowLabels");

        public static string[] OptionLabels => Loc.A("SC04", "OptionLabels");

        public static string[] OptionFeedback => Loc.A("SC04", "OptionFeedback");

        public static string[] OptionWrongTitle => Loc.A("SC04", "OptionWrongTitle");

        // ray flow: inline reveal (tick / cross + a short reason under every option), as Scenarios 1-3
        public static string TagCorrect => Loc.T("SC04", "TagCorrect");
        public static string TagWrong => Loc.T("SC04", "TagWrong");
        public static string[] OptionReasons => Loc.A("SC04", "OptionReasons");

        /// <summary>What the voice says while each glove's properties are shown (subtitle text).</summary>
        public static string[] GloveVoice => Loc.A("SC04", "GloveVoice");
    }
}
