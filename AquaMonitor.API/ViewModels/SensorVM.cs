using System.ComponentModel.DataAnnotations;

namespace AquaMonitor.API.ViewModels;

public class CreateSensorVM
{
    [Required(ErrorMessage = "Nome do sensor é obrigatório.")]
    [MaxLength(100, ErrorMessage = "Nome não pode exceder 100 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Localização é obrigatória.")]
    [MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    [Range(0.1, 100000, ErrorMessage = "Limite de alerta deve ser entre 0.1 e 100.000 L/h.")]
    [FiniteNumber]
    public double AlertThresholdLitersPerHour { get; set; } = 500;
}

public class UpdateSensorVM
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    [Range(0.1, 100000)]
    [FiniteNumber]
    public double? AlertThresholdLitersPerHour { get; set; }

    public bool? IsActive { get; set; }
}

public class SensorResponseVM
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double AlertThresholdLitersPerHour { get; set; }
    public bool IsActive { get; set; }
    public DateTime InstalledAt { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int TotalReadings { get; set; }
    public int OpenAlerts { get; set; }
}
