using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CyberProtech.Load;

/// <summary>
/// Gerador de carga da API, para medir a resposta sob concorrência real.
/// </summary>
/// <remarks>
/// <para>
/// Existe porque a medição sequencial engana: ela nunca faz duas requisições
/// ao mesmo tempo, e o banco do laboratório é justamente o recurso que estraga
/// sob concorrência (pool de conexões, locks, CPU para BCrypt).
/// </para>
/// <para>
/// Cada cenário roda com N workers reais, em paralelo, e coleta a latência de
/// cada requisição. O relatório sai com vazão e percentis, que é o que permite
/// dizer se a API aguenta e a partir de onde ela quebra.
/// </para>
/// </remarks>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var opcoes = Opcoes.Parse(args);

        if (opcoes.Ajuda)
        {
            Opcoes.ImprimirAjuda();
            return 0;
        }

        using var cliente = CriarClient(opcoes.BaseUrl);

        Console.WriteLine($"alvo    {opcoes.BaseUrl}");
        Console.WriteLine($"cenário {opcoes.Cenario}");
        Console.WriteLine($"carga   {opcoes.Concorrencia} workers x {opcoes.DuracaoSegundos}s (rampa {opcoes.RampaSegundos}s, aquecimento {opcoes.AquecimentoSegundos}s)");
        Console.WriteLine();

        // login para os cenários autenticados
        string? token = null;
        if (opcoes.NeedsToken())
        {
            token = await ObterTokenAsync(cliente, opcoes);
            if (token is null)
            {
                Console.Error.WriteLine("ERRO: login falhou. Verifique as credenciais e se a API está no ar.");
                return 1;
            }

            Console.WriteLine("login   ok (token obtido)");
        }

        var cenario = Cenarios.Obter(opcoes.Cenario!);
        await ExecutarAquecimentoAsync(cenario, cliente, opcoes, token);
        Console.WriteLine("aquec   concluido (amostras descartadas)");
        Console.WriteLine();

        var resultado = await ExecutarAsync(cenario, cliente, opcoes, token);

        resultado.Imprimir(opcoes.Cenario!);

        return resultado.TemAlgumProblema ? 2 : 0;
    }

    private static HttpClient CriarClient(string baseUrl)
    {
        var handler = new SocketsHttpHandler
        {
            // Sem limite de conexão: o padrão do SocketsHttpHandler é int.MaxValue,
            // mas ser explícito evita surpresa se o default mudar.
            MaxConnectionsPerServer = int.MaxValue,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            // Timeout alto: o login com BCrypt custo 12 leva ~500ms, e sob
            // concorrência a fila faz o resto. Cortar em 5s mediria o timeout,
            // não a latência.
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    private static async Task<string?> ObterTokenAsync(HttpClient cliente, Opcoes opcoes)
    {
        using var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            username = opcoes.Usuario,
            password = opcoes.Senha,
        });

        if (!resposta.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"ERRO: login devolveu {(int)resposta.StatusCode}.");
            return null;
        }

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return corpo.GetProperty("token").GetString();
    }

    private static async Task ExecutarAquecimentoAsync(
        Cenario cenario, HttpClient cliente, Opcoes opcoes, string? token)
    {
        var fim = DateTime.UtcNow.AddSeconds(opcoes.AquecimentoSegundos);
        while (DateTime.UtcNow < fim)
        {
            await cenario.ExecutarAsync(cliente, opcoes, token);
        }
    }

    private static async Task<Resultado> ExecutarAsync(
        Cenario cenario, HttpClient cliente, Opcoes opcoes, string? token)
    {
        var amostras = new List<Amostra>(10_000);
        var trava = new object();
        var cancelado = new CancellationTokenSource();

        var cronometro = Stopwatch.StartNew();
        var primeiroInicio = DateTime.UtcNow;

        var tasks = Enumerable.Range(0, opcoes.Concorrencia).Select(async worker =>
        {
            // Rampa: cada worker começa um pouco depois, para a carga subir
            // em vez de bater tudo de uma vez (que mediria cold start).
            var atraso = TimeSpan.FromMilliseconds(
                opcoes.Concorrencia == 1 ? 0 : (double)opcoes.RampaSegundos * 1000 * worker / opcoes.Concorrencia);

            if (atraso > TimeSpan.Zero)
            {
                await Task.Delay(atraso, cancelado.Token);
            }

            var local = new List<Amostra>(256);

            while (!cancelado.IsCancellationRequested && cronometro.Elapsed.TotalSeconds < opcoes.DuracaoSegundos)
            {
                var amostra = await cenario.ExecutarAsync(cliente, opcoes, token);
                local.Add(amostra);
            }

            lock (trava)
            {
                amostras.AddRange(local);
            }
        }).ToArray();

        await Task.WhenAll(tasks);
        cronometro.Stop();
        cancelado.Dispose();

        return Resultado.Calcular(amostras, cronometro.Elapsed, primeiroInicio);
    }
}

