using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TcgImport.Core.Archidekt;

/// <summary>
/// Reads decks from Archidekt's (unofficial) JSON API. Without a session it sees public and unlisted decks;
/// signed in, it also sees the user's private decks and bookmarks.
/// </summary>
public sealed partial class ArchidektClient(HttpClient http)
{
    private const string ApiUrl = "https://archidekt.com/api/";
    public const int PageSize = 30;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public ArchidektSession? Session { get; private set; }

    /// <summary>Raised when the session is created, refreshed (new tokens to save) or lost (null).</summary>
    public event Action<ArchidektSession?>? SessionChanged;

    public async Task<ArchidektSession> SignInAsync(string usernameOrEmail, string password, CancellationToken ct = default)
    {
        var login = usernameOrEmail.Trim();
        object body = login.Contains('@')
            ? new { email = login, password }
            : new { username = login, password };

        using var response = await http.PostAsJsonAsync(ApiUrl + "rest-auth/login/", body, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArchidektSignInException("Archidekt didn't accept that username or email and password.");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, ct)
            ?? throw new FormatException("Empty sign-in response from Archidekt.");
        SetSession(new ArchidektSession(result.User.Id, result.User.Username, result.Token, result.RefreshToken));
        return Session!;
    }

    /// <summary>Resumes a session saved from an earlier run.</summary>
    public void RestoreSession(ArchidektSession session) => Session = session;

    public void SignOut() => SetSession(null);

    public async Task<DeckSearchPage> SearchAsync(DeckSearchFilter filter, int page, CancellationToken ct = default)
    {
        using var response = await GetAsync($"{ApiUrl}decks/v3/?{filter.ToQueryString(page, PageSize)}", ct);
        // Archidekt answers 404 for a page past the end of the results.
        if (response.StatusCode == HttpStatusCode.NotFound && page > 1)
            return new DeckSearchPage([], 0, HasMore: false);
        response.EnsureSuccessStatusCode();
        return ArchidektJson.ParseSearch(await response.Content.ReadAsStringAsync(ct));
    }

    public async Task<ArchidektDeck> GetDeckAsync(int deckId, CancellationToken ct = default)
    {
        using var response = await GetAsync($"{ApiUrl}decks/{deckId}/", ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            throw new DeckUnavailableException(deckId, signedIn: Session is not null);
        response.EnsureSuccessStatusCode();
        return ArchidektJson.ParseDeck(await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>Card names for autocomplete, from the search archidekt.com uses in its own filters.</summary>
    /// <param name="legendaryFirst">List legendary cards first, for picking a commander.</param>
    public async Task<IReadOnlyList<string>> SearchCardNamesAsync(string text, bool legendaryFirst = false, CancellationToken ct = default)
    {
        var url = $"{ApiUrl}cards/v2/?nameSearch={Uri.EscapeDataString(text.Trim())}&unique&pageSize=20";
        using var response = await GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return ArchidektJson.ParseCardNames(await response.Content.ReadAsStringAsync(ct), text, legendaryFirst);
    }

    /// <summary>Accepts a bare deck id or any archidekt.com/decks/{id}/... link.</summary>
    public static bool TryParseDeckId(string? input, out int deckId)
    {
        deckId = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var text = input.Trim();
        if (int.TryParse(text, out deckId)) return deckId > 0;

        var match = DeckUrlRegex().Match(text);
        return match.Success && int.TryParse(match.Groups[1].Value, out deckId);
    }

    private async Task<HttpResponseMessage> GetAsync(string url, CancellationToken ct)
    {
        if (Session is { } session && session.AccessTokenExpiresWithin(RefreshMargin))
            await RefreshAsync(session, ct);

        var response = await http.SendAsync(Request(url), ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized || Session is not { } current)
            return response;

        // The token was rejected before its expiry time; refresh once and retry.
        response.Dispose();
        await RefreshAsync(current, ct);
        return await http.SendAsync(Request(url), ct);
    }

    private HttpRequestMessage Request(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (Session is { } session)
            request.Headers.Authorization = new AuthenticationHeaderValue("JWT", session.AccessToken);
        return request;
    }

    private async Task RefreshAsync(ArchidektSession session, CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            // Another request may have refreshed while this one waited.
            if (!ReferenceEquals(Session, session)) return;

            using var response = await http.PostAsJsonAsync(
                ApiUrl + "rest-auth/token/refresh/", new { refresh = session.RefreshToken }, JsonOptions, ct);
            if (!response.IsSuccessStatusCode)
            {
                SetSession(null);
                throw new ArchidektSessionExpiredException();
            }

            var result = await response.Content.ReadFromJsonAsync<RefreshResponse>(JsonOptions, ct);
            var token = result?.Access ?? result?.Token;
            if (string.IsNullOrEmpty(token))
            {
                SetSession(null);
                throw new ArchidektSessionExpiredException();
            }
            SetSession(session with { AccessToken = token, RefreshToken = result!.Refresh ?? session.RefreshToken });
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private void SetSession(ArchidektSession? session)
    {
        Session = session;
        SessionChanged?.Invoke(session);
    }

    [GeneratedRegex(@"archidekt\.com/(?:api/)?decks/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex DeckUrlRegex();

    private sealed record LoginResponse(string Token, [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string RefreshToken, LoginUser User);
    private sealed record LoginUser(int Id, string Username);
    private sealed record RefreshResponse(string? Access, string? Token, string? Refresh);
}

public sealed class DeckUnavailableException(int deckId, bool signedIn)
    : Exception(signedIn
        ? $"Archidekt deck {deckId} doesn't exist, or it's private and belongs to someone else."
        : $"Archidekt deck {deckId} doesn't exist or is private. Sign in on the Archidekt account tab to use your private decks.")
{
    public int DeckId { get; } = deckId;
}
