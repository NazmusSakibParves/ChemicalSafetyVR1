using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 9 copy (chemical splash on the face and eyes: broken flask, SDS, safety shower / eye wash).
    /// Texts follow the Scene 09 workflow pictures. The Bangla voice-overs and Bangla images are files, not strings here.
    /// </summary>
    public static class Sc09Content
    {
        public static string InstructionEnglish => Loc.T("SC09", "InstructionEnglish");

        // workflow step06
        public static string SpillInstruction => Loc.T("SC09", "SpillInstruction");

        public static string FlaskHint => Loc.T("SC09", "FlaskHint");
        public static string SdsHint => Loc.T("SC09", "SdsHint");
        public static string WalkHint => Loc.T("SC09", "WalkHint");
        public static string PushHint => Loc.T("SC09", "PushHint");
        public static string FlushHint => Loc.T("SC09", "FlushHint");

        // gate card (workflow step11)
        public static string GateTitle => Loc.T("SC09", "GateTitle");
        public static string GateQuestion => Loc.T("SC09", "GateQuestion");
        public static string GateBody => Loc.T("SC09", "GateBody");

        // MCQ (workflow step12 / step13)
        public static string QuestionHeader => Loc.T("SC09", "QuestionHeader");
        public static string QuestionPrompt => Loc.T("SC09", "QuestionPrompt");
        public static string CorrectTitle => Loc.T("SC09", "CorrectTitle");
        public static string CorrectBody => Loc.T("SC09", "CorrectBody");
        public static string FinishLabel => Loc.T("SC09", "FinishLabel");
        public const int CorrectIndex = 1;

        public static string[] OptionLabels => Loc.A("SC09", "OptionLabels");

        public static string[] OptionFeedback => Loc.A("SC09", "OptionFeedback");

        public static string[] OptionWrongTitle => Loc.A("SC09", "OptionWrongTitle");

        public static string ModuleComplete => Loc.T("SC09", "ModuleComplete");
    }
}
