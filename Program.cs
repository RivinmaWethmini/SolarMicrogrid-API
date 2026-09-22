// ============================================================================
// File: Program.cs
// Project: SolarAPI - Smart Solar Microgrid Trading System
// Module: SE4040 - Enterprise Application Development
// Author: Member 4 (Energy Reservation & QR Dispatch)
// ============================================================================

using MongoDB.Driver;
using SolarAPI.Configurations;
using SolarAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── MongoDB Configuration ────────────────────────────────────────────────────
builder.Services.Configure<MongoDBSettings>(
    builder.Configuration.GetSection("MongoDBSettings"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = builder.Configuration.GetSection("MongoDBSettings").Get<MongoDBSettings>();
    var connStr = settings?.ConnectionString ?? "mongodb://127.0.0.1:27017";

    MongoClient CreateClient(string connectionString, int timeoutSeconds = 6)
    {
        var mongoSettings = MongoClientSettings.FromConnectionString(connectionString);
        mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(timeoutSeconds);

        if (mongoSettings.UseTls)
        {
            mongoSettings.SslSettings = new SslSettings
            {
                CheckCertificateRevocation = false,
                ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
            };
        }
        return new MongoClient(mongoSettings);
    }

    try
    {
        var client = CreateClient(connStr, timeoutSeconds: 6);
        var dbName = settings?.DatabaseName ?? "SolarDb";
        client.GetDatabase(dbName).RunCommand((Command<MongoDB.Bson.BsonDocument>)"{ping:1}");
        Console.WriteLine($"[INFO] Successfully connected to MongoDB at: {connStr}");
        return client;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WARNING] Primary MongoDB connection failed: {ex.Message}. Falling back to local MongoDB on 127.0.0.1:27017...");
        return CreateClient("mongodb://127.0.0.1:27017", timeoutSeconds: 3);
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

app.Run();
