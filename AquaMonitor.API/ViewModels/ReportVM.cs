using System.ComponentModel.DataAnnotations;

namespace AquaMonitor.API.ViewModels;

public class GenerateReportVM
{
    [Required]
    [Range(2000, 2100, ErrorMessage = "Ano inválido.")]
    public int Year { get; set; }

    [Required]
    [Range(1, 12, ErrorMessage = "Mês deve ser entre 1 e 12.")]
    public int Month { get; set; }

    [Required, MaxLength(2000)]
    public string MitigationPlan { get; set; } = string.Empty;
}

public class ReportResponseVM
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public double TotalConsumptionM3 { get; set; }
    public double SavedWaterM3 { get; set; }
    public int LeaksDetected { get; set; }
    public int LeaksResolved { get; set; }
    public double ReductionPercent { get; set; }
    public string MitigationPlan { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string GeneratedByUser { get; set; } = string.Empty;
}
