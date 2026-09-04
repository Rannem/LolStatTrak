using System.Security.Claims;
using System.Text;
using LolStatTrak.Api.Auth;
using LolStatTrak.Api.Hubs;
using LolStatTrak.Domain.Services;
using LolStatTrak.Infrastructure.Data;
using LolStatTrak.Infrastructure.Migrations;
using LolStatTrak.Infrastructure.Options;
using LolStatTrak.Infrastructure.Repositories;
using LolStatTrak.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Flat, Railway-friendly env vars. DATABASE_URL is what Railway's Postgres plugin injects
// natively (postgres://user:pass@host:port/db) — no manual connection-string assembly needed.
var connectionString = ResolvePostgresConnectionString(builder.Configuration)
    ?? throw new InvalidOperationException("Missing DATABASE_URL (or ConnectionStrings:Postgres) configuration.");

builder.Services.Configure<JwtOptions>(options =>
{
    options.SigningKey = builder.Configuration["JWT_SIGNING_KEY"] ?? builder.Configuration["Jwt:SigningKey"] ?? string.Empty;
});
builder.Services.Configure<RiotApiOptions>(options =>
{
    options.ApiKey = builder.Configuration["RIOT_API_KEY"] ?? builder.Configuration["RiotApi:ApiKey"] ?? string.Empty;
    options.RegionalRouting = builder.Configuration["RIOT_REGION"] ?? builder.Configuration["RiotApi:RegionalRouting"] ?? "europe";
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// Comma-separated list, e.g. "https://lolstattrak.up.railway.app" — the one public frontend URL.
var allowedOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? builder.Configuration["Cors:AllowedOrigins:0"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// --- Data access + domain services -----------------------------------------------------
builder.Services.AddSingleton(new NpgsqlConnectionFactory(connectionString));
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<ClubRepository>();
builder.Services.AddScoped<LobbyRepository>();
builder.Services.AddScoped<MatchRepository>();
builder.Services.AddScoped<RandomizerService>();
builder.Services.AddScoped<LobbyMatchCorrelationService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddHttpClient<RiotApiClient>();

builder.Services.AddDatabaseMigrations(connectionString);

// --- Auth: Discord OAuth2 (login) -> app-issued JWT (API + SignalR) --------------------
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = "Discord";
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme) // temporary cookie during the OAuth handshake only
    .AddOAuth("Discord", options =>
    {
        options.ClientId = builder.Configuration["DISCORD_CLIENT_ID"] ?? builder.Configuration["Discord:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["DISCORD_CLIENT_SECRET"] ?? builder.Configuration["Discord:ClientSecret"] ?? string.Empty;
        options.CallbackPath = "/signin-discord";
        options.AuthorizationEndpoint = "https://discord.com/api/oauth2/authorize";
        options.TokenEndpoint = "https://discord.com/api/oauth2/token";
        options.UserInformationEndpoint = "https://discord.com/api/users/@me";
        options.Scope.Add("identify");
        options.SaveTokens = true;

        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        options.ClaimActions.MapJsonKey("urn:discord:id", "id");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
        options.ClaimActions.MapCustomJson("avatar_url", user =>
        {
            var id = user.GetProperty("id").GetString();
            var avatar = user.TryGetProperty("avatar", out var a) ? a.GetString() : null;
            return avatar is null ? null! : $"https://cdn.discordapp.com/avatars/{id}/{avatar}.png";
        });

        options.Events = new OAuthEvents
        {
            OnCreatingTicket = async ctx =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, ctx.Options.UserInformationEndpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ctx.AccessToken);
                using var response = await ctx.Backchannel.SendAsync(request, ctx.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();
                using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                ctx.RunClaimActions(json.RootElement);
            },
        };
    })
    .AddJwtBearer(options =>
    {
        var signingKey = builder.Configuration["JWT_SIGNING_KEY"] ?? builder.Configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Missing JWT_SIGNING_KEY configuration.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = "LolStatTrak",
            ValidAudience = "LolStatTrak",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
        };

        // Allow the SignalR client / REST calls to authenticate via the same httpOnly session cookie.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Cookies["lst_session"];
                if (!string.IsNullOrEmpty(accessToken) && string.IsNullOrEmpty(context.Token))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// FluentMigrator migrations run automatically on startup so Railway deploys stay hands-off.
app.Services.RunDatabaseMigrations();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<LobbyHub>("/hubs/lobby");

app.Run();

/// <summary>
/// Accepts either Railway's native DATABASE_URL ("postgres://user:pass@host:port/db") or a
/// classic ConnectionStrings:Postgres value, so setup only ever needs one variable reference.
/// </summary>
static string? ResolvePostgresConnectionString(IConfiguration configuration)
{
    var explicitConnectionString = configuration.GetConnectionString("Postgres");
    if (!string.IsNullOrEmpty(explicitConnectionString))
        return explicitConnectionString;

    var databaseUrl = configuration["DATABASE_URL"];
    if (string.IsNullOrEmpty(databaseUrl))
        return null;

    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);
    var builder = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
        SslMode = Npgsql.SslMode.Prefer,
    };
    return builder.ConnectionString;
}
