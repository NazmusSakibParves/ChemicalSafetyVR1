using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 12 copy (PPE Donning Station for a corrosive acid: SDS, pick four PPE items, review, MCQ with several correct answers).
    /// Texts follow the Scene 12 workflow pictures; every text has an English and a Bangla version in Content/SC12.json.
    /// </summary>
    public static class Sc12Content
    {
        public static string InstructionEnglish => Loc.T("SC12", "InstructionEnglish");
        public static string SdsHint => Loc.T("SC12", "SdsHint");
        public static string StandHint => Loc.T("SC12", "StandHint");
        public static string PickHint => Loc.T("SC12", "PickHint");
        public static string PanelTitle => Loc.T("SC12", "PanelTitle");
        public static string RightTitle => Loc.T("SC12", "RightTitle");
        public static string RightBody => Loc.T("SC12", "RightBody");
        public static string WrongTitle => Loc.T("SC12", "WrongTitle");
        public static string WrongBody => Loc.T("SC12", "WrongBody");
        public static string ReviewTitle => Loc.T("SC12", "ReviewTitle");
        public static string ReviewHint => Loc.T("SC12", "ReviewHint");
        public static string NextLabel => Loc.T("SC12", "NextLabel");

        public static string GateTitle => Loc.T("SC12", "GateTitle");
        public static string GateQuestion => Loc.T("SC12", "GateQuestion");
        public static string GateBody => Loc.T("SC12", "GateBody");

        public static string QuestionHeader => Loc.T("SC12", "QuestionHeader");
        public static string QuestionPrompt => Loc.T("SC12", "QuestionPrompt");
        public static string SubmitLabel => Loc.T("SC12", "SubmitLabel");
        public static string CorrectTitle => Loc.T("SC12", "CorrectTitle");
        public static string CorrectBody => Loc.T("SC12", "CorrectBody");
        public static string WrongTitleMcq => Loc.T("SC12", "WrongTitleMcq");
        public static string WrongBodyMcq => Loc.T("SC12", "WrongBodyMcq");
        public static string MissedBodyMcq => Loc.T("SC12", "MissedBodyMcq");
        public static string FinishLabel => Loc.T("SC12", "FinishLabel");

        /// <summary>The MCQ options that are right (goggles / face shield, gloves, apron); "Safety boots" (3) is not.</summary>
        public static readonly bool[] CorrectSet = { true, true, true, false };

        public static string[] CategoryNames => Loc.A("SC12", "CategoryNames");
        public static string[] PickPrompt => Loc.A("SC12", "PickPrompt");

        /// <summary>The four options of a pick round (0 goggles, 1 gloves, 2 respirators, 3 aprons). Index 0 is the right one.</summary>
        public static string[] Options(int round)
        {
            switch (round)
            {
                case 0: return Loc.A("SC12", "OptGoggles");
                case 1: return Loc.A("SC12", "OptGloves");
                case 2: return Loc.A("SC12", "OptRespirators");
                default: return Loc.A("SC12", "OptAprons");
            }
        }

        public static string[] ReviewItems => Loc.A("SC12", "ReviewItems");
        public static string[] OptionLabels => Loc.A("SC12", "OptionLabels");

        public static string ModuleComplete => Loc.T("SC12", "ModuleComplete");
    }
}
