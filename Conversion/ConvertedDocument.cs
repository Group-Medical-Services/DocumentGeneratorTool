namespace DocumentGeneratorTool.Conversion;

/// <summary>
/// The parts of a converted document. Title, Subtitle, and DocumentTitle are null when the Markdown has no level-1 heading.
/// </summary>
internal sealed record ConvertedDocument(string? Title, string? Subtitle, string? DocumentTitle, string Body);
