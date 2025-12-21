using FluentValidation.AspNetCore;
using FluentValidation;
using Hangfire;
using Hangfire.SqlServer;
using IGB.Application;
using IGB.Infrastructure;
using IGB.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using System.Text;
using IGB.Web.Zoom;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Localization (views + data annotations)
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Add services to the container.
builder.Services
    .AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// FluentValidation (auto MVC validation)
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Application & Infrastructure
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Distributed Cache (Redis if configured, otherwise in-memory)
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "IGB:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Authentication (Cookie-based, with roles)
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];
        var key = builder.Configuration["Jwt:Key"];

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = key != null ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)) : null
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin", "SuperAdmin"));
    options.AddPolicy("StaffOnly", policy => policy.RequireRole("Tutor", "Admin", "SuperAdmin"));
});

// Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// SignalR (real-time)
builder.Services.AddSignalR();
builder.Services.AddScoped<IGB.Web.Services.INotificationService, IGB.Web.Services.NotificationService>();
builder.Services.AddSingleton<IGB.Web.Notifications.INotificationStore, IGB.Web.Notifications.DistributedCacheNotificationStore>();
builder.Services.AddScoped<IGB.Application.Services.IJwtTokenService, IGB.Web.Services.JwtTokenService>();

// Zoom integration
builder.Services.Configure<ZoomOptions>(builder.Configuration.GetSection("Zoom"));
builder.Services.AddSingleton<IZoomTokenService, ZoomTokenService>();
builder.Services.AddSingleton<IZoomClient, ZoomClient>();
builder.Services.AddHttpClient("ZoomOAuth", c =>
{
    c.BaseAddress = new Uri("https://zoom.us/");
});
builder.Services.AddHttpClient("ZoomApi", c =>
{
    c.BaseAddress = new Uri("https://api.zoom.us/v2/");
});

// Hangfire (background jobs) - SQL Server storage
builder.Services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180);
    config.UseSimpleAssemblyNameTypeSerializer();
    config.UseRecommendedSerializerSettings();
    config.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"), new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.FromSeconds(15),
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    });
});
builder.Services.AddHangfireServer();

// HttpClient + Polly (resilience)
builder.Services.AddHttpClient("ExternalApi")
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retry => TimeSpan.FromMilliseconds(200 * Math.Pow(2, retry))))
    .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(10)));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseResponseCompression();

// Request localization
var supportedCultures = new[] { "en-US", "ar-SA" }
    .Select(c => new System.Globalization.CultureInfo(c))
    .ToList();
var localizationOptions = new Microsoft.AspNetCore.Builder.RequestLocalizationOptions()
    .SetDefaultCulture("en-US")
    .AddSupportedCultures(supportedCultures.Select(c => c.Name).ToArray())
    .AddSupportedUICultures(supportedCultures.Select(c => c.Name).ToArray());
localizationOptions.RequestCultureProviders.Insert(0, new Microsoft.AspNetCore.Localization.CookieRequestCultureProvider());
app.UseRequestLocalization(localizationOptions);

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Hangfire Dashboard (SuperAdmin only)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new IGB.Web.Infrastructure.HangfireDashboardAuthorizationFilter() }
});

// SignalR hubs
app.MapHub<IGB.Web.Hubs.NotificationHub>("/hubs/notifications");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Recurring jobs (example - heartbeat notification)
RecurringJob.AddOrUpdate<IGB.Web.Jobs.SystemHeartbeatJob>("system-heartbeat", job => job.Run(), Cron.Minutely);

try
{
    Log.Information("Starting IGB Web Application");

    // Initialize database and seed data
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var logger = services.GetRequiredService<ILogger<DbInitializer>>();
            var initializer = new DbInitializer(context, logger);
            await initializer.InitializeAsync();
            Log.Information("Database initialized and seeded successfully");
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while seeding the database");
        }
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}
