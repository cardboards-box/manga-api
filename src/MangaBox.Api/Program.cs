using Microsoft.AspNetCore.Diagnostics;
using Microsoft.OpenApi.Models;
using static System.Net.Mime.MediaTypeNames;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

await builder.Services.AddServices(builder.Configuration, 
    c =>
        c.AddCore()
         .AddDatabase()
         .AddOAuth());

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseExceptionHandler(err =>
{
    err.Run(async ctx =>
    {
        Exception? resolveException(WebApplication app)
        {
            if (!app.Environment.IsDevelopment()) return null;

            var feature = ctx.Features.Get<IExceptionHandlerFeature>();
            if (feature != null && feature.Error != null)
                return feature.Error;

            feature = ctx.Features.Get<IExceptionHandlerPathFeature>();
            return feature?.Error;
        };

        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        ctx.Response.ContentType = Application.Json;
        ctx.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        ctx.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
        var error = resolveException(app) ?? new Exception("An error has occurred, please contact an administrator for more information");

        await ctx.Response.WriteAsJsonAsync(Boxed.Exception(error));
    });
});

app.UseCors(c =>
{
    c.AllowAnyHeader()
     .AllowAnyMethod()
     .AllowAnyOrigin()
     .WithExposedHeaders("Content-Disposition");
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
