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

    MongoClient CreateClient(string connectionString, int timeoutSeconds = 30)
    {
        var mongoSettings = MongoClientSettings.FromConnectionString(connectionString);
        mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(timeoutSeconds);
        return new MongoClient(mongoSettings);
    }

    try
    {
        var client = CreateClient(connStr, timeoutSeconds: 25);
        var dbName = settings?.DatabaseName ?? "SolarDb";
        client.GetDatabase(dbName).RunCommand((Command<MongoDB.Bson.BsonDocument>)"{ping:1}");
        Console.WriteLine($"[INFO] Successfully connected to MongoDB at: {connStr}");
        return client;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WARNING] Primary MongoDB ping failed: {ex.Message}. Falling back to default client...");
        return CreateClient(connStr, timeoutSeconds: 30);
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

var smtpSection = builder.Configuration.GetSection(SmtpSettings.SectionName);
builder.Services.Configure<SmtpSettings>(smtpSection);

builder.Services.AddSingleton<IOtpService, OtpService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailService, GmailSmtpEmailService>();
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
        var nodeCol = db.GetCollection<MicrogridNode>("MicrogridNodes");

        if (nodeCol.CountDocuments(_ => true) == 0)
        {
            nodeCol.InsertMany(new[]
            {
                new MicrogridNode
                {
                    NodeCode = "NODE-COL-01",
                    Name = "Colombo North Solar Hub",
                    Region = "Western",
                    TotalCapacityKw = 500,
                    CurrentLoadKw = 120,
                    Latitude = 6.9271,
                    Longitude = 79.8612,
                    CapacityKWh = 1000,
                    BatterySlots = 12,
                    Schedule = "06:00 - 18:00 (Peak: 11:00 - 14:00)",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                },
                new MicrogridNode
                {
                    NodeCode = "NODE-COL-02",
                    Name = "Kaduwela Microgrid Substation",
                    Region = "Western",
                    TotalCapacityKw = 350,
                    CurrentLoadKw = 80,
                    Latitude = 6.9344,
                    Longitude = 79.9842,
                    CapacityKWh = 700,
                    BatterySlots = 8,
                    Schedule = "07:00 - 17:30 (Peak: 11:30 - 14:30)",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                },
                new MicrogridNode
                {
                    NodeCode = "NODE-KND-01",
                    Name = "Kandy Central Solar Station",
                    Region = "Central",
                    TotalCapacityKw = 400,
                    CurrentLoadKw = 150,
                    Latitude = 7.2906,
                    Longitude = 80.6337,
                    CapacityKWh = 800,
                    BatterySlots = 10,
                    Schedule = "06:30 - 18:00 (Peak: 11:00 - 13:30)",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                },
                new MicrogridNode
                {
                    NodeCode = "NODE-GAL-01",
                    Name = "Galle Coastal Solar Array",
                    Region = "Southern",
                    TotalCapacityKw = 600,
                    CurrentLoadKw = 210,
                    Latitude = 6.0535,
                    Longitude = 80.2210,
                    CapacityKWh = 1200,
                    BatterySlots = 16,
                    Schedule = "06:00 - 18:30 (Peak: 10:30 - 15:00)",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                }
            });
            Console.WriteLine("[INFO] Default Microgrid Nodes initialized.");
        }

        if (proCol.CountDocuments(_ => true) == 0)
        {
            proCol.InsertMany(new[]
            {
                new Prosumer
                {
                    UserId = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                    NIC = "200224700740",
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
                    NIC = "200012345678",
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

        // Ensure all prosumer records have valid NICs populated
        var missingNicList = proCol.Find(p => string.IsNullOrEmpty(p.NIC)).ToList();
        int pIndex = 1;
        foreach (var mp in missingNicList)
        {
            var assignedNic = pIndex == 1 ? "200224700740" : (pIndex == 2 ? "200012345678" : $"19950000000{pIndex}");
            proCol.UpdateOne(p => p.Id == mp.Id, Builders<Prosumer>.Update.Set(p => p.NIC, assignedNic));
            pIndex++;
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
