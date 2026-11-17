using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 21: acetone / solvent decanted into a drinking-water bottle. Texts are in Resources/Content/SC22.json (bn + en).
    /// The right answer is the third option (C). Last scenario: FinishLabel = FINISH. Voice-overs (Resources/Sc22Vo) and the Bangla images are files, not strings here.
    /// </summary>
    public static class Sc22Content
    {
        public static string RayInstruction => Loc.T("SC22", "RayInstruction");
        public static string GateTitle => Loc.T("SC22", "GateTitle");
        public static string GateQuestion => Loc.T("SC22", "GateQuestion");
        public static string GateBody => Loc.T("SC22", "GateBody");
        public static string QuestionHeader => Loc.T("SC22", "RayQuestionHeader");
        public static string QuestionPrompt => Loc.T("SC22", "RayQuestionPrompt");
        public static string CorrectTitle => Loc.T("SC22", "RayCorrectTitle");
        public static string FinishLabel => Loc.T("SC22", "FinishLabel");
        public static string TagCorrect => Loc.T("SC22", "TagCorrect");
        public static string TagWrong => Loc.T("SC22", "TagWrong");
        public static string ModuleComplete => Loc.T("SC22", "ModuleComplete");
        public static string CautionTitle => Loc.T("SC22", "CautionTitle");
        public static string CautionClose => Loc.T("SC22", "CautionClose");
        public const int CorrectIndex = 2;
        public static string[] OptionLabels => Loc.A("SC22", "RayOptionLabels");
        public static string[] OptionReasons => Loc.A("SC22", "RayOptionReasons");
        public static string[] OptionWrongTitle => Loc.A("SC22", "RayOptionWrongTitle");
        public static string[] CapStory => Loc.A("SC22", "CapStory");

        /// <summary>Caption of a film step n1..n6.</summary>
        public static string Caption(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 2 || !int.TryParse(key.Substring(1), out var n))
                return string.Empty;
            var a = CapStory;
            return a != null && n >= 1 && n <= a.Length ? a[n - 1] : string.Empty;
        }
    }
}
