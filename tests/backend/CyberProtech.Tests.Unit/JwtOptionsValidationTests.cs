using Infrastructure;
using Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CyberProtech.Tests.Unit;

/// <summary>
/// O binding de opções do JWT é o que impede a API de subir com chave de
/// assinatura curta ou ausente. Testar aqui significa que o guard continua
/// funcionando mesmo que ninguém teste a aplicação inteira.
/// </summary>
public class JwtOptionsValidationTests
{
    private static IHostEnvironment Ambiente(string nome) => new FakeEnvironment { EnvironmentName = nome };

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static ServiceProvider Construir(Dictionary<string, string?> config)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(Ambiente("Production"));
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    private const string ConnectionString = "Host=localhost;Database=x;Username=u;Password=p";

    [Fact]
    public void Chave_de_assinatura_valida_passa_na_validacao()
    {
        using var provider = Construir(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ConnectionString,
            ["Jwt:SigningKey"] = "chave-com-tamanho-suficiente-para-hmac-sha256",
        });

        var settings = provider.GetRequiredService<IOptions<JwtSettings>>().Value;

        Assert.Equal("chave-com-tamanho-suficiente-para-hmac-sha256", settings.SigningKey);
    }

    [Fact]
    public void Chave_ausente_e_recusada()
    {
        using var provider = Construir(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ConnectionString,
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<JwtSettings>>().Value);
    }

    [Theory]
    [InlineData("curta")]
    [InlineData("1234567890123456789012345678901")] // 31 bytes
    public void Chave_com_menos_de_32_bytes_e_recusada(string chave)
    {
        using var provider = Construir(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ConnectionString,
            ["Jwt:SigningKey"] = chave,
        });

        // 31 bytes não chegam a 256 bits e a biblioteca rejeita ao assinar.
        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<JwtSettings>>().Value);
    }

    [Fact]
    public void Exatamente_32_bytes_e_aceito()
    {
        // Medido empiricamente: a biblioteca aceita 32 e rejeita 31. A mensagem
        // de erro dela fala "maior que 256 bits", que sugeriria 33, mas o
        // comportamento real é >= 32.
        using var provider = Construir(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ConnectionString,
            ["Jwt:SigningKey"] = new string('k', 32),
        });

        var settings = provider.GetRequiredService<IOptions<JwtSettings>>().Value;

        Assert.Equal(32, settings.SigningKey.Length);
    }

    [Fact]
    public void A_mediacao_e_em_bytes_e_nao_em_caracteres()
    {
        // 20 caracteres acentuados ocupam 40 bytes em UTF-8. Contar caracteres
        // recusaria uma chave criptograficamente suficiente.
        var chave = string.Concat(Enumerable.Repeat("é", 20));
        Assert.Equal(20, chave.Length);
        Assert.Equal(40, System.Text.Encoding.UTF8.GetByteCount(chave));

        using var provider = Construir(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ConnectionString,
            ["Jwt:SigningKey"] = chave,
        });

        var settings = provider.GetRequiredService<IOptions<JwtSettings>>().Value;

        Assert.Equal(20, settings.SigningKey.Length);
    }

    [Fact]
    public void A_chave_padrao_de_desenvolvimento_e_valida()
    {
        // O valor em appsettings.json precisa passar na própria validação, senão
        // o `docker compose up` do zero não sobe a API.
        var raiz = LocalizarRaizDoRepositorio();
        var caminho = Path.Combine(raiz, "backend", "API", "appsettings.json");

        Assert.True(File.Exists(caminho), $"appsettings.json não encontrado a partir de {raiz}");

        var config = new ConfigurationBuilder().AddJsonFile(caminho).Build();
        var settings = config.GetSection("Jwt").Get<JwtSettings>();

        Assert.NotNull(settings);
        Assert.True(
            System.Text.Encoding.UTF8.GetByteCount(settings!.SigningKey) >= 32,
            "A chave padrão de desenvolvimento é recusada pela própria validação.");
    }

    /// <summary>
    /// Sobe a partir do diretório do assembly até achar a raiz do repositório
    /// (a que tem o README). Evita depender do número de níveis de pasta.
    /// </summary>
    private static string LocalizarRaizDoRepositorio()
    {
        var atual = new DirectoryInfo(AppContext.BaseDirectory);

        while (atual is not null)
        {
            if (File.Exists(Path.Combine(atual.FullName, "CyberProtech.slnx")))
            {
                return atual.FullName;
            }

            atual = atual.Parent;
        }

        throw new DirectoryNotFoundException("Raiz do repositório não encontrada a partir de " + AppContext.BaseDirectory);
    }
}

