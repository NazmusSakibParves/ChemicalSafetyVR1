using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 7 copy (factory fire, choose the extinguisher). Every on-screen English string is here; the Bangla voice and the
    /// instruction image are files. Subscripts use TMP rich text (the CO2 formula).
    /// </summary>
    public static class Sc07Content
    {
        public static string InstructionEnglish => Loc.T("SC07", "InstructionEnglish");

        public static string CardSuffix => Loc.T("SC07", "CardSuffix");
        public static string NameLabel => Loc.T("SC07", "NameLabel");
        public static string UseLabel => Loc.T("SC07", "UseLabel");

        // Order = the order on the pillar from the player's left: Water, CO2, ABC dry powder.
        public static string[] CardTitles => Loc.A("SC07", "CardTitles");

        public static string[] Names => Loc.A("SC07", "Names");

        public static string[] EffectiveOn => Loc.A("SC07", "EffectiveOn");

        // Which class badge shows on the card (Water has none: it must never be used on B or C fires).
        public static readonly bool[] ShowBcBadge = { false, true, true };

        public static string GateTitle => Loc.T("SC07", "GateTitle");
        public static string GateQuestion => Loc.T("SC07", "GateQuestion");
        public static string GateBody => Loc.T("SC07", "GateBody");

        public static string QuestionHeader => Loc.T("SC07", "QuestionHeader");
        public static string QuestionPrompt => Loc.T("SC07", "QuestionPrompt");
        public static string CorrectTitle => Loc.T("SC07", "CorrectTitle");
        public static string WrongTitle => Loc.T("SC07", "WrongTitle");
        public static string FinishLabel => Loc.T("SC07", "FinishLabel");
        public const int CorrectIndex = 1;
        public static string ModuleComplete => Loc.T("SC07", "ModuleComplete");

        public static string[] OptionLabels => Loc.A("SC07", "OptionLabels");

        public static string[] OptionFeedback => Loc.A("SC07", "OptionFeedback");

        public static string[] OptionWrongTitle => Loc.A("SC07", "OptionWrongTitle");
    }
}
