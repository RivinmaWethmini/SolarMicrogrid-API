// ============================================================================
// File: Program.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// ============================================================================

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using SolarAPI.Configurations;
using SolarAPI.Models;
using SolarAPI.Security;
using SolarAPI.Services;
using SolarAPI.Services.Auth;

var builder = WebApplication.CreateBuilder(args);

// ─── MongoDB Configuration ────────────────────────────────────────────────────
builder.Services.Configure<MongoDBSettings>(
    builder.Configuration.GetSection("MongoDBSettings"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = builder.Configuration.GetSection("MongoDBSettings").Get<MongoDBSettings>();
    var connStr = settings?.ConnectionString ?? "mongodb://127.0.0.1:27017";

    MongoClient CreateClient(string connectionString, int timeoutSeconds = 3)
    {
        var mongoSettings = MongoClientSettings.FromConnectionString(connectionString);
        mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(timeoutSeconds);
        if (connectionString.Contains("ssl=true", StringComparison.OrdinalIgnoreCase) || 
            connectionString.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
        {
            mongoSettings.SslSettings = new SslSettings
            {
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12,
                CheckCertificateRevocation = false,
                ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };
        }
        return new MongoClient(mongoSettings);
    }

    try
    {
        var client = CreateClient(connStr, timeoutSeconds: 3);
        var dbName = settings?.DatabaseName ?? "SolarDb";
        client.GetDatabase(dbName).RunCommand((Command<MongoDB.Bson.BsonDocument>)"{ping:1}");
        Console.WriteLine($"[INFO] Successfully connected to MongoDB at: {connStr}");
        return client;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WARNING] Primary MongoDB connection failed: {ex.Message}. Falling back to local MongoDB on 127.0.0.1:27017...");
        var localClient = CreateClient("mongodb://127.0.0.1:27017", timeoutSeconds: 3);
        var dbName = settings?.DatabaseName ?? "SolarDb";
        localClient.GetDatabase(dbName).RunCommand((Command<MongoDB.Bson.BsonDocument>)"{ping:1}");
        Console.WriteLine($"[INFO] Successfully connected to local MongoDB on 127.0.0.1:27017");
        return localClient;
    }
});

builder.Services.AddScoped<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var settings = builder.Configuration.GetSection("MongoDBSettings").Get<MongoDBSettings>();
    return client.GetDatabase(settings?.DatabaseName ?? "SolarDb");
});

// ─── Application Services ─────────────────────────────────────────────────────
builder.Services.AddScoped<IProsumerService, ProsumerService>();
builder.Services.AddScoped<IReservationService, ReservationService>();

// ─── Enterprise Auth & Security Services ───────────────────────────────────────
var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
builder.Services.Configure<JwtSettings>(jwtSection);
var jwtSettings = jwtSection.Get<JwtSettings>() ?? new JwtSettings();

builder.Services.AddSingleton<IOtpService, OtpService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddTransient<IEmailService, MockEmailService>();
builder.Services.AddScoped<IAuthAuditService, AuthAuditService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthDatabaseInitializer, AuthDatabaseInitializer>();

// ─── JWT Authentication ───────────────────────────────────────────────────────
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// ─── Dynamic RBAC & Permission Policies ───────────────────────────────────────
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

// ─── CORS Policy ──────────────────────────────────────────────────────────────
// FIX: CORS was missing — needed for React web client (SolarWeb) to call this API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Solar Microgrid API",
        Version = "v1",
        Description = "Smart Solar Microgrid Trading & Reservation System with Email + OTP Authentication & RBAC"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and your token.\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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

var app = builder.Build();

// ─── Middleware Pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ─── Data Seeding (Initialize default records if empty) ─────────────────────────
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();

        // Initialize Auth MongoDB Collections and Security Indexes
        var authDbInit = scope.ServiceProvider.GetRequiredService<IAuthDatabaseInitializer>();
        authDbInit.InitializeAsync().GetAwaiter().GetResult();

        var proCol = db.GetCollection<Prosumer>("Prosumers");
        var resCol = db.GetCollection<Reservation>("Reservations");

        if (proCol.CountDocuments(_ => true) == 0)
        {
            proCol.InsertMany(new[]
            {
                new Prosumer
                {
                    UserId = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                    Name = "SunPower Station A (Colombo North)",
                    SolarCapacityKw = 25.0,
                    BatteryCapacityKwh = 50.0,
                    AvailableEnergyKw = 18.5,
                    PricePerKwh = 45.00m,
                    Location = "Colombo North Node #4",
                    MicrogridNodeId = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                    IsAvailable = true
                },
                new Prosumer
                {
                    UserId = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                    Name = "GreenWatt Microgrid (Kandy Hub)",
                    SolarCapacityKw = 40.0,
                    BatteryCapacityKwh = 80.0,
                    AvailableEnergyKw = 32.0,
                    PricePerKwh = 42.50m,
                    Location = "Kandy Central Substation",
                    MicrogridNodeId = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                    IsAvailable = true
                }
            });
            Console.WriteLine("[INFO] Default Prosumers initialized.");
        }

        if (resCol.CountDocuments(_ => true) == 0)
        {
            var resService = scope.ServiceProvider.GetRequiredService<IReservationService>();
            var now = DateTime.UtcNow;

            resService.CreateAsync(new Reservation
            {
                ProsumerId = "NIC-2001",
                NodeId = "NODE-COLOMBO-01",
                ReservedEnergyKwh = 15.5,
                ReservationDate = now.AddDays(2).Date,
                StartTime = "09:00",
                EndTime = "12:00",
                Status = "Approved"
            }).GetAwaiter().GetResult();

            resService.CreateAsync(new Reservation
            {
                ProsumerId = "NIC-2002",
                NodeId = "NODE-KANDY-02",
                ReservedEnergyKwh = 8.0,
                ReservationDate = now.AddDays(3).Date,
                StartTime = "14:00",
                EndTime = "16:00",
                Status = "Pending"
            }).GetAwaiter().GetResult();

            resService.CreateAsync(new Reservation
            {
                ProsumerId = "199812345678",
                NodeId = "NODE-GALLE-01",
                ReservedEnergyKwh = 22.0,
                ReservationDate = now.AddDays(4).Date,
                StartTime = "10:00",
                EndTime = "15:00",
                Status = "Pending"
            }).GetAwaiter().GetResult();

            Console.WriteLine("[INFO] Default Reservations initialized.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WARN] Could not initialize seed data: {ex.Message}");
    }
}

app.Run();
