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
