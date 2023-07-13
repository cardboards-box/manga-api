using System.Security.Claims;

namespace CardboardBox.Manga.Api;

public static class Extensions
{
    public static string? GroupId(this ClaimsPrincipal principal) => principal.Claim(ClaimTypes.GroupSid);

    public static string? GroupId(this ControllerBase ctrl) => ctrl?.User?.GroupId();

    public static string? Claim(this ClaimsPrincipal principal, string claim)
    {
        return principal?.FindFirst(claim)?.Value;
    }

    public static string? Claim(this ControllerBase ctrl, string claim)
    {
        if (ctrl.User == null) return null;
        return ctrl.User.Claim(claim);
    }

    public static TokenUser? UserFromIdentity(this ControllerBase ctrl)
    {
        if (ctrl.User == null) return null;

        return ctrl.User.UserFromIdentity();
    }

    public static TokenUser? UserFromIdentity(this ClaimsPrincipal principal)
    {
        if (principal == null) return null;

        var getClaim = (string key) => principal.Claim(key) ?? "";

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

    public class TokenUser
    {
        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = string.Empty;

        [JsonPropertyName("avatar")]
        public string Avatar { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonPropertyName("providerId")]
        public string ProviderId { get; set; } = string.Empty;
    }
}
