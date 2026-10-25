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
            "You are about to handle concentrated Hydrochloric Acid (HCl). Select the correct glove type based on safety guidelines.";
        public const string CorrectTitle = "RIGHT SELECTION!";
        public const string FinishLabel = "NEXT";
        public const int CorrectIndex = 2;
        public const string ModuleComplete = "Scenario 4 completed successfully.";
        public const string GateTitle = "GLOVE INSPECTION COMPLETE";
        public const string GateQuestion = "Are you ready for the Multiple Choice Questions (MCQ)?";
        public const string GateBody = "Press NEXT to proceed or BACK to review again.";

        public static readonly string[] Materials =
        {
            "White cotton",
            "Blue thin nitrile",
            "Black thick Neoprene"
        };

        public static readonly string[] Ratings = { "None", "Poor", "Excellent" };
        public static readonly string[] Times = { "Immediate", "<30 minute", ">480 minute" };

        public static readonly string[] RowLabels = { "MATERIAL", "RATING", "PERMEATION TIME" };

        public static readonly string[] OptionLabels =
        {
            "A   Standard cotton work gloves\n<size=70%>Poor chemical barrier and permeates instantly</size>",
            "B   Thin nitrile disposable gloves\n<size=70%>Thin nitrile disposable rubber gloves</size>",
            "C   Thick Neoprene/Butyl rubber gloves"
        };

        public static readonly string[] OptionFeedback =
        {
            "You selected cotton work gloves. Cotton has no chemical barrier. Acid soaks through immediately.",
            "Thin nitrile is a poor barrier to concentrated HCl. It is not the glove this SDS specifies.",
            "Thick Neoprene/Butyl gloves are impermeable to concentrated HCl. Prevents rapid permeation and degradation. Matches highest mineral acid resistance ratings."
        };

        public static readonly string[] OptionWrongTitle =
        {
            "WRONG SELECTION!",
            "WRONG SELECTION!",
            ""
        };
    }
}
