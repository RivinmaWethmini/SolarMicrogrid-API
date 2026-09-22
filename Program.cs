// ============================================================================
// File: Program.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// ============================================================================

using MongoDB.Driver;
using SolarAPI.Configurations;
using SolarAPI.Models;
using SolarAPI.Services;

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
builder.Services.AddSwaggerGen();

// TODO (Member 2): When JWT authentication is ready, add the following:
// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//     .AddJwtBearer(options => { /* JWT config from Member 2 */ });

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

// TODO (Member 2): Uncomment when JWT is integrated:
// app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ─── Data Seeding (Initialize default records if empty) ─────────────────────────
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
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
