using System.Net;
using System.Net.Sockets;
using GolfAssociationCommunity.Data;
using GolfAssociationCommunity.Models;
using GolfAssociationCommunity.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var urlsFromEnv = builder.Configuration["ASPNETCORE_URLS"];
if (string.IsNullOrWhiteSpace(urlsFromEnv))
{
    var (httpPort, httpsPort) = GetAvailablePortPair(5000, 5010, 5001, 5011);
    builder.WebHost.UseUrls($"http://localhost:{httpPort}", $"https://localhost:{httpsPort}");
    builder.Services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options =>
    {
        options.HttpsPort = httpsPort;
    });
}
else
{
    var httpsPortSetting = builder.Configuration["ASPNETCORE_HTTPS_PORT"];
    if (int.TryParse(httpsPortSetting, out var configuredHttpsPort))
    {
        builder.Services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options =>
        {
            options.HttpsPort = configuredHttpsPort;
        });
    }
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
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

var sqliteConnectionBuilder = new SqliteConnectionStringBuilder(defaultConnection);
if (!string.IsNullOrWhiteSpace(sqliteConnectionBuilder.DataSource) &&
    !Path.IsPathRooted(sqliteConnectionBuilder.DataSource))
{
    sqliteConnectionBuilder.DataSource = Path.Combine(builder.Environment.ContentRootPath, sqliteConnectionBuilder.DataSource);
}

var resolvedConnectionString = sqliteConnectionBuilder.ToString();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(resolvedConnectionString));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.SignIn.RequireConfirmedEmail = true;
        options.SignIn.RequireConfirmedAccount = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

// Add custom services
builder.Services.AddScoped<IAuthorizeNetPaymentService, AuthorizeNetPaymentService>();
builder.Services.AddScoped<IAuthorizeNetTransactionService, AuthorizeNetTransactionService>();
builder.Services.AddScoped<IAssociationService, AssociationService>();
builder.Services.AddScoped<ITournamentService, TournamentService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IScoreService, ScoreService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.AddScoped<IAdminAuditService, AdminAuditService>();
builder.Services.AddScoped<ICsvImportService, CsvImportService>();

// Add Controllers
builder.Services.AddControllers();

// Add Razor Pages
builder.Services.AddRazorPages();

// Allow large file uploads (hero background videos up to 100 MB)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100 MB
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100 MB
});

// Persistent uploads — stored OUTSIDE wwwroot so deployments never wipe them.
// Override "UploadsPath" in appsettings.Production.json to an absolute path on the server.
var configuredUploadsPath = builder.Configuration["UploadsPath"];
var uploadPhysicalRoot = string.IsNullOrWhiteSpace(configuredUploadsPath)
    ? Path.Combine(builder.Environment.ContentRootPath, "persistent-uploads")
    : configuredUploadsPath;
Directory.CreateDirectory(uploadPhysicalRoot);
builder.Services.Configure<UploadSettings>(opts =>
{
    opts.PhysicalRoot = uploadPhysicalRoot;
    opts.RequestPath = "/uploads";
});

Log.Information("Uploads folder: {UploadsPath}", uploadPhysicalRoot);

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

app.Use(async (context, next) =>
{
    if (context.Request.Host.Host == "127.0.0.1" || context.Request.Host.Host == "::1")
    {
        var host = context.Request.Host.Port.HasValue
            ? new HostString("localhost", context.Request.Host.Port.Value)
            : new HostString("localhost");
        var redirectUrl = $"{context.Request.Scheme}://{host}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
        context.Response.Redirect(redirectUrl, permanent: false);
        return;
    }

    await next();
});

app.UseHttpsRedirection();

app.UseStaticFiles();

// Serve persistent uploads from outside wwwroot
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadPhysicalRoot),
    RequestPath = "/uploads"
});

app.UseRouting();

