using System.Net;
using System.Text;
using System.Text.Json;
using TcgImport.Core.Archidekt;

namespace TcgImport.Core.Tests;

public class ArchidektClientTests
{
    private const string EmptySearch = """{ "count": 0, "next": null, "results": [] }""";

    [Fact]
    public async Task SignIn_sends_username_and_stores_session()
    {
        var server = new FakeArchidekt();
        server.Respond("POST /api/rest-auth/login/", HttpStatusCode.OK, LoginJson(Jwt(DateTimeOffset.UtcNow.AddHours(1))));
        var client = new ArchidektClient(server.Client);
        ArchidektSession? raised = null;
        client.SessionChanged += s => raised = s;

        var session = await client.SignInAsync("ISummonPotOfGreed", "secret");

        Assert.Equal("ISummonPotOfGreed", session.Username);
        Assert.Equal(42, session.UserId);
        Assert.Equal("refresh-1", session.RefreshToken);
        Assert.Same(session, raised);
        Assert.Contains("\"username\":\"ISummonPotOfGreed\"", server.Bodies[0]);
    }

    [Fact]
    public async Task SignIn_with_email_sends_email_field()
    {
        var server = new FakeArchidekt();
        server.Respond("POST /api/rest-auth/login/", HttpStatusCode.OK, LoginJson(Jwt(DateTimeOffset.UtcNow.AddHours(1))));

        await new ArchidektClient(server.Client).SignInAsync("me@example.com", "secret");

        Assert.Contains("\"email\":\"me@example.com\"", server.Bodies[0]);
    }

    [Fact]
    public async Task SignIn_with_wrong_password_throws_friendly_error()
    {
        var server = new FakeArchidekt();
        server.Respond("POST /api/rest-auth/login/", HttpStatusCode.BadRequest,
            """{"non_field_errors":["Unable to log in with provided credentials."]}""");
        var client = new ArchidektClient(server.Client);

        await Assert.ThrowsAsync<ArchidektSignInException>(() => client.SignInAsync("me", "wrong"));
        Assert.Null(client.Session);
    }

    [Fact]
    public async Task Requests_carry_the_access_token()
    {
        var server = new FakeArchidekt();
        server.Respond("GET /api/decks/v3/", HttpStatusCode.OK, EmptySearch);
        var client = new ArchidektClient(server.Client);
        var token = Jwt(DateTimeOffset.UtcNow.AddHours(1));
        client.RestoreSession(new ArchidektSession(42, "me", token, "refresh-1"));

        await client.SearchAsync(new DeckSearchFilter { Bookmarks = true }, 1);

        Assert.Equal($"JWT {token}", server.AuthHeaders[0]);
    }

    [Fact]
    public async Task Expired_access_token_is_refreshed_before_the_request()
    {
        var server = new FakeArchidekt();
        var fresh = Jwt(DateTimeOffset.UtcNow.AddHours(1));
        server.Respond("POST /api/rest-auth/token/refresh/", HttpStatusCode.OK, $$"""{ "access": "{{fresh}}" }""");
        server.Respond("GET /api/decks/v3/", HttpStatusCode.OK, EmptySearch);
        var client = new ArchidektClient(server.Client);
        client.RestoreSession(new ArchidektSession(42, "me", Jwt(DateTimeOffset.UtcNow.AddMinutes(-5)), "refresh-1"));
        ArchidektSession? saved = null;
        client.SessionChanged += s => saved = s;

        await client.SearchAsync(new DeckSearchFilter { OwnerUsername = "me" }, 1);

        Assert.Equal(fresh, saved?.AccessToken);
        Assert.Equal("refresh-1", saved?.RefreshToken);
        Assert.Equal($"JWT {fresh}", server.AuthHeaders.Last());
        Assert.Contains("\"refresh\":\"refresh-1\"", server.Bodies[0]);
    }

    [Fact]
    public async Task Rejected_token_is_refreshed_and_the_request_retried()
    {
        var server = new FakeArchidekt();
        var fresh = Jwt(DateTimeOffset.UtcNow.AddHours(1));
        server.Respond("GET /api/decks/v3/", HttpStatusCode.Unauthorized, "{}");
        server.Respond("GET /api/decks/v3/", HttpStatusCode.OK, EmptySearch);
        server.Respond("POST /api/rest-auth/token/refresh/", HttpStatusCode.OK, $$"""{ "access": "{{fresh}}" }""");
        var client = new ArchidektClient(server.Client);
        client.RestoreSession(new ArchidektSession(42, "me", Jwt(DateTimeOffset.UtcNow.AddHours(1)), "refresh-1"));

        var page = await client.SearchAsync(new DeckSearchFilter { OwnerUsername = "me" }, 1);

        Assert.Empty(page.Decks);
        Assert.Equal($"JWT {fresh}", server.AuthHeaders.Last());
    }

    [Fact]
    public async Task Failed_refresh_signs_out()
    {
        var server = new FakeArchidekt();
        server.Respond("POST /api/rest-auth/token/refresh/", HttpStatusCode.Unauthorized, "{}");
        var client = new ArchidektClient(server.Client);
        client.RestoreSession(new ArchidektSession(42, "me", Jwt(DateTimeOffset.UtcNow.AddMinutes(-5)), "refresh-1"));

        await Assert.ThrowsAsync<ArchidektSessionExpiredException>(
            () => client.SearchAsync(new DeckSearchFilter { OwnerUsername = "me" }, 1));
        Assert.Null(client.Session);
    }

    [Fact]
    public void Session_reads_expiry_from_the_token()
    {
        var expires = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds());
        var session = new ArchidektSession(1, "me", Jwt(expires), "r");

        Assert.Equal(expires, session.AccessTokenExpiresAt);
        Assert.False(session.AccessTokenExpiresWithin(TimeSpan.FromMinutes(1)));
        Assert.Null(new ArchidektSession(1, "me", "not-a-jwt", "r").AccessTokenExpiresAt);
    }

    private static string LoginJson(string token) =>
        JsonSerializer.Serialize(new { token, refresh_token = "refresh-1", user = new { id = 42, username = "ISummonPotOfGreed" } });

    private static string Jwt(DateTimeOffset expires)
    {
        static string Encode(string json) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{Encode("""{"alg":"HS256"}""")}.{Encode($$"""{"exp":{{expires.ToUnixTimeSeconds()}}}""")}.signature";
    }

    /// <summary>Answers requests by "METHOD /path/" with queued responses, recording what was sent.</summary>
    private sealed class FakeArchidekt : HttpMessageHandler
    {
        private readonly Dictionary<string, Queue<(HttpStatusCode, string)>> _responses = [];

        public FakeArchidekt() => Client = new HttpClient(this);

        public HttpClient Client { get; }
        public List<string> Bodies { get; } = [];
        public List<string?> AuthHeaders { get; } = [];

        public void Respond(string route, HttpStatusCode status, string body)
        {
            if (!_responses.TryGetValue(route, out var queue)) _responses[route] = queue = new();
            queue.Enqueue((status, body));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Content is not null) Bodies.Add(await request.Content.ReadAsStringAsync(ct));
            AuthHeaders.Add(request.Headers.Authorization?.ToString());

            var route = $"{request.Method} {request.RequestUri!.AbsolutePath}";
            if (!_responses.TryGetValue(route, out var queue) || queue.Count == 0)
                throw new InvalidOperationException($"Unexpected request: {route}");
            var (status, body) = queue.Count > 1 ? queue.Dequeue() : queue.Peek();
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
