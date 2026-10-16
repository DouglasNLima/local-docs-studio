using System.Text.Json;
using System.Xml;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeSvgChecks
{
    public static void Run(string output)
    {
        var renderer = new NativeSvgService();
        var unsafeContent = new Dictionary<string, string>
        {
            ["External stylesheet"] = "<style>@import url('https://example.com/style.css');</style>",
            ["External paint resource"] = "<rect width='20' height='20' fill='url(https://example.com/paint.svg)'/>",
            ["Relative external paint"] = "<rect width='20' height='20' fill='url(other.svg#paint)'/>",
            ["Escaped CSS function"] = "<rect width='20' height='20' style=\"fill:u\\72l('https://example.com/paint.svg')\"/>",
            ["Script content"] = "<script>alert('unsafe')</script>",
            ["Embedded external image"] = "<image href='https://example.com/image.png'/>",
            ["Active animation"] = "<animate attributeName='href' values='https://example.com'/>",
            ["XML base override"] = "<g xml:base='https://example.com/'><rect fill='url(#paint)'/></g>"
        };
        foreach (var (name, content) in unsafeContent)
        {
            var rejected = false;
            try { renderer.Prepare("<svg xmlns='http://www.w3.org/2000/svg'>" + content + "</svg>"); }
            catch (Exception ex) when (ex is InvalidDataException or XmlException) { rejected = true; }
            if (!rejected) throw new InvalidDataException("Unsafe native SVG was accepted: " + name);
        }
        var local = renderer.Prepare("<svg xmlns='http://www.w3.org/2000/svg'><defs><linearGradient id='paint'/></defs><rect width='20' height='20' fill='url(#paint)'/></svg>");
        if (!local.Contains("url(#paint)", StringComparison.Ordinal)) throw new InvalidDataException("A local SVG definition was lost.");
        File.WriteAllText(Path.Combine(output, "native-svg-safety.json"), JsonSerializer.Serialize(new { Rejected = unsafeContent.Keys, LocalDefinitionsPreserved = true }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
