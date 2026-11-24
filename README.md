# DocumentGeneratorTool

A command-line tool that converts a Markdown file into a professional PDF or HTML document. Markdown is parsed with [Markdig](https://github.com/xoofx/markdig), converted to [Typst](https://typst.app) markup, and rendered through a Typst template. The default template is the **GMS technical document template**, styled after [gms.ca](https://gms.ca) with Public Sans.

## Features

- **PDF and HTML output.** Both come from the same Typst template. HTML is styled by the template's `theme.css`.
- **Templates.** Each template is a folder under `templates\`. Add one and select it with `--template`.
- **Title page and table of contents.** The first level-1 heading becomes the document title. The paragraphs that follow it, up to the next heading, become the subtitle. A table of contents is generated automatically on its own page.
- **Mermaid diagrams.** Rendered to SVG and sized to 70% of the text width. A diagram taller than one page is scaled down so it fits on a single page, and the heading above it stays on the same page.
- **Syntax-highlighted code.** Common fence names such as `csharp`, `shell` and `pwsh` are mapped to Typst's language names. PowerShell is supported through a bundled syntax definition.
- **Hyphenated line breaks.** Long unbroken words are given soft-hyphen break points, so Typst shows a hyphen only where a line actually breaks.
- **Tables.** Centred, with no outer border, inner lines only, and a bold rule under the header.
- **Auto-numbered lists.** The template numbers ordered lists (`1.`, `a.`, `i.`), so the numbers in the Markdown do not matter. Bullets change glyph by depth (`•`, `–`, `▪`).
- **Bundled fonts.** Public Sans (SIL Open Font License, see `fonts\OFL.txt`) and the GMS logo are included.

## Requirements

| Requirement | Needed for | Notes |
| --- | --- | --- |
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | Building and running | The project targets `net10.0` with the `win-x64` runtime identifier. |
| Windows x64 | Running | The `win-x64` runtime identifier is pinned in `DocumentGeneratorTool.csproj`. Change it for other platforms. |
| [Node.js](https://nodejs.org) and `@mermaid-js/mermaid-cli` | Mermaid diagrams | Install with `npm install -g @mermaid-js/mermaid-cli`. This provides `mmdc`. Without it, diagrams fall back to their source as a code block. |
| Chrome or Chromium | `mmdc` | Puppeteer needs a browser. Set `PUPPETEER_EXECUTABLE_PATH` if it cannot find one. |

## Building

```powershell
dotnet build
```

Build output goes to `bin\Debug\net10.0\win-x64\`. The `templates`, `fonts` and `syntaxes` folders are copied there, so the executable can be run from its output folder.

## Usage

```powershell
dotnet run -- <input.md> [options]
```

Or run the built executable directly:

```powershell
DocumentGeneratorTool.exe <input.md> [--template <name>] [--format pdf|html] [--output <path>]
```

| Option | Short | Description |
| --- | --- | --- |
| `--template <name>` | `-t` | Template folder under `templates\`. Default: `gms-technical-document`. |
| `--format <pdf\|html>` | `-f` | Output format. Default: `pdf`. |
| `--output <path>` | `-o` | Output file. Default: the input file name with the format's extension, next to the input. |
| `--help` | `-h` | Show usage. |

### Examples

```powershell
# PDF next to the input file
DocumentGeneratorTool.exe docs\design.md

# HTML with an explicit output path
DocumentGeneratorTool.exe docs\design.md --format html --output out\design.html

# PDF using a custom template
DocumentGeneratorTool.exe docs\design.md --template my-template
```

### Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Success. The output path is printed to standard output. |
| `1` | Typst failed to render the document, or a file could not be written. |
| `2` | Usage error: a bad option, an unknown format, a missing input file, or an unknown template. |

Warnings do not change the exit code. Examples are a missing image, which is replaced by its alt text, and a Mermaid diagram that fails to render, which is replaced by its source.

## Writing Markdown

The converter supports the following Markdown:

- Headings (levels 1 to 6). The first level-1 heading is the document title.
- Paragraphs, emphasis, strong text, inline code and links.
- Nested bullet and ordered lists.
- Pipe tables.
- Fenced code blocks with a language tag.
- Block quotes and thematic breaks.
- Images. Only local relative paths are loaded, relative to the Markdown file. A missing image falls back to its alt text.
- Mermaid diagrams, in fenced blocks tagged `mermaid`.

Example:

````markdown
# Payment Service Design

Architecture overview for the payment service, covering the API layer, the queue and the ledger.

## Context

1. Clients call the **Payments API**.
2. Requests are queued for processing.
   - Retries use exponential backoff.

```mermaid
flowchart LR
    A[Client] --> B[Payments API]
    B --> C[(Ledger)]
```
````

In this example, the title is "Payment Service Design" and the subtitle is the paragraph below it.

## Templates

A template is a folder under `templates\` that contains a `template.typ` file. The default template is `templates\gms-technical-document\`.

`template.typ` must define:

- `render(title: ..., subtitle: ...)`, a function that receives the converted Markdown as its body content. `title` and `subtitle` are `none` when the document has no level-1 heading.
- `diagram-max-height`, a length that is the tallest a Mermaid diagram may be on one page. Diagrams taller than this are scaled to fit.
- `numbered(depth, body)`, a function that wraps an ordered list at the given depth. The converter calls it for every ordered list.

The renderer imports `render`, `diagram-max-height` and `numbered` by name. If a template adds a symbol that the converter needs, add it to the import line in [DocumentRenderer.cs](Rendering/DocumentRenderer.cs).

For HTML output, the template can also ship a `theme.css`. The GMS template uses one to style the HTML output.

To create a template, copy `templates\gms-technical-document\` to a new folder, edit it, and run the tool with `--template <folder name>`.

## Project structure

```
DocumentGeneratorTool/
├── Program.cs                  Entry point: validates arguments and inputs, writes the output file
├── Cli/
│   └── CommandLineOptions.cs   Argument parsing and usage text
├── Conversion/
│   ├── MarkdownToTypst.cs      Markdown to Typst markup, including lists, tables, code and images
│   └── ConvertedDocument.cs    Title, subtitle, body and assets produced by the conversion
├── Rendering/
│   ├── DocumentRenderer.cs     Builds main.typ and compiles it with TypstInterop
│   └── OutputFormat.cs         pdf or html
├── Diagrams/
│   └── MermaidRenderer.cs      Runs mmdc to render Mermaid to SVG
├── templates/
│   └── gms-technical-document/ Default template: template.typ, theme.css, assets
├── fonts/                      Public Sans (OFL) used by the templates
└── syntaxes/                   Extra syntax definitions for code highlighting
```

## How it works

1. `Program.cs` checks the arguments, the template and the input file.
2. `MarkdownToTypst` parses the Markdown with Markdig and emits Typst markup. Mermaid diagrams are rendered to SVG by `MermaidRenderer`, and images are passed to Typst as assets.
3. `DocumentRenderer` writes a `main.typ` that imports the template, calls `render` with the title, subtitle and body, and compiles it. Fonts are loaded from `fonts\` only, so an installed copy of Public Sans does not replace the bundled weights.
4. The compiled bytes are written to the output path.

## Known limitations

- Only Windows x64 is configured. The `win-x64` runtime identifier is set in the project file.
- Ordered lists always start at 1. The start number in the Markdown is ignored, because the template owns the numbering.
- Bullet glyphs cycle after three depths, so depth 4 uses `•` again.
- Loose lists (with blank lines between items) are rendered as tight lists, for consistent spacing.
- Images must be local relative paths. Remote URLs are not downloaded.
