using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaceDay.Web.Services;

public class ApiUnauthorizedException : Exception { }
public class ApiUnavailableException(Exception inner) : Exception("The RaceDay API could not be reached.", inner) { }

public class ApiResult
{
    public bool Success { get; set; }
    public HttpStatusCode Status { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, string[]> Errors { get; } = new();
}

public class ApiResult<T> : ApiResult
{
    public T? Data { get; set; }
}

/// <summary>
/// The ONLY way the MVC app touches data: typed HTTP calls to the Part 2 API.
/// The API's session cookie is kept in this app's server-side session and replayed on every call.
/// </summary>
public class ApiClient(HttpClient http, IHttpContextAccessor accessor, IConfiguration config)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private ISession Session => accessor.HttpContext!.Session;

    private string PublicBase => (config["Api:PublicUrl"] ?? config["Api:BaseUrl"] ?? "").TrimEnd('/');

    /// <summary>Turns a stored image URL into one the browser can load (local uploads are relative).</summary>
    public string? ResolveImage(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        return url.StartsWith('/') ? PublicBase + url : url;
    }

    // ---------- public helpers ----------
    public Task<ApiResult<T>> GetAsync<T>(string url) => SendAsync<T>(HttpMethod.Get, url, null);
    public Task<ApiResult<T>> PostAsync<T>(string url, object? payload) => SendAsync<T>(HttpMethod.Post, url, Body(payload));
    public Task<ApiResult<T>> PutAsync<T>(string url, object? payload) => SendAsync<T>(HttpMethod.Put, url, Body(payload));
    public Task<ApiResult> PostAsync(string url, object? payload) => SendAsync(HttpMethod.Post, url, Body(payload));
    public Task<ApiResult> PutAsync(string url, object? payload) => SendAsync(HttpMethod.Put, url, Body(payload));
    public Task<ApiResult> DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url, null);

    public async Task<ApiResult<T>> PostFileAsync<T>(string url, IFormFile file)
    {
        var form = new MultipartFormDataContent();
        var stream = new StreamContent(file.OpenReadStream());
        stream.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        form.Add(stream, "file", file.FileName);
        return await SendAsync<T>(HttpMethod.Post, url, form);
    }

    public Task<ApiResult<T>> LoginAsync<T>(string email, string password) =>
        SendAsync<T>(HttpMethod.Post, "api/auth/login", Body(new { email, password }), isLogin: true);

    // ---------- internals ----------
    private static StringContent Body(object? payload) =>
        new(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, HttpContent? content, bool isLogin = false)
    {
        var (res, body) = await RawAsync(method, url, content, isLogin);
        var result = new ApiResult<T>();
        Fill(result, res, body);
        if (result.Success && !string.IsNullOrWhiteSpace(body))
        {
            try { result.Data = JsonSerializer.Deserialize<T>(body, Json); }
            catch (JsonException) { result.Success = false; result.Message = "The server sent an unexpected response."; }
        }
        return result;
    }

    private async Task<ApiResult> SendAsync(HttpMethod method, string url, HttpContent? content)
    {
        var (res, body) = await RawAsync(method, url, content, false);
        var result = new ApiResult();
        Fill(result, res, body);
        return result;
    }

    private async Task<(HttpResponseMessage Res, string Body)> RawAsync(HttpMethod method, string url, HttpContent? content, bool isLogin)
    {
        try
        {
            using var req = new HttpRequestMessage(method, url) { Content = content };
            var cookie = Session.GetString(SessionKeys.ApiCookie);
            if (!string.IsNullOrEmpty(cookie)) req.Headers.TryAddWithoutValidation("Cookie", cookie);

            var res = await http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            CaptureCookie(res);

            if (res.StatusCode == HttpStatusCode.Unauthorized && !isLogin)
                throw new ApiUnauthorizedException();
            return (res, body);
        }
        catch (HttpRequestException ex) { throw new ApiUnavailableException(ex); }
        catch (TaskCanceledException ex) { throw new ApiUnavailableException(ex); }
    }

    /// <summary>Remember the API's session cookie (login and sliding refreshes) in our own session.</summary>
    private void CaptureCookie(HttpResponseMessage res)
    {
        if (!res.Headers.TryGetValues("Set-Cookie", out var values)) return;
        var pairs = values.Select(v => v.Split(';')[0])
                          .Where(v => v.StartsWith("RaceDay.Session", StringComparison.Ordinal) && !v.EndsWith('='))
                          .ToList();
        if (pairs.Count > 0) Session.SetString(SessionKeys.ApiCookie, string.Join("; ", pairs));
    }

    private static void Fill(ApiResult r, HttpResponseMessage res, string body)
    {
        r.Status = res.StatusCode;
        r.Success = res.IsSuccessStatusCode;
        if (r.Success) return;

        r.Message = res.StatusCode switch
        {
            HttpStatusCode.Forbidden => "You do not have permission to do that.",
            HttpStatusCode.NotFound => "That item could not be found.",
            HttpStatusCode.Unauthorized => "Invalid email or password.",
            _ => "Something went wrong. Please try again."
        };

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
            {
                r.Message = msg.GetString();
            }
            else if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in errors.EnumerateObject())
                    r.Errors[p.Name] = p.Value.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
                r.Message = "Please correct the highlighted fields.";
            }
        }
        catch (JsonException) { /* empty or non-JSON body: keep the friendly default */ }
    }
}
