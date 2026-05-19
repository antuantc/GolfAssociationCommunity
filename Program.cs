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

var authBuilder = builder.Services.AddAuthentication();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}

var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
var microsoftClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
if (!string.IsNullOrWhiteSpace(microsoftClientId) && !string.IsNullOrWhiteSpace(microsoftClientSecret))
{
    authBuilder.AddMicrosoftAccount(options =>
    {
        options.ClientId = microsoftClientId;
        options.ClientSecret = microsoftClientSecret;
    });
}

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
var enableSwagger = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableSwagger");
if (enableSwagger)
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

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true &&
        context.User.IsInRole("AssociationAdmin") &&
        !context.User.IsInRole("Admin"))
    {
        var path = context.Request.Path;
        var isAssociationPortal = path.StartsWithSegments("/AssociationAdmin", StringComparison.OrdinalIgnoreCase);
        var isIdentityPath = path.StartsWithSegments("/Identity", StringComparison.OrdinalIgnoreCase);

        if (!isAssociationPortal && !isIdentityPath)
        {
            context.Response.Redirect("/AssociationAdmin");
            return;
        }
    }

    await next();
});

app.Use(async (context, next) =>
{
    await next();

    if (context.User.Identity?.IsAuthenticated == true &&
        context.User.IsInRole("AssociationAdmin") &&
        !context.User.IsInRole("Admin") &&
        HttpMethods.IsPost(context.Request.Method) &&
        context.Request.Path.StartsWithSegments("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase) &&
        (context.Response.StatusCode == StatusCodes.Status302Found || context.Response.StatusCode == StatusCodes.Status303SeeOther))
    {
        context.Response.Headers.Location = "/AssociationAdmin";
    }
});

app.MapRazorPages();
app.MapControllers();

// Run migrations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await dbContext.Database.MigrateAsync();
    Log.Information("Database migrations completed successfully");

    var requiredRoles = new[] { "Admin", "AssociationAdmin" };
    foreach (var role in requiredRoles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var roleCreateResult = await roleManager.CreateAsync(new IdentityRole(role));
            if (!roleCreateResult.Succeeded)
            {
                Log.Error("Failed to create role {Role}: {Errors}", role, string.Join("; ", roleCreateResult.Errors.Select(e => e.Description)));
            }
            else
            {
                Log.Information("Role {Role} created", role);
            }
        }
    }

    var adminEmail = app.Configuration["AdminSeed:Email"];
    var adminPassword = app.Configuration["AdminSeed:Password"];

    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        const string adminRole = "Admin";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = app.Configuration["AdminSeed:FirstName"],
                LastName = app.Configuration["AdminSeed:LastName"],
                UpdatedAt = DateTime.UtcNow
            };

            var createUserResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (!createUserResult.Succeeded)
            {
                Log.Error("Failed to create seeded admin user {Email}: {Errors}", adminEmail, string.Join("; ", createUserResult.Errors.Select(e => e.Description)));
            }
            else
            {
                Log.Information("Seeded admin user created for {Email}", adminEmail);
            }
        }

        if (adminUser is not null && !await userManager.IsInRoleAsync(adminUser, adminRole))
        {
            var addRoleResult = await userManager.AddToRoleAsync(adminUser, adminRole);
            if (!addRoleResult.Succeeded)
            {
                Log.Error("Failed to assign Admin role to {Email}: {Errors}", adminEmail, string.Join("; ", addRoleResult.Errors.Select(e => e.Description)));
            }
            else
            {
                Log.Information("Admin role assigned to {Email}", adminEmail);
            }
        }
    }
    else
    {
        Log.Information("AdminSeed configuration not set. Skipping default admin seeding.");
    }
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