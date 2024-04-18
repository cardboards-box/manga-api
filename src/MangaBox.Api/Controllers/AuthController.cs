namespace MangaBox.Api.Controllers;

using Auth;

public class AuthController(
    IDbService _db, 
    ILogger<AuthController> _logger, 
    IAuthService _auth) : BaseController(_logger, _db)
{
    [HttpGet, Route("auth/{code}")]
    [ProducesBox<string>, ProducesError(400), ProducesError(401)]
    public Task<IActionResult> Auth([FromRoute] string code) => Handle((pid) =>
    {
        return _auth.Login(code, pid);
    });

    [HttpGet, Route("auth/@me/logins")]
    [ProducesArray<Login>, ProducesError(401)]
    public Task<IActionResult> MyLogins() => Authorized(async (pid) =>
    {
        var logins = await Database.Logins.ByProfile(pid);
        return Boxed.Ok(logins);
    });

    [HttpGet, Route("auth/{id}/logins")]
    [ProducesArray<Login>, ProducesError(401)]
    public Task<IActionResult> Logins([FromRoute] Guid id) => Admins(async (_) =>
    {
        var logins = await Database.Logins.ByProfile(id);
        return Boxed.Ok(logins);
    });
}
