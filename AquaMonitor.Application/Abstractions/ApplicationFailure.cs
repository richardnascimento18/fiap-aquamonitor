namespace AquaMonitor.Application.Abstractions;

/// <summary>Expected use-case failure; the host maps its code and optional message to the existing contract.</summary>
public sealed class ApplicationFailure(int statusCode, string? message = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string? PublicMessage { get; } = message;
}
