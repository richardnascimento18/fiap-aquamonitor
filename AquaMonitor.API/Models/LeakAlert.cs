namespace AquaMonitor.API.Models;
public class LeakAlert
{
    public int Id { get; set; }
    public int SensorId { get; set; }
    public string SensorName { get; set; } = string.Empty;
    public string SensorLocation { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Severity { get; set; }
    public string SeverityLabel => Severity switch { 4 => "Critical", 3 => "High", 2 => "Medium", _ => "Low" };
    public int Status { get; set; }
    public string StatusLabel => Status switch { 2 => "Resolved", 1 => "Acknowledged", _ => "Open" };
    public double TriggerFlowRate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
