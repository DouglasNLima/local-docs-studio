using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace LensDocsStudio.LayoutExperiment;

internal static class TextCulture
{
    // Same supported dependency-property default inherited by a stock FlowDocument.
    public static CultureInfo Value { get; } = ((XmlLanguage)FrameworkElement.LanguageProperty.GetMetadata(typeof(FrameworkElement)).DefaultValue).GetEquivalentCulture();
}
