using System.Net;
using System.Net.Sockets;
using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var urlsFromEnv = builder.Configuration["ASPNETCORE_URLS"];
if (string.IsNullOrWhiteSpace(urlsFromEnv))
{
    var (httpPort, httpsPort) = GetAvailablePortPair(5000, 5010, 5001, 5011);
    builder.WebHost.UseUrls($"http://127.0.0.1:{httpPort}", $"https://127.0.0.1:{httpsPort}");
    builder.Services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options =>
    {
        options.HttpsPort = httpsPort;
    });
}

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/golf-association-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

// Add custom services
builder.Services.AddScoped<IAuthorizeNetPaymentService, AuthorizeNetPaymentService>();
builder.Services.AddScoped<IAssociationService, AssociationService>();
builder.Services.AddScoped<ITournamentService, TournamentService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IScoreService, ScoreService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();

// Add Controllers
builder.Services.AddControllers();

// Add Razor Pages
builder.Services.AddRazorPages();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Add Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseCors("AllowAll");

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// Run migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
    Log.Information("Database migrations completed successfully");
}

Log.Information("Golf Association Community application starting...");

try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

static (int HttpPort, int HttpsPort) GetAvailablePortPair(int httpFirstPort, int httpLastPort, int httpsFirstPort, int httpsLastPort)
{
    for (var httpPort = httpFirstPort; httpPort <= httpLastPort; httpPort++)
    {
        if (!IsPortAvailable(httpPort))
        {
            continue;
        }

        for (var httpsPort = httpsFirstPort; httpsPort <= httpsLastPort; httpsPort++)
        {
            if (!IsPortAvailable(httpsPort))
            {
                continue;
            }

            return (httpPort, httpsPort);
        }
    }

    throw new InvalidOperationException($"No available HTTP/HTTPS port pair found in ranges {httpFirstPort}-{httpLastPort} and {httpsFirstPort}-{httpsLastPort}.");
}

static bool IsPortAvailable(int port)
{
    try
    {
        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        return true;
    }
    catch (SocketException)
    {
        return false;
    }
}