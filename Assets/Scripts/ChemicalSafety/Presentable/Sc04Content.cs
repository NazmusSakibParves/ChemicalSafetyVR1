namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 4 copy. The properties card uses the sheet supplied for this scenario.
    /// Bangla voice and the instruction image are files, not strings here.
    /// </summary>
    public static class Sc04Content
    {
        public const string InstructionEnglish =
            "You are a lab technician preparing to test a dye sample with concentrated HCl. " +
            "Check the SDS first. Select the right gloves.";

        public const string QuestionHeader = "SAFETY GLOVE QUIZ";
        public const string QuestionPrompt =
            "Question 4.1: You are about to handle concentrated HCl. Select the correct glove.";
        public const string CorrectTitle = "RIGHT SELECTION!";
        public const string FinishLabel = "NEXT";
        public const int CorrectIndex = 2;
        public const string ModuleComplete = "Scenario 4 completed successfully.";
        public const string GateTitle = "GLOVE INSPECTION COMPLETE";
        public const string GateBody = "Press NEXT for the question, or BACK to review the gloves.";

        public static readonly string[] Materials =
        {
            "White cotton",
            "Blue thin nitrile",
            "Black thick Neoprene"
        };

        public static readonly string[] Ratings = { "None", "Poor", "Excellent" };
        public static readonly string[] Times = { "Immediate", "<30 minute", ">480 minute" };

        public static readonly string[] OptionLabels =
        {
            "A   Cotton work gloves",
            "B   Thin nitrile gloves",
            "C   Thick neoprene gloves"
        };

        public static readonly string[] OptionFeedback =
        {
            "You selected cotton work gloves. Cotton has no chemical barrier. Acid soaks through immediately.",
            "Thin nitrile is a poor barrier to concentrated HCl. It is not the glove this SDS specifies.",
            "Thick neoprene resists concentrated HCl. Permeation time is over 480 minutes."
        };

        public static readonly string[] OptionWrongTitle =
        {
            "WRONG SELECTION!",
            "WRONG SELECTION!",
            ""
        };
    }
}
