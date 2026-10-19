using System.Windows;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>Immutable interchange payload, prepared on the owned serialisation STA.</summary>
internal static class SelectionClipboard
{
    internal sealed record Payload(string Text, string Rtf, string Xaml)
    { public System.Collections.Immutable.ImmutableArray<byte> XamlPackage { get; init; } = []; public object? Serialisation { get; init; } }
    public static DataObject Create(Payload payload)
    {
        var data = new DataObject(); data.SetData(DataFormats.UnicodeText, payload.Text); data.SetData(DataFormats.Text, payload.Text);
        data.SetData(DataFormats.Rtf, payload.Rtf); data.SetData(DataFormats.Xaml, payload.Xaml);
        if (!payload.XamlPackage.IsDefaultOrEmpty) data.SetData(DataFormats.XamlPackage, new MemoryStream(payload.XamlPackage.ToArray()), false);
        return data;
    }
}
