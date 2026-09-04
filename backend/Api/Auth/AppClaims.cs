using System.Security.Claims;
using LolStatTrak.Domain.Entities;

namespace LolStatTrak.Api.Auth;

public static class AppClaims
{
    public const string GlobalAdmin = "global_admin";
    public const string AccessStatus = "access_status";
}

public static class AppPolicies
{
    /// <summary>User has been approved by a global admin (or is one). Required for all app features.</summary>
    public const string Approved = "Approved";

    /// <summary>Site-wide superuser.</summary>
    public const string GlobalAdmin = "GlobalAdmin";
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Missing user id claim"));

    public static bool IsGlobalAdmin(this ClaimsPrincipal user) =>
        user.HasClaim(AppClaims.GlobalAdmin, "true");

    public static bool IsApproved(this ClaimsPrincipal user) =>
        user.IsGlobalAdmin()
        || user.FindFirstValue(AppClaims.AccessStatus) == nameof(UserAccessStatus.Approved);
}
