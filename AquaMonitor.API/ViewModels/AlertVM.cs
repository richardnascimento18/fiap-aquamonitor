using System.ComponentModel.DataAnnotations;
namespace AquaMonitor.API.ViewModels;
public class AlertResponseVM
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double TriggerFlowRate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int SensorId { get; set; }
    public string SensorName { get; set; } = string.Empty;
    public string SensorLocation { get; set; } = string.Empty;
}
public class UpdateAlertStatusVM
{
    [Required][Range(0,2)] public int Status { get; set; }
}
public class AlertFilterVM
{
    public int? SensorId { get; set; }
    public int? Status { get; set; }
    public int? Severity { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
