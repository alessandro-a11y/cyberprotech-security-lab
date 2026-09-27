using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CyberProtech.Tests.Integration;

/// <summary>
/// Os cenários de vulnerabilidade, verificados nos DOIS estados.
/// </summary>
/// <remarks>
/// Esta é a suíte que garante o que o laboratório promete: cada cenário
/// realmente abre quando o controle é ligado, e realmente fecha quando é
/// desligado. Um toggle que não fizesse nada passaria em metade dos testes —
/// por isso cada caso vem em par.
/// </remarks>
public class CenariosLaboratorioTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ApiFactory factory;
    private string tokenAdmin = string.Empty;
    private string tokenAluno = string.Empty;

    private const string AdminId = "3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e01";
    private const string AlunoId = "3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e02";

    public CenariosLaboratorioTests(ApiFactory factory) => this.factory = factory;

    public async Task InitializeAsync()
    {
        // Ver o comentário em AutenticacaoTests: o fixture não pode estourar
        // antes do Skip.IfNot de cada teste.
        if (!ApiFactory.DatabaseDisponivel())
        {
            return;
        }

        await factory.RestaurarEstadoCorrigidoAsync();
        tokenAdmin = await factory.ObterTokenAsync(ApiFactory.AdminUsername) ?? string.Empty;
        tokenAluno = await factory.ObterTokenAsync("aluno01") ?? string.Empty;
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrEmpty(tokenAdmin))
        {
            await factory.RestaurarEstadoCorrigidoAsync();
        }
    }

    /// <summary>
    /// Liga os controles indicados e descarta a resposta. Cada teste faz as
    /// próprias asserções no que a API devolveu.
    /// </summary>
    private async Task LigarAsync(object controles)
    {
        using var resposta = await factory.AlterarLaboratorioAsync(tokenAdmin, controles);
        resposta.EnsureSuccessStatusCode();
    }

    private async Task LigarVulnModeAsync(bool vulnMode) => await LigarAsync(new { vulnMode });

    private static async Task<JsonElement[]> LerUsuariosAsync(HttpClient client, string query = "")
    {
        var url = string.IsNullOrEmpty(query) ? "/api/users?pageSize=100" : $"/api/users?pageSize=100&{query}";
        var corpo = await client.GetFromJsonAsync<JsonElement>(url);
        return corpo.EnumerateArray().ToArray();
    }

    // ---------------------------------------------------------------- SQLi

    [SkippableFact]
    public async Task SQLi_com_o_flag_desligado_nao_vaza_nada()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");
        Skip.If(string.IsNullOrEmpty(tokenAdmin), "Login não disponível");

        await LigarAsync(new { vulnMode = false });

        // Falhar aqui, com o estado à vista, é melhor do que descobrir depois
        // que o controle não teve efeito.
        await AssertLabStateAsync("vuln-mode", false);

        using var client = factory.CriarClienteAutenticado(tokenAdmin);

        var usuarios = await LerUsuariosAsync(client, "search=' OR '1'='1");

        Assert.Empty(usuarios);
    }

    /// <summary>
    /// Lê o estado real de um toggle e falha mostrando o JSON inteiro, para
    /// diagnóstico de cenários que não abrem nem fecham.
    /// </summary>
    private async Task AssertLabStateAsync(string toggle, bool esperado)
    {
        using var client = factory.CriarClienteAutenticado(tokenAdmin);
        using var resposta = await client.GetAsync("/api/lab/config");
        resposta.EnsureSuccessStatusCode();

        var json = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        var atual = json.GetProperty("toggles")
            .EnumerateArray()
            .First(t => t.GetProperty("id").GetString() == toggle)
            .GetProperty("on").GetBoolean();

        Assert.True(
            atual == esperado,
            $"Esperava {toggle}={esperado}, veio {atual}. Estado: {await resposta.Content.ReadAsStringAsync()}");
    }

    [SkippableFact]
    public async Task SQLi_com_o_flag_ligado_devolve_a_tabela_inteira()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");
        Skip.If(string.IsNullOrEmpty(tokenAdmin), "Login não disponível");

        await LigarAsync(new { vulnMode = true });
        await AssertLabStateAsync("vuln-mode", true);
        using var client = factory.CriarClienteAutenticado(tokenAdmin);

        var comPayload = await LerUsuariosAsync(client, "search=' OR '1'='1");
        var semPayload = await LerUsuariosAsync(client);

        // O clássico: a tautologia devolve a tabela toda, não só quem casa.
        Assert.NotEmpty(comPayload);
        Assert.Equal(semPayload.Length, comPayload.Length);
    }

    [SkippableFact]
    public async Task SQLi_busca_legitima_continua_funcionando_nos_dois_estados()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");
        Skip.If(string.IsNullOrEmpty(tokenAdmin), "Login não disponível");

        foreach (var vulnMode in new[] { false, true })
        {
            await LigarAsync(new { vulnMode });
            using var client = factory.CriarClienteAutenticado(tokenAdmin);

            var usuarios = await LerUsuariosAsync(client, "search=admin");

            Assert.NotEmpty(usuarios);
            Assert.Contains(usuarios, u => u.GetProperty("username").GetString() == "admin");
        }
    }

    // --------------------------------------------------------------- IDOR

    [SkippableFact]
    public async Task IDOR_com_o_flag_desligado_bloqueia_o_acesso_a_terceiro()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");
        Skip.If(string.IsNullOrEmpty(tokenAluno), "Login não disponível");

        await LigarAsync(new { vulnMode = false });
        using var client = factory.CriarClienteAutenticado(tokenAluno);

        using var leitura = await client.GetAsync($"/api/users/{AdminId}");

        Assert.Equal(HttpStatusCode.Forbidden, leitura.StatusCode);
    }

    [SkippableFact]
    public async Task IDOR_com_o_flag_ligado_deixa_ler_o_dado_de_terceiro()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");
        Skip.If(string.IsNullOrEmpty(tokenAluno), "Login não disponível");

        await LigarAsync(new { vulnMode = true });
        using var client = factory.CriarClienteAutenticado(tokenAluno);

        using var leitura = await client.GetAsync($"/api/users/{AdminId}");

        Assert.Equal(HttpStatusCode.OK, leitura.StatusCode);

        var corpo = await leitura.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("admin@cyberprotech.lab", corpo.GetProperty("email").GetString());
    }

    [SkippableFact]
    public async Task IDOR_nao_afeta_o_acesso_ao_proprio_perfil()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");
        Skip.If(string.IsNullOrEmpty(tokenAluno), "Login não disponível");

        foreach (var vulnMode in new[] { false, true })
        {
            await LigarVulnModeAsync(vulnMode);
            using var client = factory.CriarClienteAutenticado(tokenAluno);

            using var leitura = await client.GetAsync($"/api/users/{AlunoId}");

            // Este endpoint não é cenário: 200 nos dois estados.
            Assert.Equal(HttpStatusCode.OK, leitura.StatusCode);
        }
    }


    // ---------------------------------------------------------------- XSS

    [SkippableTheory]
    [InlineData(false, false)] // corrigido: a tag some
    [InlineData(true, true)]   // vulnerável: o payload sobrevive
    public async Task XSS_a_bio_depende_do_flag(bool vulnMode, bool deveSobreviver)
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");
        Skip.If(string.IsNullOrEmpty(tokenAluno), "Login não disponível");

        await LigarAsync(new { vulnMode });
        using var client = factory.CriarClienteAutenticado(tokenAluno);

        const string payload = "<script>alert(1)</script>";

        using var gravacao = await client.PutAsJsonAsync("/api/auth/me", new
        {
            email = "aluno01@cyberprotech.lab",
            bio = payload,
        });

        Assert.Equal(HttpStatusCode.OK, gravacao.StatusCode);

        var corpo = await gravacao.Content.ReadFromJsonAsync<JsonElement>();
        var bio = corpo.GetProperty("user").GetProperty("bio").GetString() ?? string.Empty;

        Assert.Equal(deveSobreviver, bio.Contains("<script>", StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public async Task XSS_manipulador_de_evento_tambem_e_neutralizado_no_estado_corrigido()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");
        Skip.If(string.IsNullOrEmpty(tokenAluno), "Login não disponível");

        await LigarAsync(new { vulnMode = false });
        using var client = factory.CriarClienteAutenticado(tokenAluno);

        using var gravacao = await client.PutAsJsonAsync("/api/auth/me", new
        {
            email = "aluno01@cyberprotech.lab",
            bio = "<img src=x onerror=alert(1)>",
        });

        var corpo = await gravacao.Content.ReadFromJsonAsync<JsonElement>();
        var bio = corpo.GetProperty("user").GetProperty("bio").GetString() ?? string.Empty;

        Assert.DoesNotContain("onerror", bio, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------- area administrativa

    [SkippableFact]
    public async Task A_area_administrativa_nao_tem_versao_vulneravel()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");
        Skip.If(string.IsNullOrEmpty(tokenAluno), "Login não disponível");

        foreach (var vulnMode in new[] { false, true })
        {
            await LigarVulnModeAsync(vulnMode);
            using var client = factory.CriarClienteAutenticado(tokenAluno);

            using var stats = await client.GetAsync("/api/admin/stats");

            // Importante: o IDOR é na leitura de usuário, não na área admin.
            Assert.Equal(HttpStatusCode.Forbidden, stats.StatusCode);
        }
    }

    [SkippableFact]
    public async Task Troca_de_papel_com_valor_fora_da_allowlist_e_400()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");
        Skip.If(string.IsNullOrEmpty(tokenAdmin), "Login não disponível");

        using var client = factory.CriarClienteAutenticado(tokenAdmin);

        using var resposta = await client.PutAsJsonAsync($"/api/admin/users/{AlunoId}/role", new { role = "SuperAdmin" });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [SkippableFact]
    public async Task Admin_nao_altera_o_proprio_papel()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");
        Skip.If(string.IsNullOrEmpty(tokenAdmin), "Login não disponível");

        using var client = factory.CriarClienteAutenticado(tokenAdmin);

        using var resposta = await client.PutAsJsonAsync($"/api/admin/users/{AdminId}/role", new { role = "User" });

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    // ------------------------------------------------------------ validações

    [SkippableTheory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task Parametros_fora_da_faixa_sao_400_na_versao_corrigida(string query)
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");
        Skip.If(string.IsNullOrEmpty(tokenAdmin), "Login não disponível");

        await LigarAsync(new { vulnMode = false });
        using var client = factory.CriarClienteAutenticado(tokenAdmin);

        using var leitura = await client.GetAsync($"/api/users?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, leitura.StatusCode);
    }

    [SkippableFact]
    public async Task Filtro_por_papel_e_exato_e_com_caixa()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");
        Skip.If(string.IsNullOrEmpty(tokenAdmin), "Login não disponível");

        using var client = factory.CriarClienteAutenticado(tokenAdmin);

        var comMaiuscula = await LerUsuariosAsync(client, "role=Admin");
        var comMinuscula = await LerUsuariosAsync(client, "role=admin");

        Assert.NotEmpty(comMaiuscula);
        Assert.All(comMaiuscula, u => Assert.Equal("Admin", u.GetProperty("role").GetString()));

        // "admin" minúsculo não casa com a coluna, que guarda "Admin".
        Assert.Empty(comMinuscula);
    }

    // ------------------------------------------------------------- health

    [SkippableFact]
    public async Task Health_e_health_ready_respondem_ok()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        using var client = factory.CreateClient();

        using var liveness = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);

        using var readiness = await client.GetAsync("/api/health/ready");
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
    }
}
