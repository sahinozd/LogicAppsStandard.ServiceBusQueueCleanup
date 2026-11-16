using System.Diagnostics.CodeAnalysis;

[assembly: CLSCompliant(false)]
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Shared in namespace shouldn't be an issue since our project uses that name.", Scope = "module")]
[assembly: SuppressMessage("Naming", "CA1848: Use the LoggerMessage delegates", Justification = "We can sacrifice performance for ease of use", Scope = "module")]
[assembly: SuppressMessage("Usage", "CA2254: Template should be a static expression", Justification = "Customization is needed in logging and cannot be done in a static way.")]
[assembly: SuppressMessage("Design", "CA1031: Do not catch general exception types", Justification = "One message that fails must not stop the cleanup of the others.", Scope = "type", Target = "~T:Hip.Integrations.Shared.Functions.Repository.Persistence.ServiceBus.DataContext")]
[assembly: SuppressMessage("Performance", "CA1873: Avoid potentially expensive logging", Justification = "We can sacrifice performance for ease of use", Scope = "module")]
