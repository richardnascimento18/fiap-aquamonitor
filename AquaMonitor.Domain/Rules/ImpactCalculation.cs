namespace AquaMonitor.Domain.Rules;

/// <summary>Period-over-period consumption difference; not evidence of savings caused by monitoring.</summary>
public sealed record ImpactCalculation(double TotalConsumptionM3, double SavedWaterM3, double ReductionPercent)
{
    public static ImpactCalculation Calculate(double currentLiters, double previousLiters)
    {
        var difference = previousLiters - currentLiters;
        var reduction = previousLiters > 0 ? Math.Round(difference / previousLiters * 100, 2) : 0;
        if (!double.IsFinite(currentLiters) || currentLiters < 0 || !double.IsFinite(previousLiters) || previousLiters < 0 ||
            !double.IsFinite(difference) || !double.IsFinite(reduction))
            throw new DomainRuleException("Consumo fora do intervalo suportado para calculo.");
        return new(Math.Round(currentLiters / 1000, 3), Math.Round(difference / 1000, 3), reduction);
    }
}

public sealed record ReportingPeriod
{
    public DateTime Start { get; }
    public DateTime EndExclusive { get; }
    public DateTime PreviousStart { get; }

    public ReportingPeriod(int year, int month)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
            throw new DomainRuleException("Periodo invalido.");
        Start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        EndExclusive = Start.AddMonths(1);
        PreviousStart = Start.AddMonths(-1);
    }
}