/// <summary>
/// Limite de conexões do pool.
/// </summary>
/// <remarks>
/// O padrão do Npgsql é 100, exatamente o <c>max_connections</c> padrão do
/// PostgreSQL. Com os dois iguais, a API pode ocupar todas as conexões e
/// deixar psql, pgAdmin e qualquer outro serviço sem entrada — foi o que o
/// teste de carga mostrou com 400 workers.
/// </remarks>
public class PoolDeConexaoTests
{
    private static string Montar(Dictionary<string, string?> config)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();
        return Infrastructure.DependencyInjection.MontarConnectionString(configuration);
    }

    [Fact]
    public void Sem_configuracao_mantem_o_padrao_do_npgsql()
    {
        var resultado = Montar(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=x;Username=u;Password=p",
        });

        Assert.Equal(100, new Npgsql.NpgsqlConnectionStringBuilder(resultado).MaxPoolSize);
    }

    [Fact]
    public void Aplica_o_limite_configurado()
    {
        var resultado = Montar(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=x;Username=u;Password=p",
            ["Database:MaxPoolSize"] = "20",
        });

        Assert.Equal(20, new Npgsql.NpgsqlConnectionStringBuilder(resultado).MaxPoolSize);
    }

    [Fact]
    public void Preserva_o_resto_da_connection_string()
    {
        var resultado = Montar(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Host=db;Port=5432;Database=cyberprotech_lab;Username=postgres;Password=changeme",
            ["Database:MaxPoolSize"] = "20",
        });

        var construtor = new Npgsql.NpgsqlConnectionStringBuilder(resultado);

        Assert.Equal("db", construtor.Host);
        Assert.Equal(5432, construtor.Port);
        Assert.Equal("cyberprotech_lab", construtor.Database);
        Assert.Equal("postgres", construtor.Username);
        Assert.Equal("changeme", construtor.Password);
        Assert.Equal(20, construtor.MaxPoolSize);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    public void Valor_invalido_ou_nao_positivo_nao_quebra_a_subida(string valor)
    {
        var resultado = Montar(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=x;Username=u;Password=p",
            ["Database:MaxPoolSize"] = valor,
        });

        // Melhor manter o padrão do que derrubar a API por config errada.
        Assert.Equal(100, new Npgsql.NpgsqlConnectionStringBuilder(resultado).MaxPoolSize);
    }

    [Fact]
    public void Connection_string_ausente_nao_quebra()
    {
        var resultado = Montar(new Dictionary<string, string?>());

        Assert.Equal(string.Empty, resultado);
    }

    [Fact]
    public void O_padrao_do_projeto_cabe_no_max_connections_padrao_do_postgres()
    {
        // 3 réplicas × 20 = 60 de 100. A conta tem que sobrar folga.
        const int replicas = 3;
        const int maxConnectionsPostgres = 100;

        var raiz = LocalizarRaizDoRepositorio();
        var caminho = Path.Combine(raiz, "backend", "API", "appsettings.json");
        var config = new ConfigurationBuilder().AddJsonFile(caminho).Build();

        var pool = config.GetValue<int?>("Database:MaxPoolSize") ?? 100;

        Assert.True(replicas * pool < maxConnectionsPostgres,
            $"Configuração atual: {replicas} réplicas × pool {pool} = {replicas * pool}, " +
            $"e o PostgreSQL aceita {maxConnectionsPostgres}. Não sobra para nenhum outro cliente.");
    }

    private static string LocalizarRaizDoRepositorio()
    {
        var atual = new DirectoryInfo(AppContext.BaseDirectory);

        while (atual is not null)
        {
            if (File.Exists(Path.Combine(atual.FullName, "CyberProtech.slnx")))
            {
                return atual.FullName;
            }

            atual = atual.Parent;
        }

        throw new DirectoryNotFoundException("Raiz do repositório não encontrada a partir de " + AppContext.BaseDirectory);
    }
}
