using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpenPortal.Web;

namespace OpenPortal.Tests.Integration;

/// <summary>
/// An <see cref="HttpClient"/> wrapper that handles the antiforgery handshake, so tests can state what they
/// are checking instead of restating the protocol on every call.
/// <para>
/// Handshake deliberately: obtain a token, then use it. A test that skips straight to a POST and expects
/// success would silently pass against a host with no antiforgery check at all, which is the opposite of
/// what these tests are for.
/// </para>
/// </summary>
public sealed class ApiClient : IDisposable
{
    private readonly HttpClient _http;
    private bool _ownsHttpClient;

    private ApiClient(HttpClient http, bool ownsHttpClient)
    {
        _http = http;
        _ownsHttpClient = ownsHttpClient;
    }

    public static async Task<ApiClient> CreateAsync(OpenPortalFactory factory)
    {
        var http = factory.CreateClient();
        var client = new ApiClient(http, ownsHttpClient: true);
        await client.RefreshAntiforgeryTokenAsync();
        return client;
    }

    public static ApiClient For(HttpClient http) => new(http, ownsHttpClient: false);

    public HttpClient Http => _http;

    /// <summary>Fetches a fresh token. Called automatically before the first unsafe request and after sign-in.</summary>
    public async Task RefreshAntiforgeryTokenAsync()
    {
        using var response = await _http.GetAsync("/api/auth/antiforgery");
        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            $"The antiforgery handshake must succeed before anything else can. Body: {await response.Content.ReadAsStringAsync()}");

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = payload.GetProperty("requestToken").GetString();
        var headerName = payload.GetProperty("headerName").GetString();

        token.ShouldNotBeNullOrWhiteSpace();
        headerName.ShouldBe(AntiforgeryDefaults.HeaderName);

        _http.DefaultRequestHeaders.Remove(AntiforgeryDefaults.HeaderName);
        _http.DefaultRequestHeaders.Add(AntiforgeryDefaults.HeaderName, token);
    }

    public Task<HttpResponseMessage> GetAsync(string path) => _http.GetAsync(path);

    /// <summary>
    /// Signs in and then obtains a fresh antiforgery token.
    /// <para>
    /// The refresh is not optional. ASP.NET Core binds an antiforgery token to the identity that requested
    /// it, so the token a client fetched before signing in is rejected afterwards with "meant for a
    /// different claims-based user". Any client - including the SPA - must re-run the handshake after every
    /// change of identity, and this helper makes that requirement impossible to forget in a test.
    /// </para>
    /// </summary>
    public async Task<HttpResponseMessage> SignInAsync(string email, string password, bool rememberMe = false)
    {
        var response = await PostAsync("/api/auth/login", new { email, password, rememberMe });
        if (response.IsSuccessStatusCode)
        {
            await RefreshAntiforgeryTokenAsync();
        }

        return response;
    }

    public async Task SignOutAsync()
    {
        using var response = await PostAsync("/api/auth/logout");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await RefreshAntiforgeryTokenAsync();
    }

    public Task<HttpResponseMessage> PostAsync(string path, object? body = null) =>
        _http.PostAsJsonAsync(path, body ?? new { });

    public Task<HttpResponseMessage> PostFormAsync(string path, IDictionary<string, string> fields) =>
        _http.PostAsync(path, new FormUrlEncodedContent(fields));

    public Task<HttpResponseMessage> PutAsync(string path, object body) =>
        _http.PutAsJsonAsync(path, body);

    public Task<HttpResponseMessage> DeleteAsync(string path) => _http.DeleteAsync(path);

    /// <summary>
    /// Sends an unsafe request carrying no antiforgery token.
    /// <para>
    /// The header has to be removed rather than merely not set: a default header left on the client would
    /// make this call indistinguishable from a normal one, and the test would silently stop testing
    /// anything.
    /// </para>
    /// </summary>
    public async Task<HttpResponseMessage> PostWithoutTokenAsync(string path, object? body = null)
    {
        _http.DefaultRequestHeaders.Remove(AntiforgeryDefaults.HeaderName);

        try
        {
            return await _http.PostAsJsonAsync(path, body ?? new { });
        }
        finally
        {
            // Restore the token so later calls on this client behave normally.
            await RefreshAntiforgeryTokenAsync();
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }
}