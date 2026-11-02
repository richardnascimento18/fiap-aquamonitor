namespace AquaMonitor.Application.Abstractions;

/// <summary>One connection per use case; all enlisted repositories share its transaction.</summary>
public interface IWorkSession : IDisposable
{
    IReportRepository Reports { get; }
    ISensorRepository Sensors { get; }
    IReadingRepository Readings { get; }
    IAlertRepository Alerts { get; }
    T InTransaction<T>(Func<T> operation);
}

public interface IWorkSessionFactory
{
    IWorkSession Open();
}
