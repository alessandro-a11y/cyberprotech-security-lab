using System.IdentityModel.Tokens.Jwt;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace CyberProtech.Tests.Unit;

/// <summary>
/// Emissão do token. O que importa é que as claims usadas na autorização estejam
/// estejam presentes e que o papel viaje dentro do token — é o que a policy
/// <c>AdminOnly</c> lê.
/// </summary>
public class JwtTokenServiceTests
{
    private const string Key = "chave-de-teste-com-tamanho-suficiente-123456";

    private static JwtTokenService Criar(int expiracaoMinutos = 60)
    {
        var settings = Options.Create(new JwtSettings
        {
            Issuer = "cyberprotech-api",
            Audience = "cyberprotech-web",
            SigningKey = Key,
            ExpirationMinutes = expiracaoMinutos,
        });

        return new JwtTokenService(settings);
    }

    private static User NovoUsuario(string papel = "User") => new()
    {
        Id = Guid.Parse("3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e02"),
        Username = "aluno01",
        Email = "aluno01@cyberprotech.lab",
        Role = papel,
    };

    [Fact]
    public void O_token_traz_sub_como_string()
    {
        var (token, _) = Criar().CreateToken(NovoUsuario());

        var sub = LerClaim(token, JwtRegisteredClaimNames.Sub);

        // GUID cru não serializa: o JwtSecurityTokenHandler rejeitaria.
        Assert.Equal("3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e02", sub);
    }

    [Fact]
    public void O_token_traz_o_papel_que_a_policy_AdminOnly_le()
    {
        var (token, _) = Criar().CreateToken(NovoUsuario("Admin"));

        var role = LerClaim(token, "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");

        Assert.Equal("Admin", role);
    }

    [Fact]
    public void O_token_traz_usuario_e_email()
    {
        var (token, _) = Criar().CreateToken(NovoUsuario());

        Assert.Equal("aluno01", LerClaim(token, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"));
        Assert.Equal("aluno01@cyberprotech.lab", LerClaim(token, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"));
    }

    [Fact]
    public void O_token_traz_issuer_audience_e_validade_configurados()
    {
        var (token, expiraEm) = Criar(expiracaoMinutos: 30).CreateToken(NovoUsuario());

        var jwt = LerJwt(token);

        Assert.Equal("cyberprotech-api", jwt.Issuer);
        Assert.Contains("cyberprotech-web", jwt.Audiences);
        Assert.Equal(expiraEm, jwt.ValidTo, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void A_expiracao_respeita_a_configuracao()
    {
        var antes = DateTime.UtcNow;
        var (_, expiraEm) = Criar(expiracaoMinutos: 15).CreateToken(NovoUsuario());

        Assert.InRange(expiraEm, antes.AddMinutes(15).AddSeconds(-5), antes.AddMinutes(15).AddSeconds(5));
    }

    [Fact]
    public void Tokens_de_usuarios_diferentes_tem_assinaturas_diferentes()
    {
        var service = Criar();

        var (primeiro, _) = service.CreateToken(NovoUsuario("User"));
        var (segundo, _) = service.CreateToken(NovoUsuario("Admin"));

        Assert.NotEqual(primeiro, segundo);
    }

    [Theory]
    [InlineData("curta")]          // 5 caracteres: abaixo do mínimo da biblioteca
    [InlineData("123456789012345")] // 15 caracteres: 120 bits, ainda abaixo de 128
    public void Uma_chave_menor_que_o_minimo_da_biblioteca_falha_na_assinatura(string chave)
    {
        // A biblioteca de token exige no mínimo 128 bits (16 caracteres) e
        // lança ao assinar. O construtor sozinho não valida nada.
        var settings = Options.Create(new JwtSettings { SigningKey = chave });
        var service = new JwtTokenService(settings);

        Assert.ThrowsAny<Exception>(() => service.CreateToken(NovoUsuario()));
    }

    [Fact]
    public void O_minimo_real_da_biblioteca_e_32_bytes()
    {
        // Medido empiricamente contra a biblioteca: 31 bytes falha, 32 passa.
        // A mensagem de erro cita "maior que 256 bits", o que sugeriria 33, mas
        // o comportamento real é >= 32 bytes.
        var curto = Options.Create(new JwtSettings { SigningKey = new string('k', 31) });
        var noLimite = Options.Create(new JwtSettings { SigningKey = new string('k', 32) });

        Assert.ThrowsAny<Exception>(() => new JwtTokenService(curto).CreateToken(NovoUsuario()));

        var (token, _) = new JwtTokenService(noLimite).CreateToken(NovoUsuario());
        Assert.False(string.IsNullOrEmpty(token));
    }

    private static JwtSecurityToken LerJwt(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    private static string LerClaim(string token, string claim) =>
        LerJwt(token).Claims.First(c => c.Type == claim).Value;
}