/// <summary>
/// Uma requisição medida: quanto tempo levou e o que respondeu.
/// </summary>
internal readonly record struct Amostra(double Milissegundos, int Status);

/// <summary>
/// Um cenário: um endpoint, ou um fluxo, medido repetidamente.
/// </summary>
internal sealed class Cenario(string nome, Func<HttpClient, Opcoes, string?, Task<HttpResponseMessage>> requisicao)
{
    public string Nome => nome;

    public async Task<Amostra> ExecutarAsync(HttpClient cliente, Opcoes opcoes, string? token)
    {
        var cronometro = Stopwatch.StartNew();

        try
        {
            using var resposta = await requisicao(cliente, opcoes, token);
            cronometro.Stop();

            return new Amostra(cronometro.Elapsed.TotalMilliseconds, (int)resposta.StatusCode);
        }
        catch (Exception ex)
        {
            cronometro.Stop();

            // Falha de transporte (timeout, conexão recusada) conta como 0:
            // senão o relatório diria que a API respondeu rápido.
            return new Amostra(cronometro.Elapsed.TotalMilliseconds, ex is TaskCanceledException ? 408 : 0);
        }
    }
}

internal static class Cenarios
{
    public static Cenario Obter(string nome) => nome switch
    {
        "users" => Get("/api/users?page=1&pageSize=50"),

        "users-busca" => new Cenario(nome, (c, o, t) => c.SendAsync(RequisicaoGet(
            $"/api/users?search={Uri.EscapeDataString(o.TermoBusca)}&pageSize=50", t))),

        "user-por-id" => Get("/api/users/{o.IdAlvo}"),

        "me" => Get("/api/auth/me"),

        "stats" => Get("/api/admin/stats"),

        "ready" => new Cenario(nome, (c, o, t) => c.GetAsync("/api/health/ready")),

        "health" => new Cenario(nome, (c, o, t) => c.GetAsync("/api/health")),

        // Senha errada de propósito: mede o custo do BCrypt sem gastar tempo
        // gerando token, e não exige usuário válido.
        "login" => new Cenario(nome, (c, o, t) => c.PostAsJsonAsync("/api/auth/login", new
        {
            username = o.Usuario,
            password = "senha-errada-para-benchmark",
        })),

        _ => throw new ArgumentException($"Cenário desconhecido: {nome}. Use --ajuda para ver a lista."),
    };

    public static IReadOnlyList<string> Todos { get; } =
        ["users", "users-busca", "user-por-id", "me", "stats", "ready", "health", "login"];

    private static Cenario Get(string caminho) =>
        new(caminho, (c, o, t) => c.SendAsync(RequisicaoGet(caminho.Replace("{o.IdAlvo}", o.IdAlvo), t)));

    private static HttpRequestMessage RequisicaoGet(string caminho, string? token)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, caminho);

        if (token is not null)
        {
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return requisicao;
    }
}

