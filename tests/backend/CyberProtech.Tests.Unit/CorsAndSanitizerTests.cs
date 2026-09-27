using API.Infrastructure;
using Infrastructure.Lab;
using Infrastructure.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CyberProtech.Tests.Unit;

/// <summary>
/// CORS: "CORS aberto" é o cenário <c>misconfig</c> do laboratório, então a
/// versão corrigida precisa ser restritiva e destes testes é a rede de proteção.
/// </summary>
public class CorsSetupTests
{
    [Fact]
    public void Em_development_entram_localhost_e_127_0_0_1()
    {
        // O navegador trata os dois como origens distintas. Se só um entrasse,
        // abrir a interface pelo outro quebraria o CORS.
        var origens = CorsSetup.ResolveOrigins(null, isDevelopment: true);

        Assert.Contains("http://localhost:5173", origens);
        Assert.Contains("http://127.0.0.1:5173", origens);
    }

    [Fact]
    public void Fora_de_development_nao_entra_nenhuma_origem_padrao()
    {
        var origens = CorsSetup.ResolveOrigins(null, isDevelopment: false);

        Assert.Empty(origens);
    }

    [Fact]
    public void A_origem_configurada_e_respeitada()
    {
        var origens = CorsSetup.ResolveOrigins("https://portal.cyberprotech.lab", isDevelopment: false);

        Assert.Equal(["https://portal.cyberprotech.lab"], origens);
    }

    [Theory]
    [InlineData("http://a.test;http://b.test")]
    [InlineData("http://a.test,http://b.test")]
    public void Varias_Origens_separadas_por_ponto_e_virgula_sao_aceitas(string configurado)
    {
        var origens = CorsSetup.ResolveOrigins(configurado, isDevelopment: false);

        Assert.Contains("http://a.test", origens);
        Assert.Contains("http://b.test", origens);
    }

    [Fact]
    public void Duplicatas_sao_removidas()
    {
        var origens = CorsSetup.ResolveOrigins(
            "http://localhost:5173;http://LOCALHOST:5173;http://outra.test",
            isDevelopment: true);

        Assert.Equal(origens.Length, origens.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Configuracao_vazia_nao_quebra(string? configurado)
    {
        var origens = CorsSetup.ResolveOrigins(configurado, isDevelopment: false);

        Assert.Empty(origens);
    }

    [Fact]
    public void A_allowlist_de_metodos_nao_inclui_verbos_que_a_api_nao_tem()
    {
        Assert.Contains("GET", CorsSetup.AllowedMethods);
        Assert.Contains("POST", CorsSetup.AllowedMethods);
        Assert.Contains("PUT", CorsSetup.AllowedMethods);
        Assert.Contains("DELETE", CorsSetup.AllowedMethods);

        Assert.DoesNotContain("PATCH", CorsSetup.AllowedMethods);
        Assert.DoesNotContain("OPTIONS", CorsSetup.AllowedMethods);
        Assert.DoesNotContain("TRACE", CorsSetup.AllowedMethods);
    }

    [Fact]
    public void A_allowlist_de_headers_tem_Authorization_e_nao_qualquer_coisa()
    {
        Assert.Contains("Authorization", CorsSetup.AllowedHeaders);
        Assert.Contains("Content-Type", CorsSetup.AllowedHeaders);
        Assert.Contains("Accept", CorsSetup.AllowedHeaders);
    }
}

/// <summary>
/// Sanitização da bio. Só roda no estado corrigido; no vulnerável o texto
/// passa intacto de propósito, para o frontend ter payload de verdade.
/// </summary>
public class BioSanitizerTests
{
    private static BioSanitizer Criar(bool vulnMode)
    {
        var lab = CriarLabState(vulnMode);

        return new BioSanitizer(lab);
    }

    private static LabState CriarLabState(bool vulnMode)
    {
        var options = new LabOptions { Enabled = true, VulnMode = vulnMode };
        var environment = new FakeEnvironment { EnvironmentName = "Development" };

        return new LabState(Options.Create(options), environment, NullLogger<LabState>.Instance);
    }

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    [Fact]
    public void No_modo_corrigido_remove_a_tag_script()
    {
        var resultado = Criar(vulnMode: false).Sanitize("<script>alert(1)</script>");

        Assert.DoesNotContain("<script>", resultado, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("</script>", resultado, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void No_modo_corrigido_remove_manipulador_de_evento()
    {
        var resultado = Criar(vulnMode: false).Sanitize("<img src=x onerror=alert(1)>");

        Assert.DoesNotContain("onerror", resultado, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", resultado, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<b>oi</b>")]
    [InlineData("<svg/onload=alert(1)>")]
    [InlineData("<iframe src=javascript:alert(1)>")]
    public void No_modo_corrigido_nao_sobra_marcacao(string payload)
    {
        var resultado = Criar(vulnMode: false).Sanitize(payload);

        Assert.DoesNotContain("<", resultado);
        Assert.DoesNotContain(">", resultado);
    }

    [Fact]
    public void No_modo_vulneravel_o_payload_sobrevive_inteiro()
    {
        const string payload = "<script>alert(document.cookie)</script>";

        var resultado = Criar(vulnMode: true).Sanitize(payload);

        // É o que dá munição ao cenário de XSS do frontend.
        Assert.Equal(payload, resultado);
    }

    [Fact]
    public void Texto_comum_sobrevive_nos_dois_estados()
    {
        const string bio = "Analista de suporte. Gosta de café e de logs bem escritos.";

        Assert.Equal(bio, Criar(vulnMode: false).Sanitize(bio));
        Assert.Equal(bio, Criar(vulnMode: true).Sanitize(bio));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Entradas_vazias_viram_texto_vazio(string? bio)
    {
        Assert.Equal(string.Empty, Criar(vulnMode: false).Sanitize(bio));
    }

    [Fact]
    public void Nao_escapa_entidades_e_apos_remover_marcacao()
    {
        // Só para deixar explícito o limite do sanitizador: ele NÃO é um
        // Codificador de HTML. A proteção de verdade é escapar na renderização.
        var resultado = Criar(vulnMode: false).Sanitize("&lt;script&gt;");

        Assert.DoesNotContain("&lt;", resultado);
    }
}
