namespace MangaBox.Auth;

public static class AuthExtensions
{
    public static string? Claim(this ClaimsPrincipal? principal, string claim)
    {
        return principal?.FindFirst(claim)?.Value;
    }

    public static TokenUser? UserFromIdentity(this ClaimsPrincipal? principal)
    {
        if (principal == null) return null;

        string getClaim(string key) => principal.Claim(key) ?? "";

        var id = getClaim(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(id)) return null;

        return new TokenUser
        {
            Id = id,
            Nickname = getClaim(ClaimTypes.Name),
            Email = getClaim(ClaimTypes.Email),
            Avatar = getClaim(ClaimTypes.UserData),
            Provider = getClaim(ClaimTypes.PrimarySid),
            ProviderId = getClaim(ClaimTypes.PrimaryGroupSid)
        };
    }

    internal static string GetOAuth(this IConfiguration config, string key)
    {
        return config["OAuth:" + key] ?? throw new NullReferenceException($"OAuth:{key} is not set");
    }

    internal static int GetOAuthInt(this IConfiguration config, string key)
    {
        return int.Parse(config.GetOAuth(key));
    }

    internal static SymmetricSecurityKey GetKey(this string key)
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    internal static SymmetricSecurityKey GetKey(this IConfiguration config)
    {
        return config.GetOAuth("Key").GetKey();
    }

    internal static TokenValidationParameters GetParameters(this IConfiguration config)
    {
        return new TokenValidationParameters
        {
            IssuerSigningKey = config.GetKey(),
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            ValidAudience = config.GetOAuth("Audience"),
            ValidIssuer = config.GetOAuth("Issuer"),
        };
    }
}
