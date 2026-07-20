using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace DocumentClassifier.Infrastructure;

/// <summary>
/// Extension methods for authorization helpers.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Gets the user ID from the current claims principal.
    /// Returns a hashed identifier for privacy.
    /// </summary>
    public static string GetUserIdentifier(this ClaimsPrincipal user)
    {
        var objectId = user?.FindFirst("oid")?.Value;
        var email = user?.FindFirst(ClaimTypes.Email)?.Value;
        var upn = user?.FindFirst("upn")?.Value;

        var identifier = objectId ?? email ?? upn ?? "anonymous";
        
        // Hash the identifier for privacy in logs
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(identifier));
        return Convert.ToHexString(hash)[..12];
    }
}

/// <summary>
/// Policy names for authorization.
/// </summary>
public static class AuthorizationPolicies
{
    public const string DocumentProcessing = "DocumentProcessing";
    public const string AdminOnly = "AdminOnly";
}
