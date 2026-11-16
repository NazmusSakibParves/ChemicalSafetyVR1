using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 21: no JHA for a chemical coating job in a poorly ventilated room. Texts are in Resources/Content/SC21.json (bn + en).
    /// The right answer is the third option (C). Voice-overs (Resources/Sc21Vo) and the Bangla images are files, not strings here.
    /// </summary>
    public static class Sc21Content
    {
        public static string RayInstruction => Loc.T("SC21", "RayInstruction");
        public static string GateTitle => Loc.T("SC21", "GateTitle");
        public static string GateQuestion => Loc.T("SC21", "GateQuestion");
        public static string GateBody => Loc.T("SC21", "GateBody");
        public static string QuestionHeader => Loc.T("SC21", "RayQuestionHeader");
        public static string QuestionPrompt => Loc.T("SC21", "RayQuestionPrompt");
        public static string CorrectTitle => Loc.T("SC21", "RayCorrectTitle");
        public static string FinishLabel => Loc.T("SC21", "FinishLabel");
        public static string TagCorrect => Loc.T("SC21", "TagCorrect");
        public static string TagWrong => Loc.T("SC21", "TagWrong");
        public static string ModuleComplete => Loc.T("SC21", "ModuleComplete");
        public const int CorrectIndex = 2;
        public static string[] OptionLabels => Loc.A("SC21", "RayOptionLabels");
        public static string[] OptionReasons => Loc.A("SC21", "RayOptionReasons");
        public static string[] OptionWrongTitle => Loc.A("SC21", "RayOptionWrongTitle");
        public static string[] CapStory => Loc.A("SC21", "CapStory");

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
