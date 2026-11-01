using System.Net.Http.Json;

namespace BrazilOmu;

public static class Constants
{
    // TODO: change to the public URL of your deployed server (must be HTTPS for iOS)
    public const string ApiBaseUrl = "https://also-scan-metabolism-country.trycloudflare.com";
}

public record LoginResult(bool Ok, string Message, DateTime? ExpiresAt, string? Label);

public static class ApiService
{
    static readonly HttpClient Http = new() { BaseAddress = new Uri(Constants.ApiBaseUrl), Timeout = TimeSpan.FromSeconds(15) };

    public static string DeviceId
    {
        get
        {
            var id = Preferences.Get("device_id", "");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                Preferences.Set("device_id", id);
            }
            return id;
        }
    }

    public static async Task<LoginResult> LoginAsync(string key)
    {
        try
        {
            var resp = await Http.PostAsJsonAsync("/api/auth/login", new { key, deviceId = DeviceId });
            var res = await resp.Content.ReadFromJsonAsync<LoginResult>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            return res ?? new LoginResult(false, "Unexpected server response.", null, null);
        }
        catch
        {
            return new LoginResult(false, "Cannot reach the server. Check your connection.", null, null);
        }
    }

    public static async Task<bool> PingAsync()
    {
        try
        {
            var r = await Http.GetAsync("/api/health");
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
