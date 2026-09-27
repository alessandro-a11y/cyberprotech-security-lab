using Application.Interfaces;
using Application.DTOs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Lab;

/// <summary>
/// Configuração do laboratório, lida da seção "Lab".
/// </summary>
public sealed class LabOptions
{
    public const string SectionName = "Lab";

    /// <summary>
    /// Interruptor mestre. Mesmo ligado, os controles só ficam graváveis em
    /// ambiente de desenvolvimento (ver <see cref="LabState"/>).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Estado inicial do modo vulnerável.</summary>
    public bool VulnMode { get; set; }

    /// <summary>Estado inicial do modo de erros detalhados.</summary>
    public bool VerboseErrors { get; set; }

    /// <summary>Estado inicial do rate limit de login. Ligado por padrão.</summary>
    public bool RateLimit { get; set; } = true;

    /// <summary>Estado inicial dos headers de segurança. Ligado por padrão.</summary>
    public bool SecurityHeaders { get; set; } = true;
}

/// <summary>
/// Estado mutável dos controles do laboratório, em memória.
/// </summary>
/// <remarks>
/// <para>
/// Vive em memória de propósito: reiniciar a API devolve tudo ao estado
/// configurado, que é o estado seguro. Persistir significaria que um deploy
/// esquecido deixaria o modo vulnerável ligado.
/// </para>
/// <para>
/// <see cref="LabState.Writable"/> é a dupla trava: exige
/// <see cref="LabOptions.Enabled"/> <b>e</b> ambiente de desenvolvimento. Com a
/// configuração padrão em produção, os controles recusam mudar em vez de
/// expor o portal aberto.
/// </para>
/// </remarks>
public sealed class LabState
{
    // .NET 8 não tem System.Threading.Lock (chegou no 9).
    private readonly object _gate = new();
    private readonly LabOptions _options;
    private readonly bool _isDevelopment;
    private readonly ILogger<LabState> _logger;

    private bool _vulnMode;
    private bool _verboseErrors;
    private bool _rateLimit;
    private bool _securityHeaders;

    public LabState(
        IOptions<LabOptions> options,
        IHostEnvironment environment,
        ILogger<LabState> logger)
    {
        _options = options.Value;
        _isDevelopment = environment.IsDevelopment();
        _logger = logger;

        _vulnMode = _options.VulnMode;
        _verboseErrors = _options.VerboseErrors;
        _rateLimit = _options.RateLimit;
        _securityHeaders = _options.SecurityHeaders;
    }

    /// <summary>Laboratório habilitável nesta instância.</summary>
    public bool Enabled => _options.Enabled;

    /// <summary>Os controles podem ser alterados por um Admin, nesta instância.</summary>
    public bool Writable => _options.Enabled && _isDevelopment;

    public LabToggle[] GetToggles()
    {
        lock (_gate)
        {
            return
            [
                new LabToggle("vuln-mode", "Modo vulnerável", "Ativa os cenários inseguros do laboratório", _vulnMode),
                new LabToggle("verbose-errors", "Erros detalhados", "Expõe stack trace nas respostas da API", _verboseErrors),
                new LabToggle("rate-limit", "Limite de tentativas de login", "Bloqueia força bruta", _rateLimit),
                new LabToggle("sec-headers", "Headers de segurança", "CSP, HSTS, X-Frame-Options", _securityHeaders),
            ];
        }
    }

    /// <summary>
    /// Aplica as mudanças enviadas. <paramref name="request"/> não é alterado.
    /// </summary>
    public void Apply(LabConfigUpdateRequest request)
    {
        lock (_gate)
        {
            if (request.VulnMode.HasValue)
            {
                _vulnMode = request.VulnMode.Value;
            }

            if (request.VerboseErrors.HasValue)
            {
                _verboseErrors = request.VerboseErrors.Value;
            }

            if (request.RateLimit.HasValue)
            {
                _rateLimit = request.RateLimit.Value;
            }

            if (request.SecurityHeaders.HasValue)
            {
                _securityHeaders = request.SecurityHeaders.Value;
            }
        }

        if (request.VulnMode == true || _vulnMode)
        {
            _logger.LogWarning(
                "MODO VULNERÁVEL ATIVO no laboratório. endpoints sem proteção contra: SQLi, XSS e IDOR. Não usar em rede confiável.");
        }
    }

    /// <summary>Valor bruto, para o código que decide o comportamento.</summary>
    public bool Get(LabToggleId id)
    {
        lock (_gate)
        {
            return id switch
            {
                LabToggleId.VulnMode => _vulnMode,
                LabToggleId.VerboseErrors => _verboseErrors,
                LabToggleId.RateLimit => _rateLimit,
                LabToggleId.SecurityHeaders => _securityHeaders,
                _ => false,
            };
        }
    }
}

/// <summary>
/// Identificadores dos controles, para o código interno ler o estado sem
/// depender das strings do frontend.
/// </summary>
public enum LabToggleId
{
    VulnMode,
    VerboseErrors,
    RateLimit,
    SecurityHeaders,
}
