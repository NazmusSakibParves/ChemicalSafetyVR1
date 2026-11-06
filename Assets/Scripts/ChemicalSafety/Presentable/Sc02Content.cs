using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 2 copy (chemical storage placement). The texts live in Assets/Resources/Content/SC02.json (English and Bangla
    /// side by side); the member name is the JSON key. They used to be inline in PresentableSequenceDirector (RunScenario1 tail,
    /// RunPlacement, BuildSc02Question). Card texts of the placement game are in GENERAL.json.
    /// </summary>
    public static class Sc02Content
    {
        public static string InstructionEnglish => Loc.T("SC02", "InstructionEnglish");
        public static string PlaceHint => Loc.T("SC02", "PlaceHint");
        public static string PlaceHint2 => Loc.T("SC02", "PlaceHint2");
        public static string QuestionHeader => Loc.T("SC02", "QuestionHeader");
        public static string QuestionPrompt => Loc.T("SC02", "QuestionPrompt");
        public static string CorrectTitle => Loc.T("SC02", "CorrectTitle");
        public static string WrongTitle => Loc.T("SC02", "WrongTitle");
        public static string FinishLabel => Loc.T("SC02", "FinishLabel");

        // controller-ray flow (Take Decision box, container pick, compartment popup)
        public static string DecisionTitle => Or(Loc.T("SC02", "DecisionTitle"), "TAKE DECISION");
        public static string DecisionBody => Or(Loc.T("SC02", "DecisionBody"), "Inspect the containers to read their hazards, or continue when you are ready to store them.");
        public static string InspectionButton => Or(Loc.T("SC02", "InspectionButton"), "INSPECTION");
        public static string ContinueButton => Or(Loc.T("SC02", "ContinueButton"), "CONTINUE");
        public static string PickContainerHint => Or(Loc.T("SC02", "PickContainerHint"), "Select the container you want to store.");
        public static string InspectHint => Or(Loc.T("SC02", "InspectHint"), "Select a container to read its properties. Close it, then inspect the next one.");
        public static string DestTitle => Or(Loc.T("SC02", "DestTitle"), "WHERE DO YOU STORE IT?");
        public static string DestTitleWrong => Or(Loc.T("SC02", "DestTitleWrong"), "WRONG COMPARTMENT - TRY AGAIN");

        public static string[] Compartments
        {
            get
            {
                var a = Loc.A("SC02", "Compartments");
                return a != null && a.Length >= 4 ? a : new[] { "Flammables", "Oxidizers", "Acids", "General Storage" };
            }
        }

        // Callout + six-question flow: one question per chemical (builder order 0-5), shown one after the other
        public static string TagCorrect => Or(Loc.T("SC02", "TagCorrect"), "CORRECT");
        public static string TagWrong => Or(Loc.T("SC02", "TagWrong"), "WRONG");
        public static string ChemName(int chem) => Loc.T("SC02", "C" + chem + "Name");

        /// <summary>"CHEMICAL 2 / 6" tag at the top of the callout.</summary>
        public static string ChemChip(int n, int total)
        {
            string D(int v) => Loc.IsBangla ? new string((char)('০' + v), 1) : v.ToString();
            return (Loc.IsBangla ? "রাসায়নিক " : "CHEMICAL ") + D(n) + " / " + D(total);
        }
        public static string[] ChemProps(int chem) => Loc.A("SC02", "C" + chem + "Props");
        public static string[] QuestionOptions(int chem) => Loc.A("SC02", "Q" + chem + "Options");
        public static string[] QuestionReasons(int chem) => Loc.A("SC02", "Q" + chem + "Reasons");

        /// <summary>"Question 2.n: Title. Ask" for the n-th question shown (1-based).</summary>
        public static string QuestionText(int chem, int n)
        {
            var num = Loc.IsBangla ? new string((char)('০' + n), 1) : n.ToString();
            return string.Format(Loc.T("SC02", "QuestionFormat"), num, Loc.T("SC02", "Q" + chem + "Title"), Loc.T("SC02", "Q" + chem + "Ask"));
        }

        private static string Or(string s, string fallback) => string.IsNullOrEmpty(s) ? fallback : s;

        public const int CorrectIndex = 1;

        public static string[] OptionLabels => Loc.A("SC02", "OptionLabels");
        public static string[] OptionFeedback => Loc.A("SC02", "OptionFeedback");
    }
}
