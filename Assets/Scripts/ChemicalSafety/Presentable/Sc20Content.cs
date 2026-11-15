using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 20: chemical splash on a worker, the emergency shower, the report to the supervisor. Texts are in Resources/Content/SC20.json (bn + en).
    /// The right answer is the third option (C). Voice-overs (Resources/Sc20Vo) and the Bangla images are files, not strings here.
    /// </summary>
    public static class Sc20Content
    {
        public static string RayInstruction => Loc.T("SC20", "RayInstruction");
        public static string GateTitle => Loc.T("SC20", "GateTitle");
        public static string GateQuestion => Loc.T("SC20", "GateQuestion");
        public static string GateBody => Loc.T("SC20", "GateBody");
        public static string QuestionHeader => Loc.T("SC20", "RayQuestionHeader");
        public static string QuestionPrompt => Loc.T("SC20", "RayQuestionPrompt");
        public static string CorrectTitle => Loc.T("SC20", "RayCorrectTitle");
        public static string FinishLabel => Loc.T("SC20", "FinishLabel");
        public static string TagCorrect => Loc.T("SC20", "TagCorrect");
        public static string TagWrong => Loc.T("SC20", "TagWrong");
        public static string ModuleComplete => Loc.T("SC20", "ModuleComplete");
        public const int CorrectIndex = 2;
        public static string[] OptionLabels => Loc.A("SC20", "RayOptionLabels");
        public static string[] OptionReasons => Loc.A("SC20", "RayOptionReasons");
        public static string[] OptionWrongTitle => Loc.A("SC20", "RayOptionWrongTitle");
        public static string[] CapStory => Loc.A("SC20", "CapStory");

        /// <summary>Caption of a film step n1..n7.</summary>
        public static string Caption(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 2 || !int.TryParse(key.Substring(1), out var n))
                return string.Empty;
            var a = CapStory;
            return a != null && n >= 1 && n <= a.Length ? a[n - 1] : string.Empty;
        }
    }
}
