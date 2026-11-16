using System.Diagnostics.CodeAnalysis;

[assembly: CLSCompliant(false)]
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Shared in namespace shouldn't be an issue since our project uses that name.", Scope = "module")]
[assembly: SuppressMessage("Maintainabilty", "CA1515: Consider making public types internal", Justification = "We can sacrifice maintainabilty for ease of use", Scope = "namespaceanddescendants", Target = "~N:Hip.Integrations.Shared.Functions.Functions")]
[assembly: SuppressMessage("Maintainabilty", "CA1515: Consider making public types internal", Justification = "We can sacrifice maintainabilty for ease of use", Scope = "namespaceanddescendants", Target = "~N:Hip.Integrations.Shared.Functions.Extensions")]
[assembly: SuppressMessage("Naming", "CA1724: Type names should not match namespaces", Justification = "This conflict must not be an issue in our project.", Scope = "module")]
[assembly: SuppressMessage("Design", "CA1031: Do not catch general exception types", Justification = "The callback must always be sent, also when the cleanup fails.", Scope = "module")]
