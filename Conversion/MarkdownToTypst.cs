using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DocumentGeneratorTool.Conversion;

/// <summary>Converts Markdown into Typst markup that is passed to a template's <c>render</c> function.</summary>
internal sealed class MarkdownToTypst
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras()
        .Build();

    // Characters with meaning in Typst markup. Escaping "-" and "/" also stops "--" and "//" from being read as shorthand or comments.
    private const string TypstMarkupChars = "\\#*_`$@<>[]~/=+-";

    // A soft hyphen is a break opportunity that Typst draws as a hyphen only when the line breaks there.
    private const char SoftHyphen = '\u00AD';
    private const int MaxUnbrokenRun = 20;

    public const string ImageRoot = "/assets/";
    public const string DiagramRoot = "/diagrams/";

    private readonly Func<string, byte[]?> _loadImage;
    private readonly Func<string, byte[]?> _renderDiagram;
    private readonly Dictionary<string, byte[]> _assets = [];
    private int _diagramCount;
    private int _listDepth;

    // Set while a heading's text is rendered, so its words are converted to book case.
    private bool _bookCaseHeading;
    private bool _atHeadingStart;

    // loadImage receives the decoded image path from the Markdown and returns its bytes, or null when the image is unavailable.
    // renderDiagram receives Mermaid source and returns SVG bytes, or null to show the source as a code block instead.
    public MarkdownToTypst(Func<string, byte[]?> loadImage, Func<string, byte[]?> renderDiagram)
    {
        _loadImage = loadImage;
        _renderDiagram = renderDiagram;
    }

    // Files referenced by the Typst markup, keyed by their virtual path. The renderer registers each one.
    public IReadOnlyDictionary<string, byte[]> Assets => _assets;

    // Typst virtual path for an image.
    public static string ImageVirtualPath(string relativePath) => ImageRoot + relativePath.Replace('\\', '/');

    public ConvertedDocument Convert(string markdown)
    {
        var blocks = Markdown.Parse(markdown, Pipeline).ToList();
        var titleIndex = blocks.FindIndex(block => block is HeadingBlock { Level: 1 });
        if (titleIndex < 0)
        {
            return new ConvertedDocument(null, null, null, RenderBlocks(blocks));
        }

        // The first level-1 heading is the title. The paragraphs straight after it become the subtitle on the title page.
        var title = (HeadingBlock)blocks[titleIndex];
        var subtitleEnd = titleIndex + 1;
        while (subtitleEnd < blocks.Count && blocks[subtitleEnd] is ParagraphBlock)
        {
            subtitleEnd++;
        }
        var subtitle = blocks.GetRange(titleIndex + 1, subtitleEnd - titleIndex - 1);
        var body = blocks.Take(titleIndex).Concat(blocks.Skip(subtitleEnd));

        var atStart = true;
        return new ConvertedDocument(
            RenderHeading(title.Inline),
            subtitle.Count == 0 ? null : RenderBlocks(subtitle),
            ToBookCase(PlainText(title.Inline), ref atStart),
            RenderBlocks(body));
    }

    private string RenderBlocks(IEnumerable<Block> blocks)
    {
        var parts = new List<string>();
        foreach (var block in blocks)
        {
            var text = RenderBlock(block);
            if (text.Length > 0)
            {
                parts.Add(text);
            }
        }
        return string.Join("\n\n", parts);
    }

    private string RenderBlock(Block block) => block switch
    {
        HeadingBlock heading => new string('=', heading.Level) + " " + RenderHeading(heading.Inline),
        ParagraphBlock paragraph => RenderInline(paragraph.Inline),
        CodeBlock code => RenderCodeBlock(code),
        QuoteBlock quote => "#quote(block: true)[\n" + RenderBlocks(quote) + "\n]",
        ListBlock list => RenderList(list),
        ThematicBreakBlock => "#context if target() == \"html\" { html.elem(\"hr\") } else { line(length: 100%) }",
        Table table => RenderTable(table),
        // Raw HTML and unsupported block types are skipped.
        _ => string.Empty,
    };

    private string RenderCodeBlock(CodeBlock code)
    {
        var lines = new List<string>(code.Lines.Count);
        for (var i = 0; i < code.Lines.Count; i++)
        {
            lines.Add(code.Lines.Lines[i].Slice.ToString());
        }

        var text = string.Join('\n', lines);
        var language = code is FencedCodeBlock fenced
            ? (fenced.Info ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            : null;
        if (string.Equals(language, "mermaid", StringComparison.OrdinalIgnoreCase) && RenderDiagram(text) is { } diagram)
        {
            return diagram;
        }

        var languageArg = string.IsNullOrEmpty(language) ? string.Empty : $", lang: {ToTypstString(TypstLanguage(language))}";
        return $"#raw({ToTypstString(text)}, block: true{languageArg})";
    }

    // Typst highlights by the names in its bundled syntax set. Common fence names that differ from those names are mapped here.
    private static string TypstLanguage(string language) => LanguageAliases.TryGetValue(language, out var mapped) ? mapped : language;

    private static readonly Dictionary<string, string> LanguageAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"] = "cs",
        ["fsharp"] = "fs",
        ["shell"] = "sh",
        ["pwsh"] = "powershell",
    };

    // Paged output sizes images to the page. HTML has no page width to resolve a percentage against, so it uses a fixed width.
    // Diagrams are 70% of the text width (A4 minus 2cm margins = 17cm, so 11.9cm in HTML). Images use the full width.
    // A diagram taller than the template's diagram-max-height is scaled down so it fits on one page.
    private static string ImageMarkup(string virtualPath, bool diagram)
    {
        var path = ToTypstString(virtualPath);
        var htmlWidth = diagram ? "11.9cm" : "15cm";
        var paged = diagram
            ? $"layout(size => {{ let w = size.width * 0.7; let h = measure(image({path}, width: w)).height; let fit = if h > diagram-max-height {{ w * (diagram-max-height / h) }} else {{ w }}; align(center, image({path}, width: fit)) }})"
            : $"image({path}, width: 100%)";
        return $"#context if target() == \"html\" {{ html.frame(image({path}, width: {htmlWidth})) }} else {{ {paged} }}";
    }

    private string? RenderDiagram(string source)
    {
        var svg = _renderDiagram(source);
        if (svg is null)
        {
            return null;
        }

        var path = DiagramRoot + "diagram-" + (++_diagramCount) + ".svg";
        _assets[path] = svg;
        return ImageMarkup(path, diagram: true);
    }

    // Every item uses the same marker, so numbers and bullet glyphs come from the template, never from the Markdown.
    // Ordered lists are wrapped with their depth so the template can pick the numbering style for that level.
    private string RenderList(ListBlock list)
    {
        _listDepth++;
        try
        {
            var marker = list.IsOrdered ? "+" : "-";
            var items = list.OfType<ListItemBlock>()
                .Select(item => PrefixListItem(marker, RenderItemBlocks(item)))
                .ToList();
            var text = string.Join('\n', items);
            return list.IsOrdered ? $"#numbered({_listDepth})[\n{text}\n]" : text;
        }
        finally
        {
            _listDepth--;
        }
    }

    // A nested list follows its parent item on the next line, with no blank line, so it does not get a paragraph gap.
    private string RenderItemBlocks(ListItemBlock item)
    {
        var text = new StringBuilder();
        var previous = false;
        foreach (var block in item)
        {
            var rendered = RenderBlock(block);
            if (rendered.Length == 0)
            {
                continue;
            }

            if (previous)
            {
                text.Append(block is ListBlock ? "\n" : "\n\n");
            }
            text.Append(rendered);
            previous = true;
        }
        return text.ToString();
    }

    private static string PrefixListItem(string marker, string body)
    {
        var lines = body.Split('\n');
        var padding = new string(' ', marker.Length + 1);
        lines[0] = lines[0].Length > 0 ? marker + " " + lines[0] : marker;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length > 0)
            {
                lines[i] = padding + lines[i];
            }
        }
        return string.Join('\n', lines);
    }

    private string RenderTable(Table table)
    {
        var columns = table.ColumnDefinitions.Count;
        var alignments = table.ColumnDefinitions
            .Select(d => d.Alignment switch
            {
                TableColumnAlign.Center => "center",
                TableColumnAlign.Right => "right",
                _ => "left",
            });

        var cells = new List<string>();
        foreach (var row in table.OfType<TableRow>())
        {
            var rowCells = row.OfType<TableCell>()
                .Take(columns)
                .Select(cell => RenderBlocks(cell))
                .Select(content => row.IsHeader ? $"[*{content}*]" : $"[{content}]")
                .ToList();
            while (rowCells.Count < columns)
            {
                rowCells.Add("[]");
            }
            cells.AddRange(rowCells);
        }

        return "#table(\n"
            + $"  columns: {columns},\n"
            + $"  align: ({string.Join(", ", alignments)}),\n"
            + "  " + string.Join(",\n  ", cells) + ",\n"
            + ")";
    }

    // Plain text of a heading, for the document title metadata.
    private static string PlainText(ContainerInline? container)
    {
        if (container is null)
        {
            return string.Empty;
        }

        return string.Concat(container.Descendants().Select(inline => inline switch
        {
            LiteralInline literal => literal.Content.ToString(),
            CodeInline code => code.Content,
            _ => string.Empty,
        }));
    }

    // Headings at every level, including the title, are set in book case so the body, contents page and title page agree.
    private string RenderHeading(ContainerInline? container)
    {
        _bookCaseHeading = true;
        _atHeadingStart = true;
        try
        {
            return RenderInline(container);
        }
        finally
        {
            _bookCaseHeading = false;
        }
    }

    private string RenderInline(ContainerInline? container)
    {
        if (container is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var inline in container)
        {
            AppendInline(sb, inline);
        }
        return sb.ToString();
    }

    private void AppendInline(StringBuilder sb, Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                sb.Append(EscapeText(_bookCaseHeading
                    ? ToBookCase(literal.Content.ToString(), ref _atHeadingStart)
                    : literal.Content.ToString()));
                break;
            case HtmlEntityInline entity:
                sb.Append(EscapeText(entity.Transcoded.ToString()));
                break;
            case LineBreakInline lineBreak:
                sb.Append(lineBreak.IsHard ? "#linebreak()" : " ");
                break;
            case CodeInline code:
                sb.Append($"#raw({ToTypstString(code.Content)})");
                break;
            case EmphasisInline emphasis:
                AppendEmphasis(sb, emphasis);
                break;
            case LinkInline { IsImage: true } image:
                sb.Append(RenderImage(image));
                break;
            case LinkInline link:
                sb.Append(string.IsNullOrEmpty(link.Url)
                    ? RenderInline(link)
                    : $"#link({ToTypstString(link.Url)})[{RenderInline(link)}]");
                break;
            case AutolinkInline autolink:
                sb.Append($"#link({ToTypstString(autolink.Url)})[{EscapeText(autolink.Url)}]");
                break;
            case ContainerInline container:
                sb.Append(RenderInline(container));
                break;
            // Inline HTML is shown as literal text so the author sees it, rather than silently dropping it.
            case HtmlInline html:
                sb.Append(EscapeText(html.Tag));
                break;
            // Other unsupported inline types are skipped.
        }
    }

    private string RenderImage(LinkInline image)
    {
        var path = Uri.UnescapeDataString(image.Url ?? string.Empty);
        var bytes = _loadImage(path);
        if (bytes is null)
        {
            // Unavailable images fall back to their alt text.
            return RenderInline(image);
        }

        var virtualPath = ImageVirtualPath(path);
        _assets[virtualPath] = bytes;
        return ImageMarkup(virtualPath, diagram: false);
    }

    private void AppendEmphasis(StringBuilder sb, EmphasisInline emphasis)
    {
        var inner = RenderInline(emphasis);
        switch (emphasis.DelimiterChar)
        {
            case '*' or '_' when emphasis.DelimiterCount >= 2:
                sb.Append($"*{inner}*");
                break;
            case '*' or '_':
                sb.Append($"_{inner}_");
                break;
            case '~' when emphasis.DelimiterCount >= 2:
                sb.Append($"#strike[{inner}]");
                break;
            case '~':
                sb.Append($"#sub[{inner}]");
                break;
            case '^':
                sb.Append($"#super[{inner}]");
                break;
            case '+':
                sb.Append($"#underline[{inner}]");
                break;
            default:
                sb.Append(inner);
                break;
        }
    }

    // Small words stay lower case in book case unless they start the heading.
    private static readonly HashSet<string> SmallWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "as", "at", "but", "by", "for", "from", "in", "nor", "of", "on", "or", "the", "to", "via", "vs", "with",
    };

    // Capitalises each word and each part of a hyphenated or slash-separated word.
    // A word that already has a capital (such as HTML or MermaidRenderer) is kept as written.
    private static string ToBookCase(string text, ref bool atHeadingStart)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var token in Regex.Split(text, @"(\s+)"))
        {
            if (token.Length == 0 || char.IsWhiteSpace(token[0]))
            {
                sb.Append(token);
                continue;
            }

            var letters = new string(token.Where(char.IsLetter).ToArray());
            if (!atHeadingStart && SmallWords.Contains(letters))
            {
                sb.Append(token.ToLowerInvariant());
            }
            else if (token.Any(char.IsUpper))
            {
                sb.Append(token);
            }
            else
            {
                var startOfPart = true;
                foreach (var c in token)
                {
                    sb.Append(startOfPart && char.IsLetter(c) ? char.ToUpperInvariant(c) : c);
                    startOfPart = c is '-' or '/' or '(' or '"';
                }
            }
            atHeadingStart = false;
        }
        return sb.ToString();
    }

    private static string EscapeText(string text)
    {
        var sb = new StringBuilder(text.Length + 8);
        var run = 0;
        foreach (var c in text)
        {
            // Typst only wraps at break opportunities, so long unbroken runs get a zero-width space to keep them inside the page and table cells.
            if (char.IsWhiteSpace(c))
            {
                run = 0;
            }
            else if (++run > MaxUnbrokenRun)
            {
                sb.Append(SoftHyphen);
                run = 1;
            }

            if (TypstMarkupChars.Contains(c))
            {
                sb.Append('\\');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    internal static string ToTypstString(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", string.Empty)
            .Replace("\n", "\\n");
        return "\"" + escaped + "\"";
    }
}
