namespace AquaMonitor.API.Models;
public class WaterReading
{
    public int Id { get; set; }
    public int SensorId { get; set; }
    public string SensorName { get; set; } = string.Empty;
    public string SensorLocation { get; set; } = string.Empty;
    public double LitersConsumed { get; set; }
    public double FlowRateLitersPerHour { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public bool IsAnomaly { get; set; }
}
