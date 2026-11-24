using DocumentGeneratorTool.Cli;
using DocumentGeneratorTool.Rendering;

if (!CommandLineOptions.TryParse(args, out var options, out var exitCode))
{
    return exitCode;
}

var baseDir = AppContext.BaseDirectory;
var templatePath = DocumentRenderer.TemplateFilePath(baseDir, options.Template);
if (Path.GetFileName(options.Template) != options.Template || options.Template is "." or "..")
{
    Console.Error.WriteLine($"Invalid template name '{options.Template}'. Use the name of a folder under templates\\.");
    return 2;
}
if (!File.Exists(templatePath))
{
    Console.Error.WriteLine($"Template '{options.Template}' not found: {templatePath}");
    return 2;
}

var inputPath = Path.GetFullPath(options.Input);
if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Input file not found: {inputPath}");
    return 2;
}

var extension = options.Format == OutputFormat.Html ? ".html" : ".pdf";
var outputPath = options.Output is null
    ? Path.ChangeExtension(inputPath, extension)
    : Path.GetFullPath(options.Output);

try
{
    var markdown = File.ReadAllText(inputPath);
    using var renderer = new DocumentRenderer(baseDir);
    var bytes = renderer.Render(markdown, Path.GetDirectoryName(inputPath)!, options.Template, options.Format);

    var outputDir = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrEmpty(outputDir))
    {
        Directory.CreateDirectory(outputDir);
    }
    File.WriteAllBytes(outputPath, bytes);
    Console.WriteLine(outputPath);
    return 0;
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine("Typst failed to render the document:");
    Console.Error.WriteLine(ex.Message);
    return 1;
}
catch (IOException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
