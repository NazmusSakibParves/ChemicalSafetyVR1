namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 10 copy (acid spill on the floor: SDS, PPE, ventilate, spill kit, contain, neutralize, absorb, clean glass, dispose).
    /// Texts follow the Scene 10 workflow pictures. The Bangla voice-overs and Bangla images are files, not strings here.
    /// </summary>
    public static class Sc10Content
    {
        // workflow step02
        public const string InstructionEnglish =
            "You are a lab technician. A glass bottle of acid has slipped and shattered on the floor. You are the first person on the scene. The lab supervisor is not in the room. You must respond correctly and safely.";

        public const string SdsHint = "Read the Safety Data Sheet. It lists the spill response steps.";
        public const string PpeHint = "Put on your protective equipment. Touch each item.";
        public const string VentHint = "Ventilate the area. Touch the Fume Hood switch.";
        public const string KitHint = "Go to the spill kit and touch it to open it.";
        public const string CollectHint = "Take every item from the spill kit.";
        public const string ContainHint = "Touch CONTAIN. Lay the absorbent sock around the spill to stop the acid spreading.";
        public const string NeutralizeHint = "Touch NEUTRALIZE. Sprinkle soda ash over the acid to neutralize it.";
        public const string AbsorbHint = "Touch ABSORB. Lay absorbent pads on the spill to soak up the liquid.";
        public const string CleanHint = "Touch CLEAN-UP GLASS. Sweep the broken glass into the dustpan with the brush.";
        public const string DisposeHint = "Touch DISPOSE. Put the sock, pads and glass in the hazardous waste bag, then in the bin.";

        // PPE hologram (workflow step04): order of the PPE station items
        public static readonly string[] PpeItems = { "Splash Goggles", "Respirator", "Neoprene Gloves", "Acid-resistant Apron" };

        // spill kit collection (workflow step07)
        public const string CollectionTitle = "SPILL KIT COLLECTION";

        public static readonly string[] CollectionItems =
        {
            "Absorbent Sock", "Soda Ash", "Absorbent Pad", "Dust Pan & Brush", "Waste Bin Bag"
        };

        // neon step labels (workflow step08-12)
        public const string ContainLabel = "CONTAIN";
        public const string NeutralizeLabel = "NEUTRALIZE";
        public const string AbsorbLabel = "ABSORB";
        public const string CleanLabel = "CLEAN-UP\nGLASS";
        public const string DisposeLabel = "DISPOSE\nHAZARDOUS\nWASTE";
        public const string DisposeSub = "ALL SPILL MATERIALS";

        // gate card (workflow step13)
        public const string GateTitle = "Spill response complete.";
        public const string GateQuestion = "Are you ready for the multiple choice questions (MCQ)?";
        public const string GateBody = "If yes, click NEXT to proceed, or if you want to review again, click BACK.";

        // MCQ (workflow step14 / step15)
        public const string QuestionHeader = "MCQ: Spill Response";
        public const string QuestionPrompt = "Which of the following is the correct and safe way to handle an acid spill?";
        public const string CorrectTitle = "RIGHT ACTION";
        public const string CorrectBody = "Safe sequence";
        public const string FinishLabel = "NEXT";
        public const int CorrectIndex = 1;

        public static readonly string[] OptionLabels =
        {
            "Mop it up with water immediately",
            "Neutralize the acid, absorb it, clean up the glass fragments, and dispose of everything properly",
            "Use a vacuum cleaner to clean up the spill",
            "Leave it alone and let it evaporate on its own"
        };

        public static readonly string[] OptionFeedback =
        {
            "Spreads acid over a larger area and dilutes it into hidden crevices.",
            CorrectBody,
            "A vacuum can spark, spread acid fumes and is ruined by the acid.",
            "The acid and its fumes stay dangerous and someone may slip or get burned."
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
