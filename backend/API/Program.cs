using System.Text;
using API.Endpoints;
using API.Infrastructure;
using Infrastructure;
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
        ?? AppDbContextFactory.FallbackConnectionString,
        name: "database",
        tags: ["database"]);

// DbContext (PostgreSQL) + repositórios + hasher + emissor de token.
builder.Services.AddInfrastructure(builder.Configuration);

// Autenticação e autorização.
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
          ?? throw new InvalidOperationException($"Seção \"{JwtSettings.SectionName}\" ausente na configuração.");

if (jwt.SigningKey.Length < 32)
{
    // HMAC-SHA256 com chave curta é quebrável; melhor falhar na subida do que
    // aceitar um token que qualquer um assina.
    throw new InvalidOperationException(
        "Jwt:SigningKey precisa ter ao menos 32 caracteres. Defina a variável de ambiente Jwt__SigningKey.");
}

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
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
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

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthEndpoints();
app.MapControllers();

app.Run();
