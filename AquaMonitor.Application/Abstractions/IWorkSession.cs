namespace AquaMonitor.Application.Abstractions;

/// <summary>One connection per use case; all enlisted repositories share its transaction.</summary>
public interface IWorkSession : IDisposable
{
    ISensorRepository Sensors { get; }
    T InTransaction<T>(Func<T> operation);
}

public interface IWorkSessionFactory
{
    IWorkSession Open();
}
