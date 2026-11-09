using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<RaceDayDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Image storage: Azure Blob Storage when a connection string is configured,
// otherwise a local "uploads" folder (so the API still runs without an Azure account).
var blobConnection = builder.Configuration["AzureBlob:ConnectionString"];
var useAzure = !string.IsNullOrWhiteSpace(blobConnection);
if (useAzure) builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
else builder.Services.AddSingleton<IBlobStorageService, LocalFileStorageService>();

// Session-based authentication: the server issues an HttpOnly session cookie on login that carries
// the user's id and role. Every later request is authenticated/authorised from that cookie.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "RaceDay.Session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        o.SlidingExpiration = true;
        // APIs return status codes, not redirects to a login page.
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "API for managing race events, categories, enrolments, results and image uploads. " +
                      "Call POST /api/auth/login first; Swagger UI then sends the session cookie automatically."
    });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.RoutePrefix = "swagger");

if (!useAzure)
{
    var uploads = LocalFileStorageService.UploadsPath(app.Environment);
    Directory.CreateDirectory(uploads);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploads),
        RequestPath = "/uploads"
    });
}

if (!app.Environment.IsEnvironment("Testing"))
{
    // Retry so the API can start before SQL Server is ready (e.g. docker compose).
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
    for (var attempt = 1; ; attempt++)
    {
        try { db.Database.Migrate(); break; }
        catch when (attempt < 12)
        {
            app.Logger.LogWarning("Database not ready (attempt {Attempt}/12). Retrying in 5 seconds...", attempt);
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

public partial class Program { }   // exposed for WebApplicationFactory in the test project
