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
    return new MongoClient(settings?.ConnectionString ?? "mongodb://localhost:27017");
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
