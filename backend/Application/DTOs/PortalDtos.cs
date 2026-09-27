using System.ComponentModel.DataAnnotations;

namespace Application.DTOs;

/// <summary>
/// Campos que o próprio usuário pode editar no próprio perfil.
/// </summary>
/// <remarks>
/// Não inclui <c>Username</c> (o campo é somente-leitura na interface) nem
/// <c>Role</c> (só um Admin altera papel, e por outro endpoint). O e-mail é
/// conferido contra duplicidade, porque tem índice único.
/// </remarks>
public sealed class UpdateProfileRequest
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(256, ErrorMessage = "O e-mail deve ter no máximo 256 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [StringLength(512, ErrorMessage = "A bio deve ter no máximo 512 caracteres.")]
    public string? Bio { get; set; }
}

/// <summary>
/// Troca de senha pelo próprio usuário.
/// </summary>
/// <remarks>
/// A senha atual é exigida: sem ela, um token vazado já bastaria para trocar a
/// senha e tomar a conta inteira. O token não expira ao trocar a senha (ver
/// limitação registrada em <c>backend/README.md</c>).
/// </remarks>
public sealed class ChangePasswordRequest
{
    [Required(ErrorMessage = "Informe a senha atual.")]
    [StringLength(128, ErrorMessage = "Senha atual inválida.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a nova senha.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "A nova senha deve ter entre 8 e 128 caracteres.")]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Novo papel de um usuário. Restrito à allowlist — um papel arbitrário viria
/// direto do cliente e criaria papel que nenhuma policy reconhece.
/// </summary>
public sealed class ChangeRoleRequest
{
    [Required(ErrorMessage = "Informe o papel.")]
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// Contagens da área administrativa, para o Dashboard e a tela de Administração.
/// </summary>
/// <param name="TotalUsers">Total de contas cadastradas.</param>
/// <param name="Admins">Contas com papel Admin.</param>
/// <param name="RegularUsers">Contas com papel User.</param>
/// <param name="AdminPercentage">Percentual de contas com privilégio (0 a 100).</param>
public sealed record AdminStats(int TotalUsers, int Admins, int RegularUsers, int AdminPercentage);
