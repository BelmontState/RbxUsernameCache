using System.Net;
using Polly;
using Polly.Retry;

namespace RbxUsernameCache;

public sealed class RobloxApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RobloxApiService> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public RobloxApiService(HttpClient httpClient, ILogger<RobloxApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        // Define the retry policy
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => r.StatusCode == (HttpStatusCode)429)
            .WaitAndRetryAsync(3, retryAttempt => 
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff: 2s, 4s, 8s
                (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Rate limited by Roblox. Retrying in {Delay}s (Attempt {Count})", 
                        timespan.TotalSeconds, retryCount);
                });
    }

    public async Task<string?> FetchUsernameFromRoblox(long userId)
    {
        try 
        {
            // Execute the request within the policy
            using var response = await _retryPolicy.ExecuteAsync(() => 
                _httpClient.GetAsync($"https://users.roblox.com/v1/users/{userId}")
            );

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<RobloxUserResponse>();
                return data?.Name;
            }

            if ((int)response.StatusCode is 429 or >= 500)
            {
                _logger.LogError("Max retries reached. Still erroring for UserID: {UserId}", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Roblox username for {UserId}", userId);
        }
        
        return null;
    }
}