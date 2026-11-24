using System.Diagnostics.CodeAnalysis;
using DocumentGeneratorTool.Rendering;

namespace DocumentGeneratorTool.Cli;

/// <summary>The options given on the command line.</summary>
internal sealed record CommandLineOptions(string Input, string Template, OutputFormat Format, string? Output)
{
    public const string DefaultTemplate = "gms-technical-document";

    public const string Usage = """
        Usage: DocumentGeneratorTool <input.md> [--template <name>] [--format pdf|html] [--output <path>]

          -t, --template <name>   Template folder under templates\ (default: gms-technical-document)
          -f, --format <format>   pdf or html (default: pdf)
          -o, --output <path>     Output file (default: input file name with the format's extension, next to the input)
          -h, --help              Show this help
        """;

    /// <summary>
    /// Parses the arguments. Returns false when the program should stop without rendering. In that case
    /// <paramref name="exitCode"/> is 0 after help was shown, or 2 after a usage error was printed.
    /// </summary>
    public static bool TryParse(string[] args, [NotNullWhen(true)] out CommandLineOptions? options, out int exitCode)
    {
        options = null;
        exitCode = 0;

        string? input = null;
        var template = DefaultTemplate;
        var format = "pdf";
        string? output = null;

        var i = 0;
        while (i < args.Length)
        {
            var arg = args[i];
            if (arg is "-h" or "--help")
            {
                Console.WriteLine(Usage);
                return false;
            }

            if (arg is "-t" or "--template" or "-f" or "--format" or "-o" or "--output")
            {
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"Option {arg} needs a value.");
                    exitCode = 2;
                    return false;
                }

                var value = args[i + 1];
                i += 2;
                switch (arg)
                {
                    case "-t" or "--template":
                        template = value;
                        break;
                    case "-f" or "--format":
                        format = value;
                        break;
                    default:
                        output = value;
                        break;
                }
                continue;
            }

            if (arg.StartsWith('-'))
            {
                Console.Error.WriteLine($"Unknown option: {arg}");
                Console.Error.WriteLine(Usage);
                exitCode = 2;
                return false;
            }

            if (input is not null)
            {
                Console.Error.WriteLine("Only one input file can be given.");
                exitCode = 2;
                return false;
            }
            input = arg;
            i++;
        }

        if (input is null)
        {
            Console.Error.WriteLine(Usage);
            exitCode = 2;
            return false;
        }

        if (!Enum.TryParse<OutputFormat>(format, ignoreCase: true, out var outputFormat))
        {
            Console.Error.WriteLine($"Unsupported format '{format}'. Use pdf or html.");
            exitCode = 2;
            return false;
        }

        options = new CommandLineOptions(input, template, outputFormat, output);
        return true;
    }
}
