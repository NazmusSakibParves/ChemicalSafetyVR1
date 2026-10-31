using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 11 copy (gas cylinder leak in the yard: SDS, raise the alarm, evacuate, call the supervisor by radio).
    /// Texts follow the Scene 11 workflow pictures; every text has an English and a Bangla version in Content/SC11.json.
    /// </summary>
    public static class Sc11Content
    {
        public static string InstructionEnglish => Loc.T("SC11", "InstructionEnglish");
        public static string SdsHint => Loc.T("SC11", "SdsHint");
        public static string AlarmHint => Loc.T("SC11", "AlarmHint");
        public static string EvacuateHint => Loc.T("SC11", "EvacuateHint");
        public static string CallHint => Loc.T("SC11", "CallHint");
        public static string AlarmLabel => Loc.T("SC11", "AlarmLabel");
        public static string CallLabel => Loc.T("SC11", "CallLabel");
        public static string RadioScreen => Loc.T("SC11", "RadioScreen");
        public static string RadioCall => Loc.T("SC11", "RadioCall");
        public static string RadioAck => Loc.T("SC11", "RadioAck");
        public static string RadioOut => Loc.T("SC11", "RadioOut");
        public static string MapShared => Loc.T("SC11", "MapShared");
        public static string MapYou => Loc.T("SC11", "MapYou");
        public static string RadioPlayer => Loc.T("SC11", "RadioPlayer");
        public static string RadioReply => Loc.T("SC11", "RadioReply");

        public static string GateTitle => Loc.T("SC11", "GateTitle");
        public static string GateQuestion => Loc.T("SC11", "GateQuestion");
        public static string GateBody => Loc.T("SC11", "GateBody");

        public static string QuestionHeader => Loc.T("SC11", "QuestionHeader");
        public static string QuestionPrompt => Loc.T("SC11", "QuestionPrompt");
        public static string CorrectTitle => Loc.T("SC11", "CorrectTitle");
        public static string CorrectBody => Loc.T("SC11", "CorrectBody");
        public static string FinishLabel => Loc.T("SC11", "FinishLabel");
        public const int CorrectIndex = 0;

        public static string[] OptionLabels => Loc.A("SC11", "OptionLabels");
        public static string[] OptionFeedback => Loc.A("SC11", "OptionFeedback");
        public static string[] OptionWrongTitle => Loc.A("SC11", "OptionWrongTitle");

        public static string ModuleComplete => Loc.T("SC11", "ModuleComplete");
    }
}
