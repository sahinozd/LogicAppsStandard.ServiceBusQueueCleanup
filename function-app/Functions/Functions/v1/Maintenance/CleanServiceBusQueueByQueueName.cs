using Hip.Integrations.Shared.Functions.Business.Maintenance.Interfaces;
using Hip.Integrations.Shared.Functions.Extensions;
using Hip.Integrations.Shared.Functions.Functions.v1.General;
using Hip.Integrations.Shared.Functions.Models.Hip.ServiceBus;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace Hip.Integrations.Shared.Functions.Functions.v1.Maintenance;

public class CleanServiceBusQueueByQueueName : BaseFunction
{
    private readonly ICleanServiceBusQueueService _service;

    public CleanServiceBusQueueByQueueName(IHttpClientFactory httpClientFactory, ICleanServiceBusQueueService service, TelemetryClient telemetryClient) : base(httpClientFactory, telemetryClient)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    [Function(nameof(CleanServiceBusQueueByQueueName))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "general/v1/hip/clean-servicebus-queues")]
        HttpRequestData req)
    {
        ArgumentNullException.ThrowIfNull(req);

        var correlationId = await req.GetCorrelationIdAsync().ConfigureAwait(false);
        var callbackUrl = await req.GetCallbackUrlAsync().ConfigureAwait(false);

        // The _ = ExecuteMethod makes sure that a new task is started as fire and forget, without waiting for it. This way we can return with a 202, while the background task will call the callbackurl when finished.
        _ = ExecuteFunctionTaskAsync(async () =>
        {
            var message = await req.GetJsonBodyAsync<ServiceBusMessageContainer<IList<ServiceBusQueue>>>().ConfigureAwait(false);
            await _service.ExecuteAsync(message, req.GetRequestCancellationToken()).ConfigureAwait(false);

        }, req, correlationId, new Uri(callbackUrl));

        TelemetryClient.TrackTrace($"Return accepted (202) to: {callbackUrl} for correlation id: {correlationId}");
        return req.CreateResponse(HttpStatusCode.Accepted);
    }
}