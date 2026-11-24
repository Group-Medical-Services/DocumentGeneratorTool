using DocumentGeneratorTool.Conversion;
using DocumentGeneratorTool.Diagrams;
using TypstInterop;
using TypstInterop.Models;

namespace DocumentGeneratorTool.Rendering;

/// <summary>
/// Renders Markdown through a Typst template. A template is a folder under <c>templates</c> containing
/// <c>template.typ</c>, which must define <c>#let render(body, title: none, subtitle: none) = ...</c> and
/// <c>#let diagram-max-height</c>, the tallest length a Mermaid diagram may take on one page. The body is the converted
/// Markdown as content. The title and subtitle come from the first level-1 heading and the paragraphs after it, and are
/// <c>none</c> when the Markdown has no level-1 heading.
/// </summary>
internal sealed class DocumentRenderer : IDisposable
{
    private readonly string _baseDir;
    private readonly List<byte[]> _fonts;
    private readonly TypstCompiler _compiler;
    private readonly MermaidRenderer _mermaid = new();

    private const string CustomSyntaxPath = "/syntaxes/powershell.sublime-syntax";

    public DocumentRenderer(string baseDir)
    {
        _baseDir = baseDir;
        var fontsDir = Path.Combine(baseDir, "fonts");
        _fonts = Directory.Exists(fontsDir)
            ? Directory.EnumerateFiles(fontsDir, "*.*")
                .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllBytes)
                .ToList()
            : [];

        // Embedded Typst fonts stay available, and the bundled fonts folder adds to them. System fonts are
        // excluded so an installed variable Public Sans cannot shadow the bundled static weights.
        _compiler = new TypstCompiler(new TypstCompilerOptions { FontsSource = TypstFontsSource.DefaultOnly });
    }

    public static string TemplateFilePath(string baseDir, string templateName) =>
        Path.Combine(baseDir, "templates", templateName, "template.typ");

    public byte[] Render(string markdown, string markdownDir, string templateName, OutputFormat format)
    {
        var converter = new MarkdownToTypst(path => LoadImage(path, markdownDir), _mermaid.TryRender);
        var document = converter.Convert(markdown);

        var templateImport = MarkdownToTypst.ToTypstString($"/templates/{templateName}/template.typ");
        var lines = new List<string>
        {
            $"#import {templateImport}: render, diagram-max-height, numbered",
            // Languages Typst does not ship a grammar for come from the syntaxes folder next to the executable.
            $"#set raw(syntaxes: {MarkdownToTypst.ToTypstString(CustomSyntaxPath)})",
        };
        if (document.DocumentTitle is { } docTitle)
        {
            // The document title sets the PDF metadata and the HTML <title>. It has to be a top-level set rule.
            lines.Add($"#set document(title: {MarkdownToTypst.ToTypstString(docTitle)})");
        }
        lines.Add($"#render(title: {ContentArgument(document.Title)}, subtitle: {ContentArgument(document.Subtitle)})[");
        lines.Add(document.Body);
        lines.Add("]");
        lines.Add(string.Empty);
        var mainFile = string.Join('\n', lines);

        var typstFormat = format == OutputFormat.Html ? TypstOutputFormat.Html : TypstOutputFormat.Pdf;
        var result = _compiler.Compile(c =>
        {
            c.WithRoot(_baseDir).WithSource(mainFile).WithFormat(typstFormat);
            foreach (var font in _fonts)
            {
                c.WithFont(font);
            }
            foreach (var (path, bytes) in converter.Assets)
            {
                c.WithFile(path, bytes);
            }
            return c;
        });

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.ErrorMessage?.Trim() ?? "Typst compilation failed.");
        }
        return result.Output.ToArray();
    }

    // Typst content for an optional piece of markup, or none when there is no markup.
    private static string ContentArgument(string? markup) => markup is null ? "none" : $"[{markup}]";

    private static byte[]? LoadImage(string path, string markdownDir)
    {
        var normalized = path.Replace('\\', '/');
        var isLocal = normalized.Length > 0
            && !normalized.Contains(':')
            && !Path.IsPathRooted(normalized)
            && !normalized.Split('/').Contains("..");
        if (!isLocal)
        {
            Console.Error.WriteLine($"Warning: image '{path}' is not a local relative path; using its alt text instead.");
            return null;
        }

        var file = Path.Combine(markdownDir, normalized);
        if (!File.Exists(file))
        {
            Console.Error.WriteLine($"Warning: image '{path}' not found; using its alt text instead.");
            return null;
        }

        return File.ReadAllBytes(file);
    }

    public void Dispose() => _compiler.Dispose();
}
