namespace MangaBox.Auth;

public interface ITokenService
{
    TokenResult ParseToken(string token);

    Task<TokenResponse?> ResolveCode(string code);

    string GenerateToken(Guid profileId, TokenResponse resp, params string[] roles);
}

public class TokenService(
    IConfiguration _config,
    IApiService _api) : ITokenService
{
    public string AppId => _config.GetOAuth(nameof(AppId));
    public string Secret => _config.GetOAuth(nameof(Secret));
    public string Issuer => _config.GetOAuth(nameof(Issuer));
    public string Audience => _config.GetOAuth(nameof(Audience));
    public string Url => _config.GetOAuth(nameof(Url));
    public int ExpiresMinutes => _config.GetOAuthInt(nameof(ExpiresMinutes));

    public Task<TokenResponse?> ResolveCode(string code)
    {
        var request = new TokenRequest(code, Secret, AppId);
        return _api.Post<TokenResponse, TokenRequest>(Url, request);
    }

    public TokenResult ParseToken(string token)
    {
        var validationParams = _config.GetParameters();

        var handler = new JwtSecurityTokenHandler();

        var principals = handler.ValidateToken(token, validationParams, out var securityToken);

        return new(principals, securityToken);
    }

    public string GenerateToken(Guid profileId, TokenResponse resp, params string[] roles)
    {
        return new JwtToken(_config.GetKey())
            .SetAudience(Audience)
            .SetIssuer(Issuer)
            .Expires(ExpiresMinutes)
            .AddClaim(ClaimTypes.NameIdentifier, profileId.ToString())
            .AddClaim(ClaimTypes.Actor, resp.User.Id)
            .AddClaim(ClaimTypes.Name, resp.User.Nickname)
            .AddClaim(ClaimTypes.Email, resp.User.Email)
            .AddClaim(ClaimTypes.UserData, resp.User.Avatar)
            .AddClaim(ClaimTypes.PrimarySid, resp.Provider)
            .AddClaim(ClaimTypes.PrimaryGroupSid, resp.User.ProviderId)
            .AddClaim(roles.Select(t => new Claim(ClaimTypes.Role, t)).ToArray())
            .Write();
    }
}