namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 6 copy (chemistry lab, fume hood, engineering controls). Texts follow the Scene 06 workflow pictures.
    /// The Bangla voice-overs and the Bangla images are files, not strings here.
    /// </summary>
    public static class Sc06Content
    {
        public const string InstructionEnglish =
            "You are a Mixing Operator. You need to pour a volatile solvent, but because the container is open, " +
            "vapors are spreading into the air. Take the right Engineering Control before pouring.";

        public const string HoodHint = "Switch on the Fume Hood at the control panel.";
        public const string HoodOnHint = "The fume hood is running. Go to the table and close the solvent container.";
        public const string CapHint = "Touch the cap to close the container.";
        public const string JugHint = "Touch the Volatile Solvent container to pick it up.";
        public const string SashHint = "Touch the glass door of the fume hood to open it.";
        public const string HoodWarn = "Switch on the fume hood first.";
        public const string PourHint = "Touch the glass jar to pour the solvent slowly.";

        // "Inspection completed" card (workflow step08)
        public const string GateTitle = "Inspection Completed";
        public const string GateQuestion = "Are you ready for the multiple choice questions?";
        public const string GateBody = "To proceed click next button or to review again press back button.";

        // MCQ 6.1 (workflow step09..12)
        public const string QuestionHeader = "Engineering Control Hierarchy";
        public const string QuestionPrompt = "What is the correct engineering control to capture vapors when pouring a volatile solvent?";
        public const string CorrectTitle = "RIGHT ACTION";
        public const string CorrectBody = "Switching on the hood first, then working inside is correct.";
        public const string FinishLabel = "NEXT";
        public const int CorrectIndex = 2;

        public static readonly string[] OptionLabels =
        {
            "Pour the solvent outside the fume hood on the open workbench.",
            "Put the bottle inside the fume hood but leave the hood switched off.",
            "Switch on the fume hood first, then bring the bottle inside to pour.",
            "Do not use the fume hood; just open a nearby window."
        };

        // The designer's picture shows the passive-ventilation sentence with option A selected; it fits D (the open window),
        // so A gets its own sentence. Change here if the user wants the picture's wording on A.
        public static readonly string[] OptionFeedback =
        {
            "Pouring on the open bench releases the vapors straight into your breathing zone.",
            "A hood that is switched off pulls no air, so the vapors escape into the room.",
            CorrectBody,
            "Passive ventilation is unreliable and offers no capture at the source."
        };

        public static readonly string[] OptionWrongTitle =
        {
            "WRONG ACTION",
            "WRONG ACTION",
            "",
            "WRONG ACTION"
        };
    }
}
