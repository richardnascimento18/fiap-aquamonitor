namespace AquaMonitor.Domain.Rules;

public sealed class DomainRuleException(string message) : Exception(message);
