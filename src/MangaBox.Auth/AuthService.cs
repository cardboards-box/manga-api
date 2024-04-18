namespace MangaBox.Auth;

using Models;

/// <summary>
/// Service for resolving login codes via the OAuth service
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Resolve a login code
    /// </summary>
    /// <param name="code">The login code</param>
    /// <param name="existing">The currently logged in user ID (if linking accounts)</param>
    /// <returns>The result of the login request</returns>
    /// <remarks>
    /// Produces: <see cref="Boxed{string}"/> on success, <see cref="BoxedError"/> (401), or <see cref="BoxedError"/> (400)
    /// </remarks>
    Task<Boxed> Login(string code, Guid? existing);
}

internal class AuthService(
    IDbService _db, 
    ITokenService _token) : IAuthService
{
    /// <summary>
    /// Resolve a login code
    /// </summary>
    /// <param name="code">The login code</param>
    /// <param name="existing">The currently logged in user ID (if linking accounts)</param>
    /// <returns>The result of the login request</returns>
    /// <remarks>
    /// Produces: <see cref="Boxed{string}"/> on success, <see cref="BoxedError"/> (401), or <see cref="BoxedError"/> (400)
    /// </remarks>
    public async Task<Boxed> Login(string code, Guid? existing)
    {
        /*
        There are various scenarios that can be resolved here:
        Happy:
            1. The user is logging in for the first time, and no existing profile is linked.
            2. The user is logging in for the second+ time, so resolve the existing profile.
            3. The user is linking an account, so resolve the existing profile and link the new login.
        Unhappy:
            1. The login code is invalid or expired.
            2. The user is linking an account, but the existing profile is not the same as the one being linked.
            3. The user is linking an account, but the target profile does not exist 
               (this shouldn't happen except when a profile has been soft deleted or a malicious actor is 
                attempting to do something nefarious).
        */

        //Resolve the code with the auth provider
        var res = await _token.ResolveCode(code);

        //Response was bad? Return unauthorized.
        if (res is null || !string.IsNullOrEmpty(res.Error))
            return Boxed.Unauthorized(res?.Error ?? "Login Failed - Unknown issue");

        //Resolve the existing login and profile for the user by platform ID
        var (login, profile) = await _db.Logins.ByPlatform(res.User.Id);

        //No currently logged in profile? Not linking accounts, so resolve the profile or create a new one.
        if (existing is null || existing == Guid.Empty)
            return await NoExisting(res, login, profile);

        //No login exists for resolved profile? Create a new login and link it.
        if (login is null || profile is null)
            return await LinkNew(res, existing.Value);

        //Login exists and the profile is the same as the existing one;
        //Just update the login and return the success response.
        if (login.ProfileId == existing)
        {
            await Merge(login, res);
            return await GetSuccess(profile, res);
        }

        //Login exists, but the profile is different from the existing one.
        return Boxed.Bad("Linking Failed - Login linked to a different profile");
    }

    /// <summary>
    /// Login request was successful, and we're not linking accounts, so resolve the profile or create a new one.
    /// </summary>
    /// <param name="response">The response from the Auth service</param>
    /// <param name="login">The login profile for the user (if a profile exists)</param>
    /// <param name="profile">The linked profile for the login (if a profile exists)</param>
    /// <returns>The result of the login request</returns>
    public async Task<Boxed> NoExisting(TokenResponse response, Login? login, Profile? profile)
    {
        //Login and profile exist? Make sure they're up to date and return the success response.
        if (login is not null && profile is not null)
        {
            await Merge(login, response);
            return await GetSuccess(profile, response);
        }
        //Create a new profile to link to the login
        profile = await From(response);
        profile.Id = await _db.Profiles.Insert(profile);
        //Create a new login for the resolved login request
        login = From(response, profile.Id);
        login.Id = await _db.Logins.Insert(login);
        //Set the login as primary for the profile
        profile.PrimaryUser = login.Id;
        await _db.Profiles.Update(profile);
        //Return a successful request
        return await GetSuccess(profile, response);
    }

    /// <summary>
    /// Link a new login to an existing profile
    /// </summary>
    /// <param name="response">The token response from the auth service</param>
    /// <param name="profileId">The profile to link it to</param>
    /// <returns>The result of the login request</returns>
    public async Task<Boxed> LinkNew(TokenResponse response, Guid profileId)
    {
        //Get the existing profile to link the new login to
        var profile = await _db.Profiles.Fetch(profileId);
        //Profile doesn't exist? What the fuck?
        if (profile is null)
            return Boxed.Unauthorized("Login Linking Failed - Profile not found");
        //Create a new login for the resolved token response
        var login = From(response, profile.Id);
        login.Id = await _db.Logins.Insert(login);
        //If the profile doesn't have a primary login, assign this one to it.
        if (profile.PrimaryUser is null || profile.PrimaryUser == Guid.Empty)
        {
            profile.PrimaryUser = login.Id;
            await _db.Profiles.Update(profile);
        }
        //Return a successful request
        return await GetSuccess(profile, response);
    }

    /// <summary>
    /// Login request was successful, so lets resolve roles and generate a token.
    /// </summary>
    /// <param name="profile">The profile that was logged in</param>
    /// <param name="response">The response from the auth service</param>
    /// <returns>The JWT token boxed in a return result</returns>
    public async Task<Boxed<string>> GetSuccess(Profile profile, TokenResponse response)
    {
        //Resolve the profile's roles
        var roles = await _db.Roles.Fetch(profile.RoleIds);
        //Get the role names for use in the JWT token
        var roleNames = roles.Select(r => r.Name).ToArray();
        //Generate the JWT token
        var token = _token.GenerateToken(profile.Id, response, roleNames);
        //Return the token in a boxed result
        return Boxed.Ok(token);
    }

    /// <summary>
    /// Turn a token response and profile ID into a new login object
    /// </summary>
    /// <param name="response">The auth token response</param>
    /// <param name="pid">The profile ID to link to</param>
    /// <returns>The created login object</returns>
    public static Login From(TokenResponse response, Guid pid)
    {
        return new Login
        {
            ProfileId = pid,
            Username = response.User.Nickname,
            PlatformId = response.User.Id,
            Avatar = response.User.Avatar,
            Provider = response.User.Provider,
            ProviderId = response.User.ProviderId,
            Email = response.User.Email,
        };
    }

    /// <summary>
    /// Turn a token response into a new profile object
    /// </summary>
    /// <param name="response">The auth token response</param>
    /// <returns>The created profile</returns>
    public async Task<Profile> From(TokenResponse response)
    {
        //Fetch the default roles for users 
        var roles = await _db.Roles.Default();
        return new Profile
        {
            RoleIds = roles.Select(t => t.Id).ToArray(),
            SettingsBlob = null,
            PrimaryUser = null,
            Nickname = response.User.Nickname,
            Avatar = response.User.Avatar,
        };
    }

    /// <summary>
    /// Compare the current login to the token response and update if necessary
    /// </summary>
    /// <param name="login">The current login</param>
    /// <param name="response">The token response from the auth service</param>
    /// <returns>A task representing the completion of the update</returns>
    public async Task Merge(Login login, TokenResponse response)
    {
        if (login.Username == response.User.Nickname &&
            login.Avatar == response.User.Avatar &&
            login.Provider == response.User.Provider &&
            login.ProviderId == response.User.ProviderId &&
            login.Email == response.User.Email)
            return;

        login.Username = response.User.Nickname;
        login.Avatar = response.User.Avatar;
        login.Provider = response.User.Provider;
        login.ProviderId = response.User.ProviderId;
        login.Email = response.User.Email;
        await _db.Logins.Update(login);
    }
}