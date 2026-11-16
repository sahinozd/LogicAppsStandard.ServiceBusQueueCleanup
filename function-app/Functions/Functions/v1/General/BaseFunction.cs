using Hip.Integrations.Shared.Functions.Extensions;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker.Http;
using Newtonsoft.Json;
using System.Text;

namespace Hip.Integrations.Shared.Functions.Functions.v1.General;

public abstract class BaseFunction : BaseTelemetry
{
    private readonly IHttpClientFactory _httpClientFactory;

    protected BaseFunction(IHttpClientFactory httpClientFactory, TelemetryClient telemetryClient) : base(telemetryClient)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    protected async Task ExecuteFunctionTaskAsync(Func<Task> action, HttpRequestData req, string correlationId, Uri callbackUri)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(req);
        ArgumentNullException.ThrowIfNull(callbackUri);

        var httpClient = _httpClientFactory.CreateClient("CallbackHttpClient");
        using var request = new HttpRequestMessage(HttpMethod.Post, callbackUri);

        try
        {
            if (!await req.MockingEnabledAsync().ConfigureAwait(false))
            {
                await action().ConfigureAwait(false);
            }

            TelemetryClient.TrackTrace($"Return ok (200) to: {callbackUri.ToString()} for correlation id: {correlationId}");
            request.Content = CreateResponse(200, null);
        }
        catch (Exception ex)
        {
            request.Content = CreateResponse(500, ex.Message);
            TelemetryClient.TrackTrace($"Return internal server error (500) to: {callbackUri.ToString()} for correlation id: {correlationId}. Exception: {ex.Message}");
        }
        finally
        {
            await httpClient.SendAsync(request, req.GetRequestCancellationToken()).ConfigureAwait(false);
        }
    }

    protected static HttpContent CreateResponse(int statusCode, string? message)
    {
        var responseMessage = new
        {
            statusCode,
            message
        };

        var data = JsonConvert.SerializeObject(responseMessage);
        return new StringContent(data, Encoding.UTF8, "application/json");
    }
}