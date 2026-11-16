using Microsoft.ApplicationInsights;

namespace Hip.Integrations.Shared.Functions.Functions.v1.General;

public abstract class BaseTelemetry
{
    private readonly TelemetryClient _telemetryClient;

    protected TelemetryClient TelemetryClient => _telemetryClient;

    protected BaseTelemetry(TelemetryClient telemetryClient)
    {
        _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
    }
}
