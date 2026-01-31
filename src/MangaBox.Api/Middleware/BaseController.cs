using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;

namespace MangaBox.Api.Middleware;

[ApiController]
public class BaseController(
    ILogger _logger, 
    IDbService _db) : ControllerBase
{
    public RequestValidator Validator => new();

    public IDbService Database => _db;

    public ILogger Logger => _logger;

    [NonAction]
    public Guid? ProfileId()
    {
        if (User is null) return null;

        var id = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(id) || 
            !Guid.TryParse(id, out var guid)) return null;

        return guid;
    }

    [NonAction]
    public Task<IActionResult> Handle(Func<Guid?, Task<Boxed>> action)
    {
        return Handle(action, (object?)null);
    }

    [NonAction]
    public Task<IActionResult> Handle(Func<Task<Boxed>> action)
    {
        return Handle(action, (object?)null);
    }

    [NonAction]
    public Task<IActionResult> Handle<T>(Func<Task<Boxed>> action, T? body)
    {
        return Handle((p) => action(), body);
    }

    [NonAction]
    public IActionResult Do(Boxed boxed)
    {
        return StatusCode(boxed.Code, boxed);
    }

    [NonAction]
    public async Task<IActionResult> Handle<T>(Func<Guid?, Task<Boxed>> action, T? body)
    {
        var start = DateTime.Now;
        var pid = ProfileId();

        Boxed result;
        Exception? exception = null;
        try
        {
            result = await action(pid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling request");
            exception = ex;
            result = Boxed.Exception(ex);
        }
        var url = Request.HttpContext.Features.Get<IHttpRequestFeature>()?.RawTarget ?? Request.GetDisplayUrl();
        var log = new RequestLog
        {
            ProfileId = pid,
            StartTime = start,
            Url = url,
            Code = result.Code,
            Body = body is null ? null : JsonSerializer.Serialize(body),
            StackTrace = exception?.ToString(),
            EndTime = DateTime.Now,
        };
        result.RequestId = await _db.RequestLogs.Insert(log);
        return Do(result);
    }

    [NonAction]
    public Task<IActionResult> Authorized(Func<Guid, Task<Boxed>> action)
    {
        return Authorized(action, (object?)null);
    }

    [NonAction]
    public Task<IActionResult> Authorized<T>(Func<Guid, Task<Boxed>> action, T? body) => Handle(async (pid) =>
    {
        if (pid is null || !pid.HasValue || pid == Guid.Empty)
            return Boxed.Unauthorized();

        return await action(pid.Value);
    }, body);

    [NonAction]
    public Task<IActionResult> Roles<T>(Func<Guid, Task<Boxed>> action, T? body, params string[] roles) => Authorized(async (pid) =>
    {
        var hasRole = roles.Any(t => User.IsInRole(t));
        if (!hasRole) return Boxed.Unauthorized("You do not have the required role to access this resource");

        return await action(pid);
    }, body);

    [NonAction]
    public Task<IActionResult> Roles(Func<Guid, Task<Boxed>> action, params string[] roles) => Roles(action, (object?)null, roles);

    [NonAction]
    public Task<IActionResult> Admins(Func<Guid, Task<Boxed>> action) => Admins(action, (object?)null);

    [NonAction]
    public Task<IActionResult> Admins<T>(Func<Guid, Task<Boxed>> action, T? body) => Roles(action, body, Role.ADMIN);

    [NonAction]
    public Task<IActionResult> Mods(Func<Guid, Task<Boxed>> action) => Mods(action, (object?)null);

    [NonAction]
    public Task<IActionResult> Mods<T>(Func<Guid, Task<Boxed>> action, T? body) => Roles(action, body, Role.ADMIN, Role.MODERATOR, Role.AGENT);

    [NonAction]
    public Task<IActionResult> Bot(Func<Guid, Task<Boxed>> action) => Bot(action, (object?)null);

    [NonAction]
    public Task<IActionResult> Bot<T>(Func<Guid, Task<Boxed>> action, T? body) => Roles(action, body, Role.ADMIN, Role.AGENT);
}
