namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 9 copy (chemical splash on the face and eyes: broken flask, SDS, safety shower / eye wash).
    /// Texts follow the Scene 09 workflow pictures. The Bangla voice-overs and Bangla images are files, not strings here.
    /// </summary>
    public static class Sc09Content
    {
        public const string InstructionEnglish =
            "You are a lab technician. Pour the chemical from the sample flask into the next flask. If something goes wrong, follow the safety procedure at once.";

        // workflow step06
        public const string SpillInstruction =
            "You are a lab technician. A flask tipped corrosive liquid splashed onto your face and eyes. You must respond immediately.";

        public const string FlaskHint = "Touch the purple sample flask to lift it and pour.";
        public const string SdsHint = "Read the Safety Data Sheet. Do not rub your eyes. Go to the eyewash now.";
        public const string WalkHint = "Walk to the safety shower and eyewash.";
        public const string PushHint = "Touch the push plate to start the water.";
        public const string FlushHint = "Hold eyelids open and keep flushing.";

        // gate card (workflow step11)
        public const string GateTitle = "EYEWASH AND SHOWER INSPECTION COMPLETE.";
        public const string GateQuestion = "Are you ready for the safety assessment questions?";
        public const string GateBody = "Press NEXT to proceed or BACK to review again.";

        // MCQ (workflow step12 / step13)
        public const string QuestionHeader = "EMERGENCY PROTOCOL ASSESSMENT";
        public const string QuestionPrompt = "What is the immediate action when a chemical gets into your eyes?";
        public const string CorrectTitle = "RIGHT ANSWER";
        public const string CorrectBody = "Standard first-aid for chemical eye exposure";
        public const string FinishLabel = "NEXT";
        public const int CorrectIndex = 1;

        public static readonly string[] OptionLabels =
        {
            "Wipe face with a cloth and continue work.",
            "Move to eyewash station and flush for 15+ minutes, then alert supervisor.",
            "Rinse briefly for 5 seconds.",
            "Apply chemical neutralizer directly to the eye."
        };

        public static readonly string[] OptionFeedback =
        {
            "Wiping spreads contamination and delays flushing — permanent eye damage risk.",
            CorrectBody,
            "A short rinse leaves chemical in the eye. Flush for at least 15 minutes.",
            "Neutralizers react in the eye and add heat and injury. Only clean water, for 15+ minutes."
        };

        public static readonly string[] OptionWrongTitle =
        {
            "Wrong answer",
            "",
            "Wrong answer",
            "Wrong answer"
        };

        public const string ModuleComplete = "Module complete. Well done!";
    }
}
