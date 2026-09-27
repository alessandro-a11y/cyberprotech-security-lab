using System.Text;
using Application.Interfaces;
using Infrastructure.Lab;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;using Microsoft.Extensions.Configuration;
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
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

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
}
