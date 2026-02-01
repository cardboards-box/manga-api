using Microsoft.AspNetCore.Mvc;

namespace MangaBox.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController(
	ILogger<WeatherForecastController> logger) : BaseController(logger)
{

}
