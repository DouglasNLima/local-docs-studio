using LensDocsStudio.Native.Validation;

namespace LensDocsStudio.LayoutExperiment;

internal static class FixtureCatalog
{
    public static (string Name, string Source, string Last)[] Create(string output, string? scenario)
    {
        var targeted = TargetedFixtures.Cases.FirstOrDefault(fixture => fixture.Name == scenario);
        return targeted is null ? ContinuousPreviewFixtures.Create(output, scenario) : [(targeted.Name, targeted.Source, targeted.RequiredText[^1])];
    }
}
