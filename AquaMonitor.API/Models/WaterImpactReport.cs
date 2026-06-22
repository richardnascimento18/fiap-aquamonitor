namespace AquaMonitor.API.Models;
public class WaterImpactReport
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
    public int GeneratedByUserId { get; set; }
    public string GeneratedByUser { get; set; } = string.Empty;
}
