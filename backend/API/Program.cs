using System.Text;
using API.Endpoints;
using API.Infrastructure;
using Infrastructure;
using Infrastructure.Lab;
using Infrastructure.Persistence;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Health checks: "database" é o único verificado em /api/health/ready.
builder.Services
    .AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection é obrigatória."),
        name: "database",
        tags: ["database"]);

// DbContext (PostgreSQL) + repositórios + hasher + emissor de token.
builder.Services.AddInfrastructure(builder.Configuration);

// Autenticação e autorização.
//
// O tamanho da chave é validado nos bindings de opções de AddInfrastructure
// (que roda antes daqui e já falha a subida, medindo em bytes: 32 é o mínimo
// real). Não duplicamos a regra aqui — manter as duas em sincronia é o que
// costuma deixar passar divergência.
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
          ?? throw new InvalidOperationException($"Seção \"{JwtSettings.SectionName}\" ausente na configuração.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Uma policy sem requisito é inválida: precisa de RequireAuthenticatedUser.
    options.AddPolicy(AuthorizationPolicies.Authenticated, policy => policy.RequireAuthenticatedUser());
    options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
        policy.RequireRole(AuthorizationPolicies.AdminRole));
});

// Fração de janela fixa por IP: freia força bruta sem travar a demonstração.
// Com o controle "rate-limit" desligado, a partição vira "sem limite" — é o
// cenário de falhas de autenticação do laboratório.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
    {
        var labState = httpContext.RequestServices.GetRequiredService<LabState>();
        var enabled = labState.Get(LabToggleId.RateLimit);

        // O estado do flag entra na chave de partição de propósito:
        // RateLimitPartition memoiza o limitador por chave, então a factory só
        // roda na primeira requisição daquela chave. Sem o prefixo, virar o
        // toggle não re-avaliava nada e o limitador antigo continuava valendo.
        var partitionKey =
            (enabled ? "on:" : "off:") + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido");

        if (!enabled)
        {
            return RateLimitPartition.GetNoLimiter(partitionKey);
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });
});

// CORS restrito às origens do frontend do laboratório (configurável via
// "Frontend:BaseUrl"). Ver CorsSetup para o porquê de cada restrição.
var allowedOrigins = CorsSetup.ResolveOrigins(
    builder.Configuration.GetValue<string>("Frontend:BaseUrl"),
    builder.Environment.IsDevelopment());

builder.Services.AddCors(options =>
    options.AddPolicy(
        CorsSetup.PolicyName,
        policy => policy
            .WithOrigins(allowedOrigins)
            .WithMethods(CorsSetup.AllowedMethods)
            .WithHeaders(CorsSetup.AllowedHeaders)));

var app = builder.Build();

// Aplica migrations pendentes e popula o seed antes de atender requisições.
await app.Services.InitializeDatabaseAsync(app.Configuration);

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsSetup.PolicyName);
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthEndpoints();
app.MapControllers();

app.Run();

// Os testes de integracao (tests/backend/CyberProtech.Tests.Integration) sobem a
// API com WebApplicationFactory<Program>, que precisa enxergar este tipo. Com
// top-level statements ele e interno por padrao.
public partial class Program;
