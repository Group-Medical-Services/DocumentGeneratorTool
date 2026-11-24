using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace DocumentGeneratorTool.Diagrams;

/// <summary>Renders Mermaid source to SVG by running <c>mmdc</c> from @mermaid-js/mermaid-cli.</summary>
internal sealed class MermaidRenderer
{
    private static readonly XNamespace SvgNamespace = "http://www.w3.org/2000/svg";

    // Typst's SVG renderer ignores foreignObject, so labels must be plain SVG text.
    // Gantt charts default to a wide canvas whose 11px labels become unreadable at 70% page width, so they are laid out narrower.
    private const string Config = """{"htmlLabels":false,"flowchart":{"htmlLabels":false},"gantt":{"useWidth":600,"fontSize":12}}""";

    /// <summary>Returns SVG bytes, or null after printing a warning when the diagram cannot be rendered.</summary>
    public byte[]? TryRender(string source)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "DocumentGeneratorTool", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            var input = Path.Combine(workDir, "diagram.mmd");
            var output = Path.Combine(workDir, "diagram.svg");
            var config = Path.Combine(workDir, "mermaid.json");
            File.WriteAllText(input, source);
            File.WriteAllText(config, Config);

            var (stderr, exitCode) = RunMmdc(input, output, config);
            if (exitCode != 0 || !File.Exists(output))
            {
                var detail = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
                Console.Error.WriteLine($"Warning: could not render a Mermaid diagram; showing its source instead. {detail}".TrimEnd());
                return null;
            }

            return MakeEdgeLabelsOpaque(File.ReadAllBytes(output));
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }

    // Mermaid draws edge-label boxes at 50% opacity, so connector lines show through the label text.
    // The fix is inline styles: Typst's SVG renderer ignores the stylesheet that Mermaid uses for this.
    private static byte[] MakeEdgeLabelsOpaque(byte[] svg)
    {
        var document = XDocument.Load(new MemoryStream(svg));
        foreach (var box in document.Descendants(SvgNamespace + "rect").Where(IsEdgeLabelBox))
        {
            box.SetAttributeValue("style", "fill:#ffffff;opacity:1");
        }

        return Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
    }

    private static bool IsEdgeLabelBox(XElement rect) =>
        HasClass(rect, "background") && rect.Ancestors().Any(a => HasClass(a, "edgeLabel"));

    private static bool HasClass(XElement element, string className) =>
        element.Attribute("class")?.Value.Split(' ').Contains(className) == true;

    private static (string Stderr, int ExitCode) RunMmdc(string input, string output, string config)
    {
        var args = new[] { "-i", input, "-o", output, "-c", config };
        ProcessStartInfo startInfo;
        if (OperatingSystem.IsWindows())
        {
            // mmdc is an npm shim (.cmd), which only cmd.exe can launch.
            var command = "mmdc " + string.Join(' ', args.Select(a => a.StartsWith('-') ? a : $"\"{a}\""));
            startInfo = new ProcessStartInfo("cmd.exe") { Arguments = $"/d /s /c \"{command}\"" };
        }
        else
        {
            startInfo = new ProcessStartInfo("mmdc");
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;

        try
        {
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start mmdc.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            stdout.Wait();
            return (stderr, process.ExitCode);
        }
        catch (Win32Exception)
        {
            return ("mmdc was not found on PATH", -1);
        }
    }
}
