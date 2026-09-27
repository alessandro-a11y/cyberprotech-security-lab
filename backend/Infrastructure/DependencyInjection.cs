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
        // receberia uma instância vazia, com SigningString em branco.
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.SigningKey),
                "Jwt:SigningKey é obrigatória.")
            .Validate(
                settings => settings.SigningKey.Length >= 32,
                "Jwt:SigningKey precisa ter ao menos 32 caracteres.")
            .ValidateOnStart();

        services.AddSingleton<ITokenService, JwtTokenService>();

        // Estado do laboratório: mutável em memória, reinicia no estado seguro.
        services.Configure<LabOptions>(configuration.GetSection(LabOptions.SectionName));
        services.AddSingleton<LabState>();

        return services;
    }
}
