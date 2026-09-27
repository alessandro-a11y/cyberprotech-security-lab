using Application.Interfaces;

namespace Infrastructure.Security;

/// <summary>
/// Hash de senha com BCrypt e sal aleatório por senha.
/// </summary>
/// <remarks>
/// BCrypt já embute o sal e tem custo ajustável (<c>WorkFactor</c>), o que
/// deixa o custo maior conforme o hardware melhora.
/// Para produção, considere Argon2id; o contrato <see cref="IPasswordHasher"/>
/// existe para permitir essa troca sem tocar nos controllers.
/// </remarks>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Custo 12: ~250 ms por hash em hardware de laboratório. Aumentar conforme
    /// a máquina ficar mais rápida, ou o login passa a ser viável de força bruta.
    /// </summary>
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash) =>
        VerifyCore(password, passwordHash);

    public bool VerifyOrDummy(string password, string? passwordHash) =>
        VerifyCore(password, passwordHash ?? DummyHash.Value);

    private static bool VerifyCore(string password, string? passwordHash)
    {
        if (string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Hash de outro algoritmo (ou seed antigo): login inválido, sem stack trace.
            return false;
        }
    }

    /// <summary>
    /// Hash descartável, de uma senha aleatória. Comparar contra ele sempre falha,
    /// mas custa o mesmo que uma verificação real.
    /// </summary>
    private static readonly Lazy<string> DummyHash = new(
        () => BCrypt.Net.BCrypt.HashPassword(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), WorkFactor),
        LazyThreadSafetyMode.ExecutionAndPublication);
}
