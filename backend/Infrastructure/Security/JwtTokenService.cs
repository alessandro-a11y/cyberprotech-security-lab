using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Security;

/// <summary>
/// Emissão de JWT assinado com HMAC-SHA256 (HS256).
/// </summary>
/// <remarks>
/// O token carrega <c>sub</c> (id), <c>unique_name</c> (usuário) e
/// <c>role</c>, e é validado no servidor por emissor, audiência, assinatura e
/// validade. A chave de assinatura vem da configuração — nunca do código.
/// </remarks>
public sealed class JwtTokenService : ITokenService
{
    private readonly JwtSettings _settings;
    private readonly SigningCredentials _credentials;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;

        // HMAC-SHA256 exige no mínimo 256 bits (32 bytes) de chave.
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));

        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes);

        var claims = new[]
        {
            // "sub" precisa ser string: o JwtSecurityTokenHandler rejeita GUID.
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: _credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

/// <summary>
/// Configuração do JWT, lida da seção "Jwt" do appsettings.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "cyberprotech-api";

    public string Audience { get; set; } = "cyberprotech-web";

    /// <summary>
    /// Chave de assinatura. Sobrescrever em produção pela variável de ambiente
    /// "Jwt__SigningKey" — nunca deixar o valor padrão em ambiente real.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; } = 60;
}
