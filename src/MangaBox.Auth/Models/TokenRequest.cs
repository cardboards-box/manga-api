namespace MangaBox.Auth;

public record class TokenRequest(string Code, string Secret, string AppId);
