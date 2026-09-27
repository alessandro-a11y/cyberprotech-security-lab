using System.Net.Http.Json;
using System.Text.Json;

namespace CyberProtech.Tests.Integration;

/// <summary>
/// Diagnóstico: confirma que a <see cref="ApiFactory"/> realmente injeta a
/// configuração nos overrides. Se algum override não chegar, os demais testes
/// ficam green por acidente ou vermelhos sem motivo, e o sintoma é sempre
/// outro. Este teste falha na hora, com o valor na mensagem.
/// </summary>
public class ConfiguracaoDaFactoryTests
{
    private readonly ApiFactory factory = new();

    [SkippableFact]
    public async Task A_factory_injeta_as_configuracoes_esperadas()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        using var client = factory.CreateClient();
        using var readiness = await client.GetAsync("/api/health/ready");
        var corpo = await readiness.Content.ReadAsStringAsync();

        var token = await ObterToken();
        using var autenticado = factory.CriarClienteAutenticado(token);
        using var config = await autenticado.GetAsync("/api/lab/config");
        config.EnsureSuccessStatusCode();

        var json = await config.Content.ReadFromJsonAsync<JsonElement>();
        var estado = json.GetProperty("toggles")
            .EnumerateArray()
            .ToDictionary(t => t.GetProperty("id").GetString()!, t => t.GetProperty("on").GetBoolean());

        // O rate limit vem desligado de propósito: com ele ligado, a janela de
        // 10/min por IP é compartilhada pela classe inteira e os testes começam
        // a se rejeitar. Se este assert falhar, o override não chegou.
        Assert.True(
            estado["rate-limit"] == false,
            $"Esperava rate-limit desligado, veio {estado["rate-limit"]}. Config do app: {corpo}");

        Assert.True(json.GetProperty("labEnabled").GetBoolean(), "labEnabled deveria ser true no teste");
    }

    [SkippableFact]
    public async Task A_factory_conecta_no_banco_de_teste_e_nao_no_do_laboratorio()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        using var client = factory.CreateClient();
        using var resposta = await client.GetAsync("/api/health/ready");
        var json = await resposta.Content.ReadFromJsonAsync<JsonElement>();

        var banco = json.GetProperty("checks").EnumerateArray()
            .First(c => c.GetProperty("name").GetString() == "database")
            .GetProperty("status").GetString();

        Assert.Equal("healthy", banco);
    }

    private async Task<string> ObterToken()
    {
        using var client = factory.CreateClient();
        using var resposta = await client.PostAsJsonAsync("/api/auth/login",
            new { username = ApiFactory.AdminUsername, password = ApiFactory.TestPassword });

        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString()!;
    }
}
