using System.Text;
using Application.Interfaces;
using Infrastructure.Lab;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;using Microsoft.Extensions.Configuration;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

/// <summary>
/// Registro dos serviços de infraestrutura (DbContext, repositórios e segurança).
/// A connection string pode vir do appsettings ou da variável de ambiente
/// "ConnectionStrings__DefaultConnection" (usada pelo Docker Compose).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
            MontarConnectionString(configuration)));

        services.AddScoped<IUserRepository, EfUserRepository>();

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        // JwtTokenService resolve IOptions<JwtSettings>; sem este binding ele
        // receberia uma instância vazia, com SigningKey em branco.
        //
        // A chave é medida em BYTES, não em caracteres: HMAC-SHA256 exige no
        // mínimo 256 bits, e a biblioteca de token rejeita 31 bytes e aceita
        // 32. A mensagem de erro dela diz "maior que 256 bits", o que sugeriria
        // 33 — mas o comportamento real é >= 32 bytes. Como um caractere
        // não-ASCII ocupa mais de um byte, contar caracteres erraria a conta.
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.SigningKey),
                "Jwt:SigningKey é obrigatória.")
            .Validate(
                settings => Encoding.UTF8.GetByteCount(settings.SigningKey) >= 32,
                "Jwt:SigningKey precisa ter no mínimo 32 bytes (256 bits) para HMAC-SHA256. Gere uma com: openssl rand -base64 48")
            .ValidateOnStart();

        services.AddSingleton<ITokenService, JwtTokenService>();

        // Estado do laboratório: mutável em memória, reinicia no estado seguro.
        services.Configure<LabOptions>(configuration.GetSection(LabOptions.SectionName));
        services.AddSingleton<LabState>();
        services.AddSingleton<BioSanitizer>();

        return services;
    }

    /// <summary>
    /// Monta a connection string aplicando o limite de pool, sem sobrescrever
    /// o que já vier definido nela.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O pool padrão do Npgsql é 100, exatamente o <c>max_connections</c> padrão
    /// do PostgreSQL. Sob carga, a API ocupa todas as conexões e deixa
    /// <c>psql</c>, pgAdmin e qualquer outro serviço sem entrada — foi o que o
    /// teste de carga mostrou com 400 workers.
    /// </para>
    /// <para>
    /// A conta é sempre <c>(réplicas × pool) &lt; max_connections</c>. Com pool
    /// 20 e 3 réplicas, sobram 40 das 100 conexões.
    /// </para>
    /// </remarks>
    internal static string MontarConnectionString(IConfiguration configuration)
    {
        var original = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(original))
        {
            return original ?? string.Empty;
        }

        var construtor = new NpgsqlConnectionStringBuilder(original);

        // TryParse, e não GetValue<int?>(): o binder lança exceção em valor não
        // numérico, o que derrubaria a API na subida por causa de uma config
        // errada. Um valor ilegível é ignorado, e o padrão do Npgsql vale.
        if (int.TryParse(configuration["Database:MaxPoolSize"], out var pool) && pool > 0)
        {
            construtor.MaxPoolSize = pool;
        }

        return construtor.ConnectionString;
    }
}
