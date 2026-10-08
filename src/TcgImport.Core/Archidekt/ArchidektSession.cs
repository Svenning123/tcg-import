using System.Text;
using System.Text.Json;

namespace TcgImport.Core.Archidekt;

/// <summary>A signed-in Archidekt user. Archidekt issues a short-lived JWT plus a refresh token.</summary>
public sealed record ArchidektSession(int UserId, string Username, string AccessToken, string RefreshToken)
{
    /// <summary>When the access token expires, read from its "exp" claim; null if it can't be read.</summary>
    public DateTimeOffset? AccessTokenExpiresAt => ReadExpiry(AccessToken);

    public bool AccessTokenExpiresWithin(TimeSpan margin) =>
        AccessTokenExpiresAt is { } expires && expires - DateTimeOffset.UtcNow < margin;

    private static DateTimeOffset? ReadExpiry(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2) return null;
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return json.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return null;
        }
    }
}

public sealed class ArchidektSignInException(string message) : Exception(message);

/// <summary>The saved Archidekt sign-in stopped working (refresh token expired or revoked).</summary>
public sealed class ArchidektSessionExpiredException()
    : Exception("Your Archidekt sign-in has expired. Sign in again on the Archidekt account tab.");
