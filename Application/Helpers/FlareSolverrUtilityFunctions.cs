using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Application.Helpers;

// Fetches pages behind Cloudflare using a local FlareSolverr instance (see docker-compose.yml).
//
// FlareSolverr runs a real headless browser, which is heavy, so it's only used to obtain clearance:
// the cookies it returns (cf_clearance, bound to its User-Agent + our IP) are cached per host and
// following requests go out as plain HttpClient requests carrying those cookies and that User-Agent.
// When a plain request gets blocked (403/503/challenge page) or the cookies expire mid-run, we go back
// to FlareSolverr once (concurrent failures share that one solve) and cache the fresh cookies.
//
// Endpoint defaults to http://localhost:8191/v1; override with the FLARESOLVERR_URL env var.
public static class FlareSolverrUtilityFunctions
{
    private static readonly string Endpoint =
        Environment.GetEnvironmentVariable("FLARESOLVERR_URL") ?? "http://localhost:8191/v1";

    // FlareSolverr can take a while to solve a challenge, so the HTTP timeout must outlast maxTimeout.
    private static readonly HttpClient FlareSolverrClient = new() { Timeout = TimeSpan.FromMinutes(3) };

    // A plain HttpClient per host, holding FlareSolverr's cookies in a CookieContainer and wearing its exact
    // User-Agent (cf_clearance is rejected if the User-Agent differs).
    private record ClearanceSession(HttpClient Client);

    // A response fetched with plain HTTP. Headers are flattened (name -> comma-joined values, case-insensitive).
    public record PlainResponse(HttpStatusCode StatusCode, string Body, IReadOnlyDictionary<string, string> Headers);

    // host -> cached clearance, and host -> lock so concurrent failures trigger only one FlareSolverr solve.
    private static readonly ConcurrentDictionary<string, ClearanceSession> Sessions = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SolveLocks = new();

    // plainOnly = false: when a solve is needed, the page FlareSolverr rendered is returned as-is (saves a request).
    // plainOnly = true: always return the raw server HTML, re-fetching with plain HTTP after a solve. Use it for
    // sites whose scripts rewrite the DOM (e.g. hetushu injects hidden decoy paragraphs), since FlareSolverr
    // returns the page after its scripts ran.
    public static async Task<string> GetHtmlFromUrl(string url, int maxTimeoutMs = 60000, int maxRetries = 3,
        bool plainOnly = false)
    {
        var response = await Fetch(url, url, plainOnly, null, maxTimeoutMs, maxRetries);
        return response.Body;
    }

    // Always plain HTTP (status, body and headers), e.g. for XHR endpoints whose data is in a header.
    // headers are extra request headers (e.g. Referer / X-Requested-With for endpoints that require them).
    // If blocked, clearance is refreshed by solving solveUrl (default: url) -- pass a normal page here when url
    // is an API endpoint that a browser can't usefully render.
    public static Task<PlainResponse> GetPlainResponse(string url, string solveUrl = null,
        IReadOnlyDictionary<string, string> headers = null, int maxTimeoutMs = 60000, int maxRetries = 3) =>
        Fetch(url, solveUrl ?? url, plainOnly: true, headers, maxTimeoutMs, maxRetries);

