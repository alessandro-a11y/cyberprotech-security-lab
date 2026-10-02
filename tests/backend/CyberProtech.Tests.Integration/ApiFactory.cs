using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CyberProtech.Tests.Integration;

/// <summary>
/// Sobe a API real em memória, contra um PostgreSQL de verdade.
/// </summary>
/// <remarks>
/// <para>
/// A connection string vem de <c>TEST_CONNECTION_STRING</c>. Sem ela, a suíte
/// assume um banco descartável e usa o nome do compose
/// (<c>cyberprotech_test</c>), que a CI cria como serviço. A biblioteca é
/// sempre a mesma: assim o SQL do cenário de injeção é o SQL real do
/// PostgreSQL, e não uma emulação.
/// </para>
/// <para>
/// Quando não há banco disponível, os testes <b>não</b> são pulados com uma
/// mensagem explícita. Falhar por falta de infraestrutura esconderia regressão
/// de verdade; pular com aviso mantém o sinal limpo.
/// </para>
/// </remarks>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string ConnectionStringVariable = "TEST_CONNECTION_STRING";
    public const string FallbackConnectionString =
        "Host=localhost;Port=5432;Database=cyberprotech_test;Username=postgres;Password=changeme";

    /// <summary>Conta de teste com papel Admin, criada por <see cref="EnsureDatabase"/>.</summary>
    public const string AdminUsername = "admin";

    public const string TestPassword = "CyberProtech@2026";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable) is { Length: > 0 } valor
            ? valor
            : FallbackConnectionString;

    /// <summary>
    /// Verifica se há banco. Chamado no construtor de cada classe de teste para
    /// pular com aviso em vez de estourar por falta de infraestrutura.
    /// </summary>
    public static bool DatabaseDisponivel()
    {
        try
        {
            using var connection = new Npgsql.NpgsqlConnection(ConnectionString);
            connection.Open();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[SKIP] PostgreSQL indisponível em {Redigir(ConnectionString)}: {ex.GetType().Name}. " +
                $"Defina {ConnectionStringVariable} para rodar os testes de integração.");
            return false;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting, e não ConfigureAppConfiguration: ele escreve na
        // configuração do host, que tem precedência sobre o appsettings.json.
        // Com ConfigureAppConfiguration o Program.cs chegava a ler a conexão do
        // appsettings (Port=5432) e o teste batia no PostgreSQL errado da
        // máquina.
        var ajustes = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ConnectionString,
            // Uma chave fixa por execução: os testes de token precisam que
            // assinar e validar concordem, e não que a chave seja secreta.
            ["Jwt:SigningKey"] = "chave-de-teste-com-32-bytes-exatamente-000",
            ["Jwt:Issuer"] = "cyberprotech-api",
            ["Jwt:Audience"] = "cyberprotech-web",
            ["Database:Seed"] = "true",
            ["Seed:Password"] = TestPassword,
            ["Database:MigrateOnStartup"] = "true",
            ["Lab:Enabled"] = "true",
            // Rate limit DESLIGADO por padrão nos testes. Com ele ligado, os
            // testes compartilham a mesma janela de 10/min por IP e começam a
            // pular uns aos outros — interferência entre casos, não cobertura.
            // O comportamento do rate limit tem um teste dedicado.
            ["Lab:RateLimit"] = "false",
            ["Frontend:BaseUrl"] = "http://localhost:5173",
        };

        foreach (var (chave, valor) in ajustes)
        {
            builder.UseSetting(chave, valor);
        }

        // Também por variável de ambiente, para o Program.cs e qualquer código
        // que leia IConfiguration diretamente enxergarem o mesmo valor.
        foreach (var (chave, valor) in ajustes)
        {
            Environment.SetEnvironmentVariable(chave, valor);
        }
    }

    /// <summary>
    /// Login e devolve o token, ou <c>null</c> se as credenciais não servirem.
    /// </summary>
    /// <remarks>
    /// O resultado é cacheado por usuário. Sem o cache, um <c>IAsyncLifetime</c>
    /// que autentica antes de cada teste somaria dezenas de logins por classe
    /// e estouraria a janela de rate limit — os testes passariam a falhar com
    /// <c>429</c> por conta própria, que é ruído, não cobertura.
    /// <c>RateLimitTests</c> faz seus logins direto por <c>HttpClient</c>, fora
    /// deste cache, justamente porque precisa contar as tentativas.
    /// </remarks>
    public async Task<string?> ObterTokenAsync(string usuario, string senha = TestPassword)
    {
        var chave = $"{usuario}:{senha}";

        if (_tokens.TryGetValue(chave, out var existente))
        {
            return existente;
        }

        using var client = CreateClient();
        using var resposta = await client.PostAsJsonAsync("/api/auth/login", new { username = usuario, password = senha });

        if (!resposta.IsSuccessStatusCode)
        {
            _tokens[chave] = null;
            return null;
        }

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        var token = corpo.GetProperty("token").GetString();

        _tokens[chave] = token;
        return token;
    }

    private readonly Dictionary<string, string?> _tokens = new(StringComparer.Ordinal);

    /// <summary>
    /// Cliente já com o header <c>Authorization</c> preenchido.
    /// </summary>
    public HttpClient CriarClienteAutenticado(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Vira um controle do laboratório. Devolve o corpo da resposta para o teste
    /// conferir o que a API respondeu.
    /// </summary>
    public async Task<HttpResponseMessage> AlterarLaboratorioAsync(string token, object controles)
    {
        var client = CriarClienteAutenticado(token);
        return await client.PutAsJsonAsync("/api/lab/config", controles);
    }

    /// <summary>
    /// Coloca a API no estado corrigido. Chamar no <c>Dispose</c> evita que uma
    /// classe deixe o banco com o modo vulnerável ligado para a próxima.
    /// </summary>
    public async Task RestaurarEstadoCorrigidoAsync()
    {
        var token = await ObterTokenAsync(AdminUsername);
        if (token is null)
        {
            return;
        }

        using var resposta = await AlterarLaboratorioAsync(token, new
        {
            vulnMode = false,
            verboseErrors = false,
            rateLimit = true,
            securityHeaders = true,
        });

        resposta.Dispose();
    }

    private static string Redigir(string connectionString) =>
        string.Join(';', connectionString.Split(';')
            .Select(parte => parte.TrimStart().StartsWith("Password", StringComparison.OrdinalIgnoreCase)
                ? "Password=***"
                : parte));
}
