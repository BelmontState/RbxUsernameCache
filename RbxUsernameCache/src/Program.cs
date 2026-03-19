using DotNetEnv;
using Microsoft.Extensions.Caching.Hybrid;
using RbxUsernameCache;

var builder = WebApplication.CreateBuilder(args);
Env.Load();
Env.TraversePath().Load();

builder.Services.AddCors(opts =>
{
    opts.AddPolicy("CorsAllow",
        policyBuilder => policyBuilder
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .Build());
});

builder.Services.AddOpenApi();

builder.Services.AddHttpClient();
builder.Services.AddScoped<RobloxApiService>();

string? redisHost = Env.GetString("REDIS_HOST");
string? redisUsername = Env.GetString("REDIS_USERNAME");
string? redisPassword = Env.GetString("REDIS_PASSWORD");
string? redisDb = Env.GetString("REDIS_DB");
if (redisHost == null || redisUsername == null || redisPassword == null || redisDb == null)
{
    throw new InvalidOperationException("Missing redis connection data.");
}

builder.Services.AddStackExchangeRedisCache(options =>
{
    var config = $"{redisHost},user={redisUsername},password={redisPassword},defaultDatabase={redisDb}";
    options.Configuration = config;
});

builder.Services.AddDistributedMemoryCache();

builder.Services.AddHybridCache(options =>
{
    // Default expiration for usernames (e.g., 1 day)
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromDays(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(60) // Keep in RAM for 1 hour
    };
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("CorsAllow");

app.UseHttpsRedirection();

app.MapGet("/user/{id:long}", async (long id, HybridCache cache, RobloxApiService api) =>
{
    // The tag "user-info" allows you to invalidate all users at once if needed
    var username = await cache.GetOrCreateAsync(
        $"user:{id}",
        async cancel => await api.FetchUsernameFromRoblox(id),
        tags: ["user-info"]
    );

    return username is not null ? Results.Ok(new { id, username }) : Results.NotFound();
});

app.Run();