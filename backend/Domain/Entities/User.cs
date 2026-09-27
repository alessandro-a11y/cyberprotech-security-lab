namespace Domain.Entities;

/// <summary>
/// Usuário do laboratório. Fase 1: modelo base.
/// Autenticação/autorização serão implementadas na Fase 2.
/// As vulnerabilidades controladas (Fase 3) usarão este modelo.
/// </summary>
public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Hash da senha. Nunca armazenar senha em texto puro.
    /// O preenchimento/hashing será implementado na Fase 2.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Papel do usuário (ex.: "Admin", "User").
    /// Usado futuramente nos laboratórios de Broken Access Control / IDOR (Fase 3).
    /// </summary>
    public string Role { get; set; } = "User";

    /// <summary>
    /// Texto de apresentação exibido no perfil. Opcional.
    /// </summary>
    public string? Bio { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
