// Program.cs
// The starting point of the API. Read it top to bottom:
//   1. Build the app and add the services we need.
//   2. Create the database tables (migrations) and add the first
//      users and items if the database is empty (SeedData).
//   3. Turn on CORS so the Angular app (http://localhost:4200)
//      is allowed to call this API.
//   4. Map the controllers, so /api/items, /api/requests,
//      /api/auth actually work.
//   5. Map the SignalR hub at /borrowingHub, so browsers can
//      stay connected and hear about changes live.
//   6. Run the app.
//
// The API listens on http://localhost:5035 (see
// Properties/launchSettings.json, profile "http").
// No HTTPS redirect here on purpose: the Angular demo runs on
// plain http://localhost:4200, and a redirect would break it.

using BorrowingApi.Data;
using BorrowingApi.Hubs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Allow the Angular frontend to call this API.
// Without this, the browser blocks the calls (CORS error).
// AllowCredentials is needed for SignalR: its client sends
// cookies with its requests by default (credentials: include),
// so the browser only accepts the answer when the API says
// credentials are allowed. The origin stays pinned to the
// Angular app, so this is not open to everyone.
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add controllers (Auth, Items, BorrowRequests, WeatherForecast).
builder.Services.AddControllers();

// Add SignalR, so browsers can stay connected to the hub and
// hear about changes the moment they happen (see Hubs/BorrowingHub.cs).
builder.Services.AddSignalR();

// Add the database: SQLite file borrowing.db, through AppDbContext.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=borrowing.db"));

// Add the OpenAPI page (Swagger) for testing in the browser.
builder.Services.AddOpenApi();

var app = builder.Build();

// Create the database tables if they do not exist yet,
// then add the demo users and items if the tables are empty.
// This runs every time the app starts, but SeedData does
// nothing when there is already data, so it is safe.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    SeedData.Seed(db);
}

// Show the OpenAPI page in Development at /openapi.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Use the CORS rule above. This must come before MapControllers.
app.UseCors("Frontend");

app.MapControllers();

// The SignalR hub: browsers connect here and stay connected.
// Must come after UseCors, so the connection is allowed.
app.MapHub<BorrowingHub>("/borrowingHub");

app.Run();
