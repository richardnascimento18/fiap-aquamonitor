using System.ComponentModel.DataAnnotations;
namespace AquaMonitor.API.ViewModels;
public class LoginVM
{
    [Required(ErrorMessage="E-mail e obrigatorio.")]
    [EmailAddress(ErrorMessage="Formato de e-mail invalido.")]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage="Senha e obrigatoria.")]
    [MinLength(6, ErrorMessage="Senha deve ter ao menos 6 caracteres.")]
    public string Password { get; set; } = string.Empty;
}
public class RegisterVM
{
    [Required(ErrorMessage="Nome e obrigatorio.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    [Required(ErrorMessage="E-mail e obrigatorio.")]
    [EmailAddress(ErrorMessage="Formato de e-mail invalido.")]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage="Senha e obrigatoria.")]
    [MinLength(8, ErrorMessage="Senha deve ter ao menos 8 caracteres.")]
    public string Password { get; set; } = string.Empty;
}
public class AuthResponseVM
{
    public string Token { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
