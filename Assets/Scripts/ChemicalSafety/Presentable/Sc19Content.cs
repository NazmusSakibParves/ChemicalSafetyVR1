using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 19: confined-space tank entry (the empty chemical tank). Texts are in Resources/Content/SC19.json (bn + en).
    /// The right answer is the second option (B). Voice-overs and Bangla images are files, not strings here.
    /// </summary>
    public static class Sc19Content
    {
        public static string RayInstruction => Loc.T("SC19", "RayInstruction");
        public static string GateTitle => Loc.T("SC19", "GateTitle");
        public static string GateQuestion => Loc.T("SC19", "GateQuestion");
        public static string GateBody => Loc.T("SC19", "GateBody");
        public static string QuestionHeader => Loc.T("SC19", "RayQuestionHeader");
        public static string QuestionPrompt => Loc.T("SC19", "RayQuestionPrompt");
        public static string CorrectTitle => Loc.T("SC19", "RayCorrectTitle");
        public static string FinishLabel => Loc.T("SC19", "FinishLabel");
        public static string TagCorrect => Loc.T("SC19", "TagCorrect");
        public static string TagWrong => Loc.T("SC19", "TagWrong");
        public static string ModuleComplete => Loc.T("SC19", "ModuleComplete");
        public static string CapDangerTitle => Loc.T("SC19", "CapDangerTitle");
        public static string CapSafeTitle => Loc.T("SC19", "CapSafeTitle");
        public const int CorrectIndex = 1;
        public static string[] OptionLabels => Loc.A("SC19", "RayOptionLabels");
        public static string[] OptionReasons => Loc.A("SC19", "RayOptionReasons");
        public static string[] OptionWrongTitle => Loc.A("SC19", "RayOptionWrongTitle");
        public static string[] CapWrong => Loc.A("SC19", "CapWrong");
        public static string[] CapRight => Loc.A("SC19", "CapRight");

        /// <summary>Caption of a film step: w1..w3 (unsafe entry) and r1..r6 (safe entry).</summary>
        public static string Caption(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 2 || !int.TryParse(key.Substring(1), out var n))
                return string.Empty;
            var a = key[0] == 'w' ? CapWrong : CapRight;
            return a != null && n >= 1 && n <= a.Length ? a[n - 1] : string.Empty;
        }
    }
}
