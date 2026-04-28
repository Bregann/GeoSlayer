using Hangfire;
using Hangfire.Dashboard.BasicAuthorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using GeoSlayer.Domain.Interfaces.Helpers;
using GeoSlayer.Domain.Helpers;
using GeoSlayer;
using GeoSlayer.Domain.Interfaces.Api;
using GeoSlayer.Domain.Services;
using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Enums;

#if DEBUG
using Hangfire.MemoryStorage;
using Testcontainers.PostgreSql;
#else
using Hangfire.PostgreSql;
#endif

Log.Logger = new LoggerConfiguration()
    .WriteTo.Async(x => x.File("/app/Logs/log.log", retainedFileCountLimit: 7, rollingInterval: RollingInterval.Day))
    .WriteTo.Console()
    .Enrich.WithProperty("Application", "Gs-Api" + (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" ? "-Test" : ""))
    .WriteTo.Seq("http://192.168.1.20:5341")
    .CreateLogger();

Log.Information("Logger Setup");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();

// Add in our own services
builder.Services.AddSingleton<IEnvironmentalSettingHelper, EnvironmentalSettingHelper>();

// Add in the auth
builder.Services.AddAuthorization();

// add in controller data services
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Environment.GetEnvironmentVariable("JwtValidIssuer"),
            ValidAudience = Environment.GetEnvironmentVariable("JwtValidAudience"),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JwtKey")!))
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClients", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "https://geoslayer.bregan.me"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

#if DEBUG
GlobalConfiguration.Configuration.UseMemoryStorage();

var postgresContainer = new PostgreSqlBuilder("postgis/postgis:16-3.4")
    .WithDatabase("geoslayercontainer")
    .WithUsername("testuser")
    .WithPassword("testpass")
    // If you need the extension enabled on a specific DB automatically, 
    // the PostGIS image usually handles this, or you use an Init script.
    .WithPortBinding(5432, true)
    .Build();

await postgresContainer.StartAsync();

var connectionString = postgresContainer.GetConnectionString();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseLazyLoadingProxies()
        .UseNpgsql(connectionString, o => o.UseNetTopologySuite()));

builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseMemoryStorage()
        );

#else
var connectionString = Environment.GetEnvironmentVariable("GeoSlayerLive");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseLazyLoadingProxies()
        .UseNpgsql(connectionString, o => o.UseNetTopologySuite()));
GlobalConfiguration.Configuration.UsePostgreSqlStorage(c => c.UseNpgsqlConnection(Environment.GetEnvironmentVariable("GeoSlayerLive")));

builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(Environment.GetEnvironmentVariable("GeoSlayerLive")))
        );
#endif

// hangfire
builder.Services.AddHangfireServer(options => options.SchedulePollingInterval = TimeSpan.FromSeconds(10));

var app = builder.Build();

app.UseCors("WebClients");

#if DEBUG
// Seed the database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetService<AppDbContext>();
    var settingsHelper = scope.ServiceProvider.GetRequiredService<IEnvironmentalSettingHelper>();

    if (dbContext == null)
    {
        throw new Exception("DbContext is null");
    }

    // protection to only run when the connection string is to the test container
    var dbConnectionString = dbContext.Database.GetConnectionString();
    if (!string.IsNullOrEmpty(dbConnectionString) &&
        (dbConnectionString.Contains("127.0.0.1") ||
        dbConnectionString.Contains("financemanagercontainer")))
    {
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        await DatabaseSeedHelper.SeedDatabase(dbContext, settingsHelper, scope.ServiceProvider);
    }
}
#endif

var environmentalSettingHelper = app.Services.GetService<IEnvironmentalSettingHelper>()!;
await environmentalSettingHelper.LoadEnvironmentalSettings();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

var auth = new[] { new BasicAuthAuthorizationFilter(new BasicAuthAuthorizationFilterOptions
{
    RequireSsl = false,
    SslRedirect = false,
    LoginCaseSensitive = true,
    Users = new []
    {
        new BasicAuthAuthorizationUser
        {
            Login = environmentalSettingHelper.TryGetEnviromentalSettingValue(EnvironmentalSettingEnum.HangfireUsername),
            PasswordClear = environmentalSettingHelper.TryGetEnviromentalSettingValue(EnvironmentalSettingEnum.HangfirePassword)
        }
    }
})};

app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = auth
}, JobStorage.Current);

#if !DEBUG
HangfireJobSetup.RegisterJobs();
#endif

app.Run();
