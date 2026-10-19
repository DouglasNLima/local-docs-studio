namespace LensDocsStudio.LayoutExperiment;

/// <summary>Product requirements revised by the user on 19 October 2026.</summary>
internal static class AcceptanceV2
{
    public const string Contract = "continuous-layout-v2";
    public const double CompleteLayoutBudget = 1000;
    public const double PresentationBudget = 1500;
    public const double InputGapBudget = 100;
    public static object Rows(double layout, double presentation, double input, bool parity, bool appearanceAndLifecycle) => new
    {
        Contract,
        CompleteLayout = new { Milliseconds = layout, Budget = CompleteLayoutBudget, Pass = layout <= CompleteLayoutBudget },
        CompletePreparationPresentation = new { Milliseconds = presentation, Budget = PresentationBudget, Pass = presentation <= PresentationBudget },
        ObservedShownWindowInputGap = new { Milliseconds = input, Budget = InputGapBudget, Pass = input <= InputGapBudget },
        FunctionalSelectionClipboardSourceUiaParity = parity ? "PASS" : "INCOMPLETE",
        ActualOsAppearanceAndLifecycle = appearanceAndLifecycle ? "PASS" : "INCOMPLETE",
        Stretch250Milliseconds = new { Blocking = false, Met = layout <= 250 },
        FullAcceptance = layout <= CompleteLayoutBudget && presentation <= PresentationBudget && input <= InputGapBudget
            && parity && appearanceAndLifecycle ? "PASS" : "INCOMPLETE_OR_FAIL"
    };
}
