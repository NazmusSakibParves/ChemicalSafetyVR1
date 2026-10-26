namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 8 copy (diluting sulfuric acid with water). Texts follow the Scene 08 workflow pictures.
    /// The Bangla voice-overs and the Bangla images are files, not strings here.
    /// </summary>
    public static class Sc08Content
    {
        public const string InstructionEnglish =
            "You are a lab technician. A batch of dye requires dilute sulfuric acid. Dilute the acid carefully with water.";

        public const string PpeHint = "Touch each piece of protective equipment to put it on.";
        public const string HoodHint = "Switch on the Fume Hood at the control panel.";
        public const string TrayHint = "Touch the tray to pick it up.";
        public const string SashHint = "Touch the glass door of the fume hood to open it.";
        public const string HoodWarn = "Switch on the fume hood first.";
        public const string MixHint = "Touch the water beaker: add the acid slowly and stir.";

        // PPE SELECTION PANEL (workflow step03), in the order the items are taken
        public const string PanelTitle = "PPE SELECTION PANEL";

        public static readonly string[] PpeNames =
        {
            "Splash Goggles",
            "Respirator",
            "Neoprene Gloves",
            "Acid-resistant Apron"
        };

        // "Dilution practice complete" card (workflow step09)
        public const string GateTitle = "DILUTION PRACTICE COMPLETE.";
        public const string GateQuestion = "Are you ready for the Multiple Choice Questions (MCQ)?";
        public const string GateBody = "Press NEXT to proceed or BACK to review rules again.";

        // MCQ (workflow step10..13)
        public const string QuestionHeader = "MCQ: Acid Dilution";
        public const string QuestionPrompt = "What is the right procedure for diluting acid with water?";
        public const string CorrectTitle = "RIGHT ACTION";
        public const string CorrectBody =
            "Slowly adding acid to the water with continuous stirring (the large water volume safely absorbs the heat, preventing dangerous spattering).";
        public const string FinishLabel = "FINISH";
        public const int CorrectIndex = 1;

        public static readonly string[] OptionLabels =
        {
            "Pour water into the acid",
            "Slowly add acid to the water with continuous stirring",
            "Pour both simultaneously into a third beaker",
            "Mix quickly to reduce exposure time"
        };

        public static readonly string[] OptionFeedback =
        {
            "Water added to concentrated acid causes explosive boiling and spattering.",
            CorrectBody,
            "Mixing both at once gives no control over the heat; the solution can boil and splash.",
            "Rushing the mix releases heat too fast and spatters acid. Add the acid slowly."
        };

        public static readonly string[] OptionWrongTitle =
        {
            "WRONG ACTION",
            "",
            "WRONG ACTION",
            "WRONG ACTION"
        };

        public const string ModuleComplete = "Module complete. Well done!";
    }
}
