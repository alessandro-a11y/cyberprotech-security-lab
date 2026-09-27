using Infrastructure.Lab;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CyberProtech.Tests.Unit;

/// <summary>
/// A dupla trava do laboratório. É o teste mais importante desta suíte: se
/// alguma vez deixar de valer, um deploy em produção poderia abrir o modo
/// vulnerável com uma chamada de API.
/// </summary>
public class LabStateTests
{
    private static LabState Criar(bool enabled, bool isDevelopment, Action<LabOptions>? configurar = null)
    {
        var options = new LabOptions { Enabled = enabled };
        configurar?.Invoke(options);

        var environment = new FakeEnvironment { EnvironmentName = isDevelopment ? "Development" : "Production" };

        return new LabState(
            Options.Create(options),
            environment,
            NullLogger<LabState>.Instance);
    }

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    [Fact]
    public void Fora_de_development_os_controles_nao_sao_gravaveis_mesmo_com_enabled()
    {
        var lab = Criar(enabled: true, isDevelopment: false);

        Assert.True(lab.Enabled);
        Assert.False(lab.Writable);
    }

    [Fact]
    public void Em_development_com_enabled_os_controles_sao_gravaveis()
    {
        var lab = Criar(enabled: true, isDevelopment: true);

        Assert.True(lab.Writable);
    }

    [Fact]
    public void Com_enabled_falso_nunca_e_gravavel()
    {
        var lab = Criar(enabled: false, isDevelopment: true);

        Assert.False(lab.Writable);
    }

    [Fact]
    public void Os_estados_iniciais_vem_da_configuracao()
    {
        var lab = Criar(enabled: true, isDevelopment: true, o =>
        {
            o.VulnMode = false;
            o.RateLimit = true;
            o.SecurityHeaders = true;
        });

        Assert.False(lab.Get(LabToggleId.VulnMode));
        Assert.True(lab.Get(LabToggleId.RateLimit));
        Assert.True(lab.Get(LabToggleId.SecurityHeaders));
    }

    [Fact]
    public void Apply_altera_apenas_os_campos_enviados()
    {
        var lab = Criar(enabled: true, isDevelopment: true);

        lab.Apply(new Application.DTOs.LabConfigUpdateRequest { VerboseErrors = true });

        Assert.True(lab.Get(LabToggleId.VerboseErrors));
        // Os que não vieram no corpo continuam como estavam.
        Assert.False(lab.Get(LabToggleId.VulnMode));
        Assert.True(lab.Get(LabToggleId.RateLimit));
    }

    [Fact]
    public void Apply_pode_desligar_tambem()
    {
        var lab = Criar(enabled: true, isDevelopment: true, o => o.RateLimit = true);

        lab.Apply(new Application.DTOs.LabConfigUpdateRequest { RateLimit = false });

        Assert.False(lab.Get(LabToggleId.RateLimit));
    }

    [Fact]
    public void Os_quatro_toggles_usam_os_ids_do_frontend()
    {
        var lab = Criar(enabled: true, isDevelopment: true);

        var ids = lab.GetToggles().Select(t => t.Id).ToArray();

        // Estes ids precisam bater com frontend/src/pages/Admin.jsx.
        Assert.Equal(["vuln-mode", "verbose-errors", "rate-limit", "sec-headers"], ids);
    }

    [Fact]
    public void Todo_toggle_tem_rotulo_e_dica_para_a_interface()
    {
        var lab = Criar(enabled: true, isDevelopment: true);

        Assert.All(lab.GetToggles(), toggle =>
        {
            Assert.False(string.IsNullOrWhiteSpace(toggle.Label));
            Assert.False(string.IsNullOrWhiteSpace(toggle.Hint));
        });
    }

    [Fact]
    public void O_estado_padrao_e_o_corrigido()
    {
        // Um LabOptions recém-criado não pode deixar nada ligado por acidente.
        var lab = Criar(enabled: true, isDevelopment: true);

        Assert.False(lab.Get(LabToggleId.VulnMode));
        Assert.False(lab.Get(LabToggleId.VerboseErrors));
        Assert.True(lab.Get(LabToggleId.RateLimit));
        Assert.True(lab.Get(LabToggleId.SecurityHeaders));
    }
}

/// <summary>
/// Contrato do DTO de atualização: corpo vazio precisa ser detectável, senão um
/// `PUT {}` vira um no-op silencioso e a tela não sabe que algo deu errado.
/// </summary>
public class LabConfigUpdateRequestTests
{
    [Fact]
    public void Corpo_vazio_nao_tem_mudanca()
    {
        Assert.False(new Application.DTOs.LabConfigUpdateRequest().HasAnyChange);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Qualquer_campo_presente_conta_como_mudanca(bool valor)
    {
        Assert.True(new Application.DTOs.LabConfigUpdateRequest { VulnMode = valor }.HasAnyChange);
        Assert.True(new Application.DTOs.LabConfigUpdateRequest { VerboseErrors = valor }.HasAnyChange);
        Assert.True(new Application.DTOs.LabConfigUpdateRequest { RateLimit = valor }.HasAnyChange);
        Assert.True(new Application.DTOs.LabConfigUpdateRequest { SecurityHeaders = valor }.HasAnyChange);
    }

    [Fact]
    public void Falso_explicito_tambem_conta_como_mudanca()
    {
        // Desligar um toggle é uma intenção, não ausência de intenção.
        var request = new Application.DTOs.LabConfigUpdateRequest { RateLimit = false };

        Assert.True(request.HasAnyChange);
    }
}
