using Hip.Integrations.Shared.Functions.Business.Extensions;
using Hip.Integrations.Shared.Functions.Repository.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // Registers the TelemetryClient that BaseFunction uses. Without APPLICATIONINSIGHTS_CONNECTION_STRING it simply sends nothing.
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.Configure<LoggerFilterOptions>(options =>
        {
            // The Application Insights SDK adds a default filter that only captures Warning and more severe logs. Remove it to capture Information as well.
            var toRemove = options.Rules.FirstOrDefault(rule => rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");

            if (toRemove is not null)
            {
                options.Rules.Remove(toRemove);
            }
        });

        // Used by BaseFunction to call the callback url of the Logic App HttpWebhook action once the cleanup has finished.
        services.AddHttpClient("CallbackHttpClient");

        services.RegisterBusinessDependencies();
        services.RegisterRepositoryDependencies();
    })
    .Build();

await host.RunAsync().ConfigureAwait(false);

[ExcludeFromCodeCoverage(Justification = "Top-level statements are not testable")]
internal partial class Program;
