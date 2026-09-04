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

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Postgres");

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<RiotApiOptions>(builder.Configuration.GetSection(RiotApiOptions.SectionName));

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
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
        options.ClientId = builder.Configuration["Discord:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["Discord:ClientSecret"] ?? string.Empty;
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
        var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
        var signingKey = jwtSection["SigningKey"] ?? throw new InvalidOperationException("Missing Jwt:SigningKey");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
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
