namespace MangaBox.Services.Imaging;

/// <summary>
/// Represents a file result
/// </summary>
/// <param name="Error">The error that occurred (if any)</param>
/// <param name="Stream">The stream of the zip file</param>
/// <param name="FileName">The name of the zip file</param>
/// <param name="MimeType">The mime-type / content-type</param>
public record class SingleFileResult(
	string? Error,
	Stream? Stream = null,
	string? FileName = null,
	string? MimeType = null);
