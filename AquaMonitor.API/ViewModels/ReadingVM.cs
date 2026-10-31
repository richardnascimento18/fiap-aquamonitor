using System.ComponentModel.DataAnnotations;

namespace AquaMonitor.API.ViewModels;

public class CreateReadingVM
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "SensorId inválido.")]
    public int SensorId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Consumo não pode ser negativo.")]
    [FiniteNumber]
    public double LitersConsumed { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Vazão não pode ser negativa.")]
    [FiniteNumber]
    public double FlowRateLitersPerHour { get; set; }

    public DateTime? RecordedAt { get; set; }
}

public class ReadingResponseVM
{
    public int Id { get; set; }
    public int SensorId { get; set; }
    public string SensorName { get; set; } = string.Empty;
    public string SensorLocation { get; set; } = string.Empty;
    public double LitersConsumed { get; set; }
    public double FlowRateLitersPerHour { get; set; }
    public DateTime RecordedAt { get; set; }
    public bool IsAnomaly { get; set; }
}

public class ReadingFilterVM
{
    [Range(1, int.MaxValue)]
    public int? SensorId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public bool? AnomaliesOnly { get; set; }
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public class ConsumptionSummaryVM
{
    public int SensorId { get; set; }
    public string SensorName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double TotalLiters { get; set; }
    public double TotalM3 => TotalLiters / 1000;
    public double AverageFlowRate { get; set; }
    public double MaxFlowRate { get; set; }
    public int AnomalyCount { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
}
