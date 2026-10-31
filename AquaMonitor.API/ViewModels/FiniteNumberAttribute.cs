using System.ComponentModel.DataAnnotations;

namespace AquaMonitor.API.ViewModels;

[AttributeUsage(AttributeTargets.Property)]
public sealed class FiniteNumberAttribute : ValidationAttribute
{
    public FiniteNumberAttribute() : base("O numero deve ser finito.") { }
    public override bool IsValid(object? value) => value is null || value is double number && double.IsFinite(number);
}
