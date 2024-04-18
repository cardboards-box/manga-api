namespace MangaBox;

using Auth;

public static class DiExtensions
{
    public static IDependencyResolver AddOAuth(this IDependencyResolver resolver)
    {
        return resolver
            .Transient<ITokenService, TokenService>()
            .Transient<IAuthService, AuthService>()

            .AddServices((s, c) => s
                .AddAuthentication(opt =>
                {
                    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    opt.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(opt =>
                {
                    opt.SaveToken = true;
                    opt.RequireHttpsMetadata = false;
                    opt.TokenValidationParameters = c.GetParameters();
                }));
    }
}
