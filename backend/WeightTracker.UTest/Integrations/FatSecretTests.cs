using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using WeightTracker.Api.Integrations;
using WeightTracker.Api.Integrations.FatSecret;

namespace WeightTracker.UTest.Integrations;

public class FatSecretTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, string, (HttpStatusCode, string)> answer) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var sent = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request, sent));
            var (status, body) = answer(request, sent);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static readonly FatSecretOptions Options = new() { ConsumerKey = "ck", ConsumerSecret = "cs" };
    private static readonly ProviderCredentials Credentials = new("at", "ats");

    private static (FatSecretProvider Provider, FakeHandler Handler) Create(Func<HttpRequestMessage, string, (HttpStatusCode, string)> answer)
    {
        var handler = new FakeHandler(answer);
        return (new FatSecretProvider(new FatSecretClient(new HttpClient(handler), Options, TimeProvider.System), Options), handler);
    }

    private static (FatSecretProvider Provider, FakeHandler Handler) Create(string body, HttpStatusCode status = HttpStatusCode.OK) => Create((_, _) => (status, body));

    private static int Int(string date) => DateOnly.Parse(date).DayNumber - new DateOnly(1970, 1, 1).DayNumber;

    // ── Parsing ───────────────────────────────────────────────────────────────

    [Fact]
    public void ParseMonth_ReadsStringsAndDaysSince1970()
    {
        var body = $$$$"""{"month":{"day":[{"date_int":"{{{{Int("2026-11-01")}}}}","calories":"1850"},{"date_int":{{{{Int("2026-11-03")}}}},"calories":"0"}]}}""";
        var days = FatSecretClient.ParseMonth(body, "calories");
        Assert.Equal(new Dictionary<DateOnly, decimal> { [new(2026, 11, 1)] = 1850, [new(2026, 11, 3)] = 0 }, days);
    }

    [Fact]
    public void ParseMonth_ReadsASingleDayThatIsNotAnArray()
    {
        var days = FatSecretClient.ParseMonth($$$$"""{"month":{"day":{"date_int":"{{{{Int("2026-11-01")}}}}","weight_kg":"81.2340"}}}""", "weight_kg");
        Assert.Equal(81.234m, Assert.Single(days).Value);
    }

    [Theory]
    [InlineData("""{"month":{"from_date_int":"1","to_date_int":"2"}}""")]
    [InlineData("""{"month":{"day":[]}}""")]
    [InlineData("""{"month":null}""")]
    [InlineData("""{"month":{"day":{"date_int":"20000"}}}""")]
    public void ParseMonth_WithoutValues_IsEmpty(string body) => Assert.Empty(FatSecretClient.ParseMonth(body, "calories"));

    [Theory]
    [InlineData("<html>down</html>", 503)]
    [InlineData("[]", 200)]
    [InlineData("{}", 200)]
    [InlineData("""{"month":{}}""", 500)]
    public void ParseMonth_AnythingElse_IsAnErrorAndNeverAnEmptyMonth(string body, int status) =>
        Assert.Throws<IntegrationException>(() => FatSecretClient.ParseMonth(body, "calories", status));

    [Theory]
    [InlineData(8, "FATSECRET_CONSUMER_KEY")]
    [InlineData(6, "clock")]
    [InlineData(21, "public IP")]
    [InlineData(99, "error 99: nope")]
    public void ParseMonth_ExplainsErrors(int code, string expected)
    {
        var e = Assert.ThrowsAny<IntegrationException>(() => FatSecretClient.ParseMonth($$$$"""{"error":{"code":{{{{code}}}},"message":"nope"}}""", "calories"));
        Assert.Contains(expected, e.Message);
        Assert.IsNotType<NeedsReconnectException>(e);
    }

    [Fact]
    public void ParseMonth_Error9_NeedsReconnect() =>
        Assert.Throws<NeedsReconnectException>(() => FatSecretClient.ParseMonth("""{"error":{"code":"9","message":"Invalid token"}}""", "calories", 401));

    // ── Fetching ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fetch_AsksEveryMonthThePeriodTouches_AndKeepsOnlyItsDays()
    {
        var (provider, handler) = Create((r, _) =>
        {
            var month = new DateOnly(1970, 1, 1).AddDays(int.Parse(QueryHelpers.ParseQuery(r.RequestUri!.Query)["date"]!));
            var first = month.DayNumber - new DateOnly(1970, 1, 1).DayNumber;
            return (HttpStatusCode.OK, r.RequestUri.AbsolutePath.Contains("weight")
                ? $$$$"""{"month":{"day":{"date_int":"{{{{first}}}}","weight_kg":"80.126"}}}"""
                : $$$$"""{"month":{"day":[{"date_int":"{{{{first}}}}","calories":"1000"},{"date_int":"{{{{first + 14}}}}","calories":"1500.5"}]}}""");
        });

        var days = await provider.FetchAsync(Credentials, new DateOnly(2026, 9, 10), new DateOnly(2026, 11, 1), default);

        Assert.Equal(6, handler.Requests.Count);      // three months, two calls each
        Assert.Equal(
            [
                new ImportedDay(new(2026, 9, 15), 1501, null),
                new ImportedDay(new(2026, 10, 1), 1000, 80.13m),
                new ImportedDay(new(2026, 10, 15), 1501, null),
                new ImportedDay(new(2026, 11, 1), 1000, 80.13m),
            ], days);
    }

    [Fact]
    public async Task Fetch_LeavesOutValuesTheAppWouldNotAccept()
    {
        var (provider, _) = Create((r, _) => (HttpStatusCode.OK, r.RequestUri!.AbsolutePath.Contains("weight")
            ? $$$$"""{"month":{"day":{"date_int":"{{{{Int("2026-11-01")}}}}","weight_kg":"0.0000"}}}"""
            : $$$$"""{"month":{"day":{"date_int":"{{{{Int("2026-11-01")}}}}","calories":"99999"}}}"""));
        Assert.Empty(await provider.FetchAsync(Credentials, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 2), default));
    }

    [Fact]
    public async Task Fetch_FailsAsAWhole_WhenOneMonthFails()
    {
        var calls = 0;
        var (provider, _) = Create((_, _) => ++calls < 4 ? (HttpStatusCode.OK, """{"month":{}}""") : (HttpStatusCode.OK, """{"error":{"code":1,"message":"x"}}"""));
        await Assert.ThrowsAsync<IntegrationException>(() => provider.FetchAsync(Credentials, new DateOnly(2026, 9, 1), new DateOnly(2026, 11, 1), default));
    }

    [Fact]
    public async Task Fetch_WhenFatSecretCannotBeReached_SaysSoWithoutDetails()
    {
        var (provider, _) = Create((_, _) => throw new HttpRequestException("connect https://secret-url"));
        var e = await Assert.ThrowsAsync<IntegrationException>(() => provider.FetchAsync(Credentials, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), default));
        Assert.Equal("FatSecret can't be reached.", e.Message);
    }

    // ── Connecting ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Begin_PostsASignedRequestForAToken()
    {
        var (provider, handler) = Create("oauth_token=r%2Ft&oauth_token_secret=rts&oauth_callback_confirmed=true");

        var request = await provider.BeginAuthorizationAsync("http://localhost:8080/cb", default);

        Assert.Equal(new AuthorizationRequest("https://authentication.fatsecret.com/oauth/authorize?oauth_token=r%2Ft", "r/t", "rts"), request);
        var (sent, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        var form = QueryHelpers.ParseQuery(body).ToDictionary(p => p.Key, p => p.Value.ToString());
        Assert.Equal("http://localhost:8080/cb", form["oauth_callback"]);
        var signature = form["oauth_signature"];
        form.Remove("oauth_signature");
        Assert.Equal(OAuth1Signer.Sign("POST", "https://authentication.fatsecret.com/oauth/request_token", form, "cs"), signature);
    }

    [Fact]
    public async Task Begin_WithoutACallback_AsksForAPin()
    {
        var (provider, handler) = Create("oauth_token=rt&oauth_token_secret=rts");
        await provider.BeginAuthorizationAsync(null, default);
        Assert.Contains("oauth_callback=oob", handler.Requests[0].Body);
    }

    [Fact]
    public async Task Complete_SignsWithTheRequestToken_AndReturnsTheAccessToken()
    {
        var (provider, handler) = Create("oauth_token=at&oauth_token_secret=ats");

        Assert.Equal(Credentials, await provider.CompleteAuthorizationAsync("rt", "rts", "1234", default));

        var query = QueryHelpers.ParseQuery(handler.Requests[0].Request.RequestUri!.Query).ToDictionary(p => p.Key, p => p.Value.ToString());
        var signature = query["oauth_signature"];
        query.Remove("oauth_signature");
        Assert.Equal(("rt", "1234"), (query["oauth_token"], query["oauth_verifier"]));
        Assert.Equal(OAuth1Signer.Sign("GET", "https://authentication.fatsecret.com/oauth/access_token", query, "cs", "rts"), signature);
    }

    [Theory]
    [InlineData("Invalid signature", HttpStatusCode.Unauthorized, "Invalid signature")]
    [InlineData("<html>oops</html>", HttpStatusCode.BadGateway, "HTTP 502")]
    [InlineData("oauth_problem=x", HttpStatusCode.OK, "without a token")]
    public async Task Begin_Refused_IsExplained(string body, HttpStatusCode status, string expected)
    {
        var (provider, _) = Create(body, status);
        Assert.Contains(expected, (await Assert.ThrowsAsync<IntegrationException>(() => provider.BeginAuthorizationAsync("x", default))).Message);
    }

    [Theory]
    [InlineData("k", "s", true)]
    [InlineData("\"k\" ", "'s'", true)]
    [InlineData("k", "", false)]
    [InlineData(null, "s", false)]
    public void Options_NeedBothKeys(string? key, string? secret, bool configured)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["FATSECRET_CONSUMER_KEY"] = key, ["FATSECRET_CONSUMER_SECRET"] = secret }).Build();
        var options = FatSecretOptions.From(config);
        Assert.Equal(configured, options.IsConfigured);
        if (configured) Assert.Equal(("k", "s"), (options.ConsumerKey, options.ConsumerSecret));
    }
}
