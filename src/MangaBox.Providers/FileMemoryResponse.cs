namespace MangaBox.Providers;

public record class FileMemoryResponse(
    MemoryStream Stream,
    long Length,
    string FileName,
    string MimeType);

public record class FileResponse(
    Stream Stream,
    long Length,
    string FileName,
    string MimeType);
