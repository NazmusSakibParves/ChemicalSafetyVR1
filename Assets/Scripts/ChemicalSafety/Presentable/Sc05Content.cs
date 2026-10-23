namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 5 copy (dyeing factory, respirator selection). Names and compatibility come from the client's
    /// "Respirator Properties" sheet; the question and feedback texts from the Scene 05 workflow.
    /// The Bangla voice and the instruction image are files, not strings here.
    /// </summary>
    public static class Sc05Content
    {
        public const string InstructionEnglish =
            "You are a dyeing machine operator. A batch of fabric needs treatment with a toluene-based solvent. " +
            "The vapor level is high in there today. Put on the right mask before you go in.";

        public const string CardTitle = "MASK INFORMATION";
        public const string NameLabel = "MASK NAME:";
        public const string CompatLabel = "COMPATIBILITY:";

        public const string GateTitle = "RESPIRATOR INSPECTION COMPLETE.";
        public const string GateQuestion = "Are you ready for the Multiple Choice Questions (MCQ)?";
        public const string GateBody = "Press NEXT to proceed or BACK to review again.";

        public const string QuestionHeader = "MCQ 5.1: Respirator Type Selection";
        public const string QuestionPrompt = "Which respirator is required for toluene?";
        public const string CorrectTitle = "CORRECT CHOICE";
        public const string FinishLabel = "NEXT";
        public const int CorrectIndex = 1;

        /// <summary>Mask order in the question and on the cards: N95, half-face, surgical, full-face.</summary>
        public static readonly string[] MaskNames =
        {
            "Simple dust mask (N95)",
            "Half-face respirator with organic vapour cartridge",
            "Surgical mask",
            "Full-face respirator with particulate only filter"
        };

        public static readonly bool[] Compatible = { false, true, false, false };

        public const string Incompatible = "Incompatible with toluene solvent";
        public const string CompatibleText = "Compatible with toluene solvent";

        public static string Compatibility(int i) => Compatible[i] ? CompatibleText : Incompatible;

        public static readonly string[] OptionLabels =
        {
            "A   Simple dust mask (N95)",
            "B   Half-face respirator with organic vapor cartridge",
            "C   Surgical mask",
            "D   Full-face respirator with particulate-only filter"
        };

        public static readonly string[] OptionFeedback =
        {
            "Organic vapor passes straight through.",
            "SDS specifies organic vapour cartridge for toluene.",
            "A surgical mask only stops droplets. Organic vapor passes straight through.",
            "A particulate-only filter does not capture organic vapor. Toluene passes straight through."
        };

        public static readonly string[] OptionWrongTitle =
        {
            "WRONG CHOICE.",
            "",
            "WRONG CHOICE.",
            "WRONG CHOICE."
        };
    }
}
