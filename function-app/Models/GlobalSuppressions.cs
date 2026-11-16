using System.Diagnostics.CodeAnalysis;

[assembly: CLSCompliant(false)]
[assembly: SuppressMessage("Naming", "CA1711: Identifiers should not have incorrect suffix", Justification = "ServiceBusQueue is a fine name to use, and therefore we disable this.", Scope = "module")]
[assembly: SuppressMessage("Naming", "CA1716: Identifiers should not match keywords", Justification = "Shared in namespace shouldn't be an issue since our project uses that name.", Scope = "module")]
