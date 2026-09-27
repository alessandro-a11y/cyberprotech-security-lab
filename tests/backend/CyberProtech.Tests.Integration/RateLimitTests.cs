using System.Net;
using System.Net.Http.Json;

namespace CyberProtech.Tests.Integration;

/// <summary>
/// Rate limit de login, nos dois estados.
/// </summary>
/// <remarks>
/// Esta classe tem a <b>própria</b> <see cref="ApiFactory"/> em vez de
/// <c>IClassFixture</c> compartilhado, e é o motivo dela existir separada.
/// <para>
/// <c>RateLimitPartition</c> memoiza o limitador por chave de partição dentro da
/// instância da aplicação. Um teste que liga o rate limit e esgota a janela
/// deixa o cache quente; se as outras classes compartilhassem a mesma instância,
/// os testes seguintes levariam <c>429</c> sem terem feito nada de errado. Com
/// uma instância só sua, o cache morre junto com o teste.
/// </para>
/// </remarks>
public class RateLimitTests
{
    private readonly ApiFactory factory = new();

    public async Task DisposeAsync() => await factory.RestaurarEstadoCorrigidoAsync();

    [SkippableFact]
    public async Task Com_o_flag_ligado_o_login_bloqueia_depois_de_dez_tentativas()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        var token = await factory.ObterTokenAsync(ApiFactory.AdminUsername);
        Skip.If(token is null, "Login não disponível");

        using var ligado = await factory.AlterarLaboratorioAsync(token!, new { rateLimit = true });

        using var client = factory.CreateClient();
        var status = new List<HttpStatusCode>();

        for (var i = 0; i < 14; i++)
        {
            using var tentativa = await client.PostAsJsonAsync("/api/auth/login",
                new { username = "admin", password = "senha-errada" });
            status.Add(tentativa.StatusCode);
        }

        Assert.Equal(10, status.Count(s => s == HttpStatusCode.Unauthorized));
        Assert.Equal(4, status.Count(s => s == HttpStatusCode.TooManyRequests));
    }

    [SkippableFact]
    public async Task Com_o_flag_desligado_o_login_aceita_tentativas_sem_limite()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        var token = await factory.ObterTokenAsync(ApiFactory.AdminUsername);
        Skip.If(token is null, "Login não disponível");

        using var desligado = await factory.AlterarLaboratorioAsync(token!, new { rateLimit = false });

        using var client = factory.CreateClient();
        var status = new List<HttpStatusCode>();

        for (var i = 0; i < 14; i++)
        {
            using var tentativa = await client.PostAsJsonAsync("/api/auth/login",
                new { username = "admin", password = "senha-errada" });
            status.Add(tentativa.StatusCode);
        }

        // Este é o cenário de falhas de autenticação do laboratório.
        Assert.Equal(14, status.Count(s => s == HttpStatusCode.Unauthorized));
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, status);
    }

    [SkippableFact]
    public async Task O_login_continua_valendo_no_estado_desligado()
    {
        Skip.IfNot(ApiFactory.DatabaseDisponivel(), "PostgreSQL indisponível");

        var token = await factory.ObterTokenAsync(ApiFactory.AdminUsername);
        Skip.If(token is null, "Login não disponível");

        using var desligado = await factory.AlterarLaboratorioAsync(token!, new { rateLimit = false });

        using var client = factory.CreateClient();
        using var resposta = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = ApiFactory.TestPassword });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }
}
