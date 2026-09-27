namespace Application.Interfaces;

/// <summary>
/// Geração e verificação de hash de senha.
/// </summary>
/// <remarks>
/// Senhas nunca são armazenadas em texto puro: a camada de aplicação grava
/// apenas o hash e a verificação acontece no login.
/// Na Fase 3 este contrato é o ponto de instalação do cenário de hash fraco
/// (ex.: trocar BCrypt por SHA-256 sem sal).
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>
    /// Gera o hash de <paramref name="password"/> com um sal aleatório.
    /// O mesmo senha gera hashes diferentes a cada chamada.
    /// </summary>
    string Hash(string password);

    /// <summary>
    /// Confere a senha contra o hash. Deve devolver <c>false</c> (e nunca
    /// lançar) quando o hash é inválido ou não pertence a esse algoritmo.
    /// </summary>
    bool Verify(string password, string passwordHash);

    /// <summary>
    /// Como <see cref="Verify"/>, mas aceita hash nulo e ainda executa a mesma
    /// operação de hash. Custa caro de propósito: existe para que o login de um
    /// usuário inexistente não seja mais rápido que o de um usuário existente,
    /// o que revelaria a existência da conta pelo tempo de resposta.
    /// </summary>
    bool VerifyOrDummy(string password, string? passwordHash);
}
