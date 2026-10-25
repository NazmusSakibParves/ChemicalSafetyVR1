namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 7 copy (factory fire, choose the extinguisher). Every on-screen English string is here; the Bangla voice and the
    /// instruction image are files. Subscripts use TMP rich text (the CO2 formula).
    /// </summary>
    public static class Sc07Content
    {
        public const string InstructionEnglish =
            "You are a machine operator. A flammable solvent container near the electrical panel has overheated and ignited. " +
            "The fire is small and contained. Grab the right extinguisher.";

        public const string CardSuffix = "EXTINGUISHER SUMMARY";
        public const string NameLabel = "EXTINGUISHER NAME:";
        public const string UseLabel = "EFFECTIVE ON:";

        // Order = the order on the pillar from the player's left: Water, CO2, ABC dry powder.
        public static readonly string[] CardTitles =
        {
            "WATER EXTINGUISHER SUMMARY",
            "CO<sub>2</sub> EXTINGUISHER SUMMARY",
            "ABC DRY POWDER EXTINGUISHER SUMMARY"
        };

        public static readonly string[] Names =
        {
            "Water (H<sub>2</sub>O)",
            "Carbon Dioxide (CO<sub>2</sub>)",
            "Dry Powder (ABC)"
        };

        public static readonly string[] EffectiveOn =
        {
            "Ordinary Combustibles (Class A)",
            "Flammable Liquids & Electrical Fires",
            "Ordinary Combustibles, Flammable Liquids & Electrical Fires"
        };

        // Which class badge shows on the card (Water has none: it must never be used on B or C fires).
        public static readonly bool[] ShowBcBadge = { false, true, true };

        public const string GateTitle = "EXTINGUISHER INSPECTION COMPLETE";
        public const string GateQuestion = "Are you ready for the Multiple Choice Questions (MCQ)?";
        public const string GateBody = "Press NEXT to proceed or BACK to review again.";

        public const string QuestionHeader = "MCQ: Extinguisher Selection";
        public const string QuestionPrompt = "Which extinguisher is effective on flammable liquid and electric fire?";
        public const string CorrectTitle = "RIGHT CHOICE";
        public const string WrongTitle = "WRONG CHOICE";
        public const string FinishLabel = "NEXT";
        public const int CorrectIndex = 1;
        public const string ModuleComplete = "Scenario 7 completed successfully.";

        public static readonly string[] OptionLabels =
        {
            "Water extinguisher",
            "CO<sub>2</sub> extinguisher",
            "Wet chemical extinguisher",
            "Foam extinguisher"
        };

        public static readonly string[] OptionFeedback =
        {
            "Water conducts electricity and spreads burning flammable liquid — never use it on electrical or Class B fires.",
            "CO<sub>2</sub> is rated for Class B (flammable liquid) and electrical fires — displaces oxygen, leaves no residue.",
            "Wet chemical is made for cooking-oil (Class K) fires, not for flammable liquid or electrical fires.",
            "Foam is not rated for electrical fires — its water content could cause electric shock."
        };

        public static readonly string[] OptionWrongTitle =
        {
            WrongTitle,
            "",
            WrongTitle,
            WrongTitle
        };
    }
}
