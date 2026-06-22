namespace AquaMonitor.API.Models;
public class WaterSensor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double AlertThresholdLitersPerHour { get; set; } = 500;
    public bool IsActive { get; set; } = true;
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
}
