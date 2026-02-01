using MangaBox.Database;
using MangaBox.Jwt;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services
	.AddAuthMiddleware()
	.AddTelemetry()
	.AddJwt()
	.AddCoreServices();

await builder.Services.AddServices(builder.Configuration, c => c.AddDatabase());

var app = builder.Build();

app.RegisterBoxing();

if (app.Environment.IsDevelopment() ||
	builder.Configuration[Constants.APPLICATION_NAME + ":EnableSwagger"]?.ToLower() == "true")
{
	app.MapOpenApi();
}

app.UseCors(c => c
   .AllowAnyHeader()
   .AllowAnyMethod()
   .AllowAnyOrigin()
   .WithExposedHeaders("Content-Disposition"));

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapPrometheusScrapingEndpoint();
app.MapControllers();

app.Run();
