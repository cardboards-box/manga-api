using MangaBox.All;

var builder = WebApplication.CreateBuilder(args);

#if DEBUG
var appFile = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
builder.Configuration.AddJsonFile(appFile, false, true);
#endif

builder.Configuration.AddUserSecrets<Program>();


builder.Services.AddControllers()
	.AddJsonOptions(opts =>
	{
		opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
		opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
		opts.JsonSerializerOptions.AllowTrailingCommas = true;
		opts.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;
	});
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCustomSwaggerGen();
builder.Services
	.AddAuthMiddleware()
	.AddTelemetry();

await builder.Services.AddMangaBox(builder.Configuration);

builder.Services.AddHostedService<StatsBackgroundService>();
builder.Services.AddHostedService<LogLoaderBackgroundService>();

#if !DEBUG
builder.Services.AddHostedService<RISBackgroundService>();
builder.Services.AddHostedService<IndexBackgroundService>();
builder.Services.AddHostedService<RefreshBackgroundService>();
builder.Services.AddHostedService<NewChapterBackgroundService>();
builder.Services.AddHostedService<CatchupIndexingBackgroundService>();
builder.Services.AddHostedService<ChapterIndexBackgroundService>();
#endif

var app = builder.Build();

app.RegisterBoxing();

if (app.Environment.IsDevelopment() ||
	builder.Configuration[Constants.APPLICATION_NAME + ":EnableSwagger"]?.ToLower() == "true")
{
	app.MapOpenApi();
	app.UseSwagger();
	app.UseSwaggerUI(c =>
	{
		c.SwaggerEndpoint("/swagger/v1/swagger.json", "MangaBox V1");
	});
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

app.UseResponseCaching();

app.Run();