/// <summary>
/// Agrega as amostras e escreve o relatório.
/// </summary>
internal sealed class Resultado
{
    private Resultado(
        int total,
        int ok,
        int naoAutorizado,
        int forbidden,
        int rateLimited,
        int erroServidor,
        int falhaTransporte,
        double[] latenciasOrdenadas,
        double segundos)
    {
        Total = total;
        Ok = ok;
        NaoAutorizado = naoAutorizado;
        Forbidden = forbidden;
        RateLimited = rateLimited;
        ErroServidor = erroServidor;
        FalhaTransporte = falhaTransporte;
        LatenciasOrdenadas = latenciasOrdenadas;
        Segundos = segundos;
    }

    public int Total { get; }
    public int Ok { get; }
    public int NaoAutorizado { get; }
    public int Forbidden { get; }
    public int RateLimited { get; }
    public int ErroServidor { get; }
    public int FalhaTransporte { get; }
    public double[] LatenciasOrdenadas { get; }
    public double Segundos { get; }

    public double Vazao => Segundos <= 0 ? 0 : Total / Segundos;

    public bool TemAlgumProblema => ErroServidor > 0 || FalhaTransporte > 0;

    public static Resultado Calcular(List<Amostra> amostras, TimeSpan duracao, DateTime _)
    {
        var latencias = amostras.Select(a => a.Milissegundos).OrderBy(x => x).ToArray();

        int Contar(Func<int, bool> filtro) => amostras.Count(a => filtro(a.Status));

        return new Resultado(
            amostras.Count,
            Contar(s => s is >= 200 and < 300),
            Contar(s => s == 401),
            Contar(s => s == 403),
            Contar(s => s == 429),
            Contar(s => s >= 500),
            Contar(s => s == 0),
            latencias,
            Math.Max(duracao.TotalSeconds, 0.001));
    }

    public void Imprimir(string cenario)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {cenario} · {Total} requisições em {Segundos:F1}s ===");
        Console.WriteLine();
        Console.WriteLine($"  vazão          {Vazao,10:F1} req/s");
        Console.WriteLine();

        Console.WriteLine("  latência (ms)");
        Console.WriteLine($"    p50          {Percentil(0.50),10:F1}");
        Console.WriteLine($"    p90          {Percentil(0.90),10:F1}");
        Console.WriteLine($"    p95          {Percentil(0.95),10:F1}");
        Console.WriteLine($"    p99          {Percentil(0.99),10:F1}");
        Console.WriteLine($"    max          {(LatenciasOrdenadas.Length > 0 ? LatenciasOrdenadas[^1] : 0),10:F1}");
        Console.WriteLine();

        Console.WriteLine("  respostas");
        Console.WriteLine($"    2xx          {Ok,10}  ({Percentual(Ok)}%)");
        if (NaoAutorizado > 0) Console.WriteLine($"    401          {NaoAutorizado,10}  ({Percentual(NaoAutorizado)}%)");
        if (Forbidden > 0) Console.WriteLine($"    403          {Forbidden,10}  ({Percentual(Forbidden)}%)");
        if (RateLimited > 0) Console.WriteLine($"    429          {RateLimited,10}  ({Percentual(RateLimited)}%)  <- rate limit do laboratorio");
        if (ErroServidor > 0) Console.WriteLine($"    5xx          {ErroServidor,10}  ({Percentual(ErroServidor)}%)  <- PROBLEMA");
        if (FalhaTransporte > 0) Console.WriteLine($"    falha        {FalhaTransporte,10}  ({Percentual(FalhaTransporte)}%)  <- PROBLEMA");
        Console.WriteLine();

        if (Total == 0)
        {
            Console.WriteLine("  Nenhuma amostra coletada. A API respondeu?");
        }
    }

    private double Percentil(double fracao) =>
        LatenciasOrdenadas.Length == 0
            ? 0
            : LatenciasOrdenadas[Math.Min(LatenciasOrdenadas.Length - 1, (int)(LatenciasOrdenadas.Length * fracao))];

    private string Percentual(int contagem) =>
        Total == 0 ? "0.0" : (contagem * 100.0 / Total).ToString("F1");
}
