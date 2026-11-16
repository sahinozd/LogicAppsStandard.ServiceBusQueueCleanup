# Service Bus queue cleanup

Companion code for the blog post *Cleaning up Service Bus queues centrally: active, dead-letter, session and deferred messages*.

A Logic Apps Standard workflow sends a cleanup instruction to an Azure Function. The Function peeks each queue and settles whatever is in it, including the messages that regular tooling can't reach: deferred messages, deferred session messages and session messages.

## What's in here

| Folder | Contents |
| --- | --- |
| `logic-app/` | Logic Apps Standard project with the `clean-queue-messages` workflow. It calls the Function with an HttpWebhook action and waits for the callback. |
| `function-app/` | .NET 10 isolated Azure Functions solution with one function: `CleanServiceBusQueueByQueueName` (`POST /general/v1/hip/clean-servicebus-queues`). |
| `sample-messages/` | Request bodies for the workflow (and the Function). |

The Function solution keeps the layering of the platform it comes from:

```text
Functions   CleanServiceBusQueueByQueueName -> BaseFunction (202 + callback)
Business    CleanServiceBusQueueService     -> decides what to do per queue
Repository  ServiceBusRepository -> DataContext (Azure.Messaging.ServiceBus)
Models      ServiceBusMessageContainer, ServiceBusQueue, MessageInfo
```

## The request

```json
{
  "BatchSize": 500,
  "HandleSessionAndDeferredMessages": true,
  "DeadletterActiveQueueMessages": false,
  "ClearDeadletterQueueMessages": false,
  "Data": [ { "Name": "sbq-targetsystem-datatype-sourcesystem-prc" } ]
}
```

| Property | Effect per queue |
| --- | --- |
| `BatchSize` | Maximum number of messages that are peeked (and therefore handled) per queue. Defaults to 100. |
| `HandleSessionAndDeferredMessages` | Deferred session messages are completed. Deferred messages are dead-lettered. Active session messages are completed. |
| `DeadletterActiveQueueMessages` | Active messages on a queue **without** sessions are dead-lettered. Skipped for session queues and for queues that still hold deferred messages. |
| `ClearDeadletterQueueMessages` | Messages on the dead-letter subqueue are received and completed, so they're removed for good. |

The steps run in this order, so `full-cleanup.json` first dead-letters deferred and active messages and then purges the dead-letter queue.

## Run the Function locally

Prerequisites: .NET 10 SDK, Azure Functions Core Tools 4.x and an Azure identity that `DefaultAzureCredential` can pick up (`az login`, Visual Studio or VS Code). That identity needs the **Azure Service Bus Data Owner** role on the namespace. Receiving needs *Data Receiver*, but checking whether a queue requires sessions is a management operation.

```powershell
cd function-app
dotnet build
cd Functions
copy example.settings.json local.settings.json   # then set serviceBusNamespace
func start --port 7227
```

Then send `Functions/requests.http`, or:

```powershell
curl -X POST http://localhost:7227/general/v1/hip/clean-servicebus-queues `
  -H "CorrelationId: local-test-1" `
  -H "CallbackUrl: https://<something-that-accepts-a-post>" `
  --data "@../../sample-messages/clean-deferred-and-session-messages.json"
```

The Function answers `202 Accepted` immediately and posts `{"statusCode":200,"message":null}` (or `500` with the error) to `CallbackUrl` when the cleanup has finished. Add the header `MockingEnabled: true` to test that flow without touching Service Bus.

`AzureWebJobsStorage` points at Azurite; start it, or set your own storage connection string.

## Deploy

- **Function App**: Flex Consumption or an App Service plan, .NET 10 isolated, with a system-assigned managed identity that has *Azure Service Bus Data Owner* on the namespace. App setting `serviceBusNamespace` = `<namespace>.servicebus.windows.net`. `host.json` sets `functionTimeout` to one hour.
- **Protect the endpoint.** The function uses `AuthorizationLevel.Anonymous` because authentication is done by App Service Authentication (Easy Auth) in front of it. Enable it with Microsoft Entra ID and restrict *allowed client applications* to the client id of the Logic App's managed identity. Never deploy this function without that: anyone who can call it can empty your queues.
- **Logic App**: deploy the `logic-app` folder to a Logic Apps Standard app with a system-assigned managed identity, and set the app settings `sharedFunctionAppBaseUrl` (with a trailing `/`), `functionAppResourceAudience` and `isMockEnabled`.

## Differences from the original platform code

This repo is a trimmed copy of the maintenance function in my integration platform. Everything that isn't needed to run `CleanServiceBusQueueByQueueName` has been removed: other functions, Swagger, the caller-authorization and logging middleware, Blob/HTTP repositories and the tests. Two changes go further than trimming:

- `DataContext.CompleteSessionMessagesAsync` receives session messages in batches until the session is empty. The original used `ReceiveMessagesAsync(CancellationToken)`, an `IAsyncEnumerable` that never ends. It kept polling an empty session until the session lock expired and threw `SessionLockLost`.
- The *client initialized* log line shows the namespace instead of the client's type name.
