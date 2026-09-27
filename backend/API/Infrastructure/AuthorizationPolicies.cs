namespace API.Infrastructure;

/// <summary>
/// Nomes das policies de autorização. Centralizar evita strings soltas nos
/// controllers, que quebram em silêncio quando o nome muda.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Exclusiva de quem tem o papel Admin.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>Qualquer usuário autenticado.</summary>
    public const string Authenticated = "Authenticated";

    /// <summary>Papel que concede acesso à área administrativa.</summary>
    public const string AdminRole = "Admin";

    /// <summary>Papel atribuído ao auto-cadastro.</summary>
    public const string UserRole = "User";
}
