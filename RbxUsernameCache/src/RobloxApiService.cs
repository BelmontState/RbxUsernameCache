namespace RbxUsernameCache;

public sealed class RobloxApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RobloxApiService> _logger;

    public RobloxApiService(HttpClient httpClient, ILogger<RobloxApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<string?> FetchUsernameFromRoblox(long userId)
    {
        try 
        {
            var response = await _httpClient.GetAsync($"https://users.roblox.com/v1/users/{userId}");
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<RobloxUserResponse>();
                return data?.Name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Roblox username for {UserId}", userId);
        }
        
        return null;
    }
}