using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using TicketSystem.Api.Data;
using TicketSystem.Api.Data.Repositories;
using TicketSystem.Api.Services;

var envLoadPaths = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(AppContext.BaseDirectory, ".env"),
};
var envFile = envLoadPaths.FirstOrDefault(File.Exists);
if (envFile == null)
    throw new FileNotFoundException(
        ".env file not found. Expected at one of:\n  " +
        string.Join("\n  ", envLoadPaths) +
        "\nCopy the .env file from the project root to your output directory.");
DotNetEnv.Env.Load(envFile);

var requiredVars = new[] { "DB_DRIVER", "DB_SERVER", "DB_DATABASE" };
var missing = requiredVars.Where(v => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(v))).ToList();
if (missing.Count > 0)
    throw new InvalidOperationException(
        $"Missing required environment variable(s): {string.Join(", ", missing)}. " +
        $"Ensure they are set in the .env file at the project root.");

var dbDriver = Environment.GetEnvironmentVariable("DB_DRIVER")?.ToLowerInvariant();
var dbServer = Environment.GetEnvironmentVariable("DB_SERVER")!;
var dbDatabase = Environment.GetEnvironmentVariable("DB_DATABASE")!;
var dbUser = Environment.GetEnvironmentVariable("DB_USERNAME") ?? "";
var dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";
var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "";
var useSqliteFallback = string.Equals(Environment.GetEnvironmentVariable("USE_SQLITE_FALLBACK"), "true", StringComparison.OrdinalIgnoreCase);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// JWT Auth
var jwtKey = builder.Configuration["Jwt:Key"] ?? "SuperSecretKeyForTicketSystem2024!@#$%";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "TicketSystem",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "TicketSystem",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IRequesterRepository, RequesterRepository>();

builder.Services.AddAuthorization();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IOcrService, OcrService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ITicketAssignmentService, TicketAssignmentService>();
builder.Services.AddScoped<IEmailProcessingService, EmailProcessingService>();
builder.Services.AddHostedService<AutoCloseService>();
builder.Services.AddHostedService<EmailReceiverService>();
builder.Services.AddScoped<ISlaService, SlaService>();
builder.Services.AddScoped<SlaCalculationEngine>();
builder.Services.AddHostedService<SlaBackgroundService>();

// EmailOutboxProcessor: singleton that serves as both the outbox queue and the background sender
builder.Services.AddSingleton<EmailOutboxProcessor>();
builder.Services.AddSingleton<IEmailOutboxService>(sp => sp.GetRequiredService<EmailOutboxProcessor>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<EmailOutboxProcessor>());

// Database
if (dbDriver == "sqlite" || useSqliteFallback)
{
    var sqlitePath = Path.Combine(AppContext.BaseDirectory, $"{dbDatabase}.db");
    Console.WriteLine($"[Config] Using SQLite: {sqlitePath}");
    builder.Services.AddDbContext<TicketSystemDbContext>(options =>
        options.UseSqlite($"Data Source={sqlitePath}")
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
}
else if (dbDriver == "sqlserver")
{
    var serverPart = string.IsNullOrWhiteSpace(dbPort)
        ? dbServer
        : $"{dbServer},{dbPort}";
    var connStr = string.IsNullOrWhiteSpace(dbUser)
        ? $"Server={serverPart};Database={dbDatabase};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
        : $"Server={serverPart};Database={dbDatabase};User Id={dbUser};Password={dbPass};TrustServerCertificate=True;MultipleActiveResultSets=True";
    Console.WriteLine($"[Config] Using SQL Server: {serverPart}/{dbDatabase}");
    builder.Services.AddDbContext<TicketSystemDbContext>(options =>
        options.UseSqlServer(connStr)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
}
else
{
    throw new InvalidOperationException($"Unsupported DB_DRIVER '{dbDriver}'. Expected 'sqlserver' or 'sqlite'.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TicketSystemDbContext>();
    await DbInitializer.InitializeAsync(context);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

// Serve frontend SPA from frontend/dist/
var frontendDist = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "frontend", "dist"));
if (Directory.Exists(frontendDist))
{
    Console.WriteLine($"[Config] Serving frontend from: {frontendDist}");
    var fileProvider = new PhysicalFileProvider(frontendDist);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });

    // SPA fallback: serve index.html for any non-API/non-swagger path
    app.Use(async (ctx, next) =>
    {
        await next();
        if (ctx.Response.StatusCode == 404 &&
            !ctx.Request.Path.StartsWithSegments("/api") &&
            !ctx.Request.Path.StartsWithSegments("/swagger"))
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            await ctx.Response.SendFileAsync(Path.Combine(frontendDist, "index.html"));
        }
    });
}

app.MapControllers();
app.MapHub<TicketSystem.Api.Hubs.TicketHub>("/hubs/ticket");

app.Run();
