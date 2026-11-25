using System.Globalization;
using System.Web;

namespace WeightTracker.ITest.Infrastructure;

/// <summary>Stands in for FatSecret's servers, below the HTTP client: the real signer, client and provider run.</summary>
public class FakeFatSecretHandler : HttpMessageHandler
{
    public Dictionary<DateOnly, int> Calories { get; } = [];
    public Dictionary<DateOnly, decimal> Weights { get; } = [];
    public string? Error { get; set; }
    public string? Callback { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var query = HttpUtility.ParseQueryString(request.Content is null ? request.RequestUri!.Query : await request.Content.ReadAsStringAsync(ct));
        var path = request.RequestUri!.AbsolutePath;
        if (string.IsNullOrEmpty(query["oauth_signature"])) return Answer(HttpStatusCode.Unauthorized, "Missing signature");

        if (path.EndsWith("/request_token"))
        {
            Callback = query["oauth_callback"];
            return Answer(HttpStatusCode.OK, "oauth_token=request-token&oauth_token_secret=request-secret");
        }
        if (path.EndsWith("/access_token"))
            return query["oauth_verifier"] == "1234"
                ? Answer(HttpStatusCode.OK, "oauth_token=plain-access-token&oauth_token_secret=plain-access-secret")
                : Answer(HttpStatusCode.Unauthorized, "Invalid verifier");

        if (Error is not null) return Answer(HttpStatusCode.OK, Error);
        var epoch = new DateOnly(1970, 1, 1);
        var month = epoch.AddDays(int.Parse(query["date"]!));
        bool InMonth(DateOnly d) => d.Year == month.Year && d.Month == month.Month;
        var days = path.Contains("weight")
            ? Weights.Where(d => InMonth(d.Key)).Select(d => $"{{\"date_int\":\"{d.Key.DayNumber - epoch.DayNumber}\",\"weight_kg\":\"{d.Value.ToString("0.0000", CultureInfo.InvariantCulture)}\"}}")
            : Calories.Where(d => InMonth(d.Key)).Select(d => $"{{\"date_int\":\"{d.Key.DayNumber - epoch.DayNumber}\",\"calories\":\"{d.Value}\"}}");
        return Answer(HttpStatusCode.OK, $"{{\"month\":{{\"day\":[{string.Join(',', days)}]}}}}");
    }

    private static HttpResponseMessage Answer(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body) };
}
