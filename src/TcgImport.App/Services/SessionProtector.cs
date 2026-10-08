using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TcgImport.Core.Archidekt;

namespace TcgImport.App.Services;

/// <summary>Encrypts the Archidekt session with Windows DPAPI, so only this Windows user can read it.</summary>
public static class SessionProtector
{
    private static readonly byte[] Entropy = "TcgImport.ArchidektSession"u8.ToArray();

    public static string Protect(ArchidektSession session)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(session);
        return Convert.ToBase64String(ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser));
    }

    public static ArchidektSession? Unprotect(string? protectedSession)
    {
        if (string.IsNullOrEmpty(protectedSession)) return null;
        try
        {
            var json = ProtectedData.Unprotect(Convert.FromBase64String(protectedSession), Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<ArchidektSession>(Encoding.UTF8.GetString(json));
        }
        catch (Exception e) when (e is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }
}
