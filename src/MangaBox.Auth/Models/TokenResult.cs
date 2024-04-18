namespace MangaBox.Auth;

public record class TokenResult(ClaimsPrincipal Principal, SecurityToken Token);
