using ChemicalSafety.Localization;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Scenario 10 copy (acid spill on the floor: SDS, PPE, ventilate, spill kit, contain, neutralize, absorb, clean glass, dispose).
    /// Texts follow the Scene 10 workflow pictures. The Bangla voice-overs and Bangla images are files, not strings here.
    /// </summary>
    public static class Sc10Content
    {
        // workflow step02
        public static string InstructionEnglish => Loc.T("SC10", "InstructionEnglish");

        public static string SdsHint => Loc.T("SC10", "SdsHint");
        public static string PpeHint => Loc.T("SC10", "PpeHint");
        public static string VentHint => Loc.T("SC10", "VentHint");
        public static string KitHint => Loc.T("SC10", "KitHint");
        public static string CollectHint => Loc.T("SC10", "CollectHint");
        public static string ContainHint => Loc.T("SC10", "ContainHint");
        public static string NeutralizeHint => Loc.T("SC10", "NeutralizeHint");
        public static string AbsorbHint => Loc.T("SC10", "AbsorbHint");
        public static string CleanHint => Loc.T("SC10", "CleanHint");
        public static string DisposeHint => Loc.T("SC10", "DisposeHint");

        // PPE hologram (workflow step04): order of the PPE station items
        public static string[] PpeItems => Loc.A("SC10", "PpeItems");

        // spill kit collection (workflow step07)
        public static string CollectionTitle => Loc.T("SC10", "CollectionTitle");

        public static string[] CollectionItems => Loc.A("SC10", "CollectionItems");

        // neon step labels (workflow step08-12)
        public static string ContainLabel => Loc.T("SC10", "ContainLabel");
        public static string NeutralizeLabel => Loc.T("SC10", "NeutralizeLabel");
        public static string AbsorbLabel => Loc.T("SC10", "AbsorbLabel");
        public static string CleanLabel => Loc.T("SC10", "CleanLabel");
        public static string DisposeLabel => Loc.T("SC10", "DisposeLabel");
        public static string DisposeSub => Loc.T("SC10", "DisposeSub");

        // gate card (workflow step13)
        public static string GateTitle => Loc.T("SC10", "GateTitle");
        public static string GateQuestion => Loc.T("SC10", "GateQuestion");
        public static string GateBody => Loc.T("SC10", "GateBody");

        // MCQ (workflow step14 / step15)
        public static string QuestionHeader => Loc.T("SC10", "QuestionHeader");
        public static string QuestionPrompt => Loc.T("SC10", "QuestionPrompt");
        public static string CorrectTitle => Loc.T("SC10", "CorrectTitle");
        public static string CorrectBody => Loc.T("SC10", "CorrectBody");
        public static string FinishLabel => Loc.T("SC10", "FinishLabel");
        public const int CorrectIndex = 1;

        public static string[] OptionLabels => Loc.A("SC10", "OptionLabels");

        public static string[] OptionFeedback => Loc.A("SC10", "OptionFeedback");

        public static string[] OptionWrongTitle => Loc.A("SC10", "OptionWrongTitle");

        public static string ModuleComplete => Loc.T("SC10", "ModuleComplete");
    }
}
