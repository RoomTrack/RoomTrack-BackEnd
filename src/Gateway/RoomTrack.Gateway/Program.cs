using RoomTrack.Gateway;

// API gateway of RoomTrack: the only public entry point. It routes /api/v1/* to the service that owns each resource
// (YARP, routes in appsettings.json), applies CORS once for every service, and serves the API documentation of all
// the services in one Swagger UI. The internal API of the services (/internal/v1/*) is never routed.
var builder = WebApplication.CreateBuilder(args);

const string corsPolicy = "AllowFrontend";
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(origin => origin.TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options => options.AddPolicy(corsPolicy, policy =>
{
    // No origin configured: browsers from other origins are rejected.
    if (allowedOrigins.Length == 0) return;
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddHealthChecks();
builder.Services.AddKeepAwake();
builder.Services.AddWakeBeforeProxy();

var app = builder.Build();

if (allowedOrigins.Length == 0)
    app.Logger.LogWarning("CORS: no origins configured (Cors__AllowedOrigins). Cross-origin browser requests will be rejected.");
else
    app.Logger.LogInformation("CORS: allowed origins: {Origins}", string.Join(", ", allowedOrigins));

// Before everything else so that /health (the scheduler's wake-up call) also wakes the services.
app.UseKeepAwake();
app.UseCors(corsPolicy);

// One Swagger UI for every service: each document is fetched through the gateway (/docs/{service}/...).
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "docs";
    options.DocumentTitle = "RoomTrack API";
    // Wakes the sleeping services from the browser while the documents load through the gateway.
    options.HeadContent = KeepAwake.BrowserWakeScript(app.Configuration);
    foreach (var service in app.Configuration.GetSection("Documentation:Services").GetChildren())
        options.SwaggerEndpoint($"/docs/{service.Key}/swagger/v1/swagger.json", service.Value ?? service.Key);
});

app.MapGet("/", () => Results.Redirect("/docs")).ExcludeFromDescription();
app.MapHealthChecks("/health");
// The default proxy pipeline, plus waiting for a sleeping destination to boot before forwarding to it.
app.MapReverseProxy(proxy =>
{
    proxy.UseSessionAffinity();
    proxy.UseLoadBalancing();
    proxy.UseWakeBeforeProxy();
    proxy.UsePassiveHealthChecks();
});

app.Run();