app.UseCors("AllowAll");

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (IsBlockedIdentityPath(context.Request.Path))
    {
        var redirectPath = context.User.Identity?.IsAuthenticated == true
            ? "/Identity/Account/Manage"
            : "/Identity/Account/Login";

        context.Response.Redirect(redirectPath);
        return;
    }

    await next();
});

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

    // Self-heal corrupt SQLite schema states where migration history exists but core tables are missing.
    if (!await TableExistsAsync(dbContext, "GolfAssociations") || !await TableExistsAsync(dbContext, "AspNetUsers"))
    {
        Log.Warning("Detected missing core tables after migration. Recreating SQLite schema.");
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
        Log.Information("Database schema recreated successfully");
    }

    Log.Information("Database migrations completed successfully");

    var (disabledCount, removedTokenCount) = await DisableTwoFactorAndClearTokensAsync(userManager, dbContext);
    if (disabledCount > 0 || removedTokenCount > 0)
    {
        Log.Information(
            "Two-factor cleanup completed. Disabled users: {DisabledCount}, removed token records: {RemovedTokenCount}",
            disabledCount,
            removedTokenCount);
    }

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

        // Force-reset the admin password when AdminSeed:ForceResetPassword = true.
        // Usage: set this flag + AdminSeed:Password in Azure App Settings, restart the app,
        // then remove the flag (or set it to false) to prevent repeated resets on every startup.
        var forceReset = app.Configuration.GetValue<bool>("AdminSeed:ForceResetPassword");
        if (adminUser is not null && forceReset)
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(adminUser);
            var resetResult = await userManager.ResetPasswordAsync(adminUser, resetToken, adminPassword);
            if (!resetResult.Succeeded)
            {
                Log.Error("Failed to force-reset admin password for {Email}: {Errors}", adminEmail, string.Join("; ", resetResult.Errors.Select(e => e.Description)));
            }
            else
            {
                // Also ensure EmailConfirmed so the account is never locked out.
                adminUser.EmailConfirmed = true;
                await userManager.UpdateAsync(adminUser);
                Log.Warning("Admin password force-reset for {Email}. Remove AdminSeed:ForceResetPassword from config.", adminEmail);
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

static bool IsBlockedIdentityPath(PathString path)
{
    return path.StartsWithSegments("/Identity/Account/Manage/TwoFactorAuthentication", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/Identity/Account/Register", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/Identity/Account/RegisterConfirmation", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/Identity/Account/Manage/EnableAuthenticator", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/Identity/Account/Manage/ResetAuthenticator", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/Identity/Account/Manage/GenerateRecoveryCodes", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/Identity/Account/Manage/ShowRecoveryCodes", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/Identity/Account/Manage/PersonalData", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/Identity/Account/Manage/DownloadPersonalData", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/Identity/Account/Manage/DeletePersonalData", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/Identity/Account/LoginWith2fa", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/Identity/Account/LoginWithRecoveryCode", StringComparison.OrdinalIgnoreCase);
}

static async Task<(int DisabledCount, int RemovedTokenCount)> DisableTwoFactorAndClearTokensAsync(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext)
{
    var twoFactorUsers = await userManager.Users
        .Where(u => u.TwoFactorEnabled)
        .ToListAsync();

    foreach (var user in twoFactorUsers)
    {
        await userManager.SetTwoFactorEnabledAsync(user, false);
    }

    var tokenSet = dbContext.Set<IdentityUserToken<string>>();
    var tokensToRemove = await tokenSet
        .Where(t => t.Name == "AuthenticatorKey" || t.Name == "RecoveryCodes")
        .ToListAsync();

    if (tokensToRemove.Count > 0)
    {
        tokenSet.RemoveRange(tokensToRemove);
        await dbContext.SaveChangesAsync();
    }

    return (twoFactorUsers.Count, tokensToRemove.Count);
}

static async Task<bool> TableExistsAsync(ApplicationDbContext dbContext, string tableName)
{
    var connection = dbContext.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
        await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";

    var parameter = command.CreateParameter();
    parameter.ParameterName = "$name";
    parameter.Value = tableName;
    command.Parameters.Add(parameter);

    var result = await command.ExecuteScalarAsync();
    return Convert.ToInt32(result) > 0;
}