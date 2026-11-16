using Microsoft.Azure.Functions.Worker.Http;

namespace Hip.Integrations.Shared.Functions.Extensions;

public static class HttpRequestDataExtensions
{
    public static async Task<T> GetJsonBodyAsync<T>(this HttpRequestData req)
    {
        ArgumentNullException.ThrowIfNull(req);
        return await HttpRequestExtensionBase.GetJsonBodyAsync<T>(req.Body).ConfigureAwait(false);
    }

    public static Task<bool> MockingEnabledAsync(this HttpRequestData req)
    {
        ArgumentNullException.ThrowIfNull(req);
        req.Headers.TryGetValues("MockingEnabled", out var header);
        return Task.FromResult(bool.TryParse(header?.FirstOrDefault(), out var isMockEnabled) && isMockEnabled);
    }

    public static Task<string> GetCorrelationIdAsync(this HttpRequestData req)
    {
        ArgumentNullException.ThrowIfNull(req);
        req.Headers.TryGetValues("CorrelationId", out var header);
        ArgumentNullException.ThrowIfNull(header);

        var correlationId = header.ToList().FirstOrDefault();
        ArgumentException.ThrowIfNullOrEmpty(correlationId);

        return Task.FromResult(correlationId);
    }

    public static Task<string> GetCallbackUrlAsync(this HttpRequestData req)
    {
        ArgumentNullException.ThrowIfNull(req);
        req.Headers.TryGetValues("CallbackUrl", out var header);
        ArgumentNullException.ThrowIfNull(header);

        var callbackUrl = header.ToList().FirstOrDefault();
        ArgumentException.ThrowIfNullOrEmpty(callbackUrl);

        return Task.FromResult(callbackUrl);
    }

    public static CancellationToken GetRequestCancellationToken(this HttpRequestData req)
    {
        ArgumentNullException.ThrowIfNull(req);
        return req.FunctionContext.CancellationToken;
    }
}
