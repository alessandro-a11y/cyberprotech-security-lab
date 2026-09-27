using Application.Interfaces;
using Infrastructure.Lab;
using Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CyberProtech.Tests.Unit;

/// <summary>
/// Hash e verificação de senha. São os controles que o cenário de
/// "falhas de autenticação" do laboratório precisa continuar verificáveis.
/// </summary>
public class BcryptPasswordHasherTests
{
    private readonly IPasswordHasher hasher = new BcryptPasswordHasher();

    [Fact]
    public void Hash_devolve_um_hash_bcrypt()
    {
        var hash = hasher.Hash("CyberProtech@2026");

        Assert.StartsWith("$2", hash);
        Assert.Contains("$12$", hash); // custo 12
    }

    [Fact]
    public void A_mesma_senha_gera_hashes_diferentes_por_sal()
    {
        var primeiro = hasher.Hash("mesma-senha");
        var segundo = hasher.Hash("mesma-senha");

        // Sem sal por senha, hashes iguais seriam o caminho para rainbow table.
        Assert.NotEqual(primeiro, segundo);
    }

    [Fact]
    public void Verifica_a_senha_correta()
    {
        var hash = hasher.Hash("senha-correta");

        Assert.True(hasher.Verify("senha-correta", hash));
    }

    [Theory]
    [InlineData("senha-errada")]
    [InlineData("")]
    [InlineData("senha-correta ")]
    public void Rejeita_senha_errada(string tentativa)
    {
        var hash = hasher.Hash("senha-correta");

        Assert.False(hasher.Verify(tentativa, hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nao-e-um-hash")]
    [InlineData("!pending:fase-2")]
    public void Hash_invalido_devolve_false_e_nao_lanca(string hash)
    {
        // Um seed antigo ou de outro algoritmo não pode derrubar o login com
        // exceção: o resultado tem que ser apenas "senha inválida".
        Assert.False(hasher.Verify("qualquer", hash));
    }

    [Fact]
    public void VerifyOrDummy_rejeita_quando_o_hash_e_nulo()
    {
        Assert.False(hasher.VerifyOrDummy("qualquer", null));
    }

    [Fact]
    public void VerifyOrDummy_gasta_tempo_similares_ao_de_um_hash_real()
    {
        // É isto que impede a enumeração de usuário pelo tempo de resposta.
        // Com tolerância larga porque a máquina pode estar sob carga.
        var hashReal = hasher.Hash("senha-correta");

        var comUsuario = Parar(() => hasher.Verify("qualquer", hashReal));
        var semUsuario = Parar(() => hasher.VerifyOrDummy("qualquer", null));

        var razao = semUsuario / comUsuario;
        Assert.InRange(razao, 0.2, 5.0);
    }

    private static double Parar(Action acao)
    {
        // Descarta o aquecimento: o primeiro BCrypt paga JIT.
        acao();

        var inicio = System.Diagnostics.Stopwatch.StartNew();
        acao();
        inicio.Stop();

        return inicio.Elapsed.TotalMilliseconds;
    }
}