    private static async Task<PlainResponse> Fetch(string url, string solveUrl, bool plainOnly,
        IReadOnlyDictionary<string, string> headers, int maxTimeoutMs, int maxRetries)
    {
        var host = new Uri(url).Host;
        Exception lastError = null;

        for (var retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                if (Sessions.TryGetValue(host, out var session))
                {
                    var response = await TryPlainRequest(url, session, headers);
                    if (response != null)
                        return response;
                }

                // Blocked or no clearance yet. Returns null if another request refreshed it while we waited.
                var solvedHtml = await SolveWithFlareSolverr(host, session, solveUrl, maxTimeoutMs);
                if (solvedHtml != null && plainOnly == false && solveUrl == url)
                    return new PlainResponse(HttpStatusCode.OK, solvedHtml, new Dictionary<string, string>());

                // Fresh clearance in place -> fetch the real response with plain HTTP.
                var fresh = await TryPlainRequest(url, Sessions[host], headers);
                if (fresh != null)
                    return fresh;

                throw new Exception($"Still blocked right after a fresh FlareSolverr solve ({url})");
            }
            catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                // Retrying won't make the page appear; let the caller decide.
                throw;
            }
            catch (Exception e)
            {
                lastError = e;
                Console.WriteLine($"FlareSolverr failed >> {url}, retry {retry + 1}/{maxRetries}, Message {e.Message}");
                await Task.Delay(TimeSpan.FromSeconds(2 * (retry + 1)));
            }
        }

        throw lastError ?? new Exception($"Can not get the url via FlareSolverr after {maxRetries} retries!!! ({url})");
    }

    // Returns the response, or null if Cloudflare blocked us (cookies expired/invalid) and we need a new solve.
    private static async Task<PlainResponse> TryPlainRequest(string url, ClearanceSession session,
        IReadOnlyDictionary<string, string> headers = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
            request.Headers.TryAddWithoutValidation(name, value);

        try
        {
            using var response = await session.Client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode && IsChallengePage(body) == false)
                return new PlainResponse(response.StatusCode, body,
                    response.Headers.Concat(response.Content.Headers)
                        .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase));

            // Blocked by Cloudflare -> fall back to FlareSolverr. Anything else (404, 500...) is a real error.
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.ServiceUnavailable
                || IsChallengePage(body))
                return null;

            throw new HttpRequestException($"HTTP {(int)response.StatusCode} for {url}", null, response.StatusCode);
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    // Solves solveUrl with FlareSolverr and caches the new clearance; returns the rendered page. Returns null
    // (without solving) if another request already replaced staleSession while we waited for the lock.
    private static async Task<string> SolveWithFlareSolverr(string host, ClearanceSession staleSession,
        string solveUrl, int maxTimeoutMs)
    {
        var solveLock = SolveLocks.GetOrAdd(host, _ => new SemaphoreSlim(1));
        await solveLock.WaitAsync();
        try
        {
            if (Sessions.TryGetValue(host, out var current) && current != staleSession)
                return null;

            Console.WriteLine($"FlareSolverr solving {solveUrl}");

            using var response = await FlareSolverrClient.PostAsJsonAsync(Endpoint, new
            {
                cmd = "request.get",
                url = solveUrl,
                maxTimeout = maxTimeoutMs
            });

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var status = json.GetProperty("status").GetString();
            if (status != "ok")
                throw new Exception($"FlareSolverr {status}: {json.GetProperty("message").GetString()}");

            var solution = json.GetProperty("solution");
            var httpStatus = solution.GetProperty("status").GetInt32();
            if (httpStatus is < 200 or >= 300)
                throw new Exception($"FlareSolverr got HTTP {httpStatus} for {solveUrl}");

            // The old client isn't disposed: requests on other threads may still be using it.
            Sessions[host] = new ClearanceSession(CreatePlainClient(solution));

            return solution.GetProperty("response").GetString();
        }
        finally
        {
            solveLock.Release();
        }
    }

    // Builds the HttpClient for the direct scrape from FlareSolverr's solution: its cookies go into a
    // CookieContainer (which also picks up any Set-Cookie refreshes) and its userAgent becomes our User-Agent.
    private static HttpClient CreatePlainClient(JsonElement solution)
    {
        var cookies = new CookieContainer();
        foreach (var c in solution.GetProperty("cookies").EnumerateArray())
        {
            cookies.Add(new Cookie(
                c.GetProperty("name").GetString(),
                c.GetProperty("value").GetString(),
                c.GetProperty("path").GetString(),
                c.GetProperty("domain").GetString()));
        }

        var client = new HttpClient(new HttpClientHandler
        {
            CookieContainer = cookies,
            AutomaticDecompression = DecompressionMethods.All
        }) { Timeout = TimeSpan.FromSeconds(30) };

        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", solution.GetProperty("userAgent").GetString());
        return client;
    }

    // Cloudflare's interstitial ("Just a moment...") can come back with a 200 in some setups.
    private static bool IsChallengePage(string html) =>
        html.Contains("<title>Just a moment", StringComparison.OrdinalIgnoreCase)
        || html.Contains("cf-chl-", StringComparison.OrdinalIgnoreCase);
}
