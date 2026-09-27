namespace API.Infrastructure;

/// <summary>
/// Política de CORS da API.
/// </summary>
/// <remarks>
/// <para>
/// Restritiva de propósito. A API usa <c>Authorization: Bearer</c> e não
/// cookie, então <c>AllowAnyHeader</c> e <c>AllowAnyMethod</c> não têm
/// justificativa — e "CORS aberto" é um dos cenários do laboratório
/// (<c>frontend/src/data/vulnerabilities.js</c>, <c>misconfig</c>), então a
/// versão corrigida precisa ser o oposto do alvo.
/// </para>
/// <para>
/// <c>AllowAnyOrigin</c> nunca é usado, e <c>AllowCredentials</c> fica
/// desligado: com token no header, habilitar credencial só abriria espaço para
/// CSRF sem ganho nenhum.
/// </para>
/// </remarks>
public static class CorsSetup
{
    /// <summary>Nome da policy, referenciado em <c>app.UseCors</c>.</summary>
    public const string PolicyName = "Frontend";

    /// <summary>
    /// Só os verbos que a API usa. <c>AllowAnyMethod</c> liberaria verbos que
    /// nem existem na API.
    /// </summary>
    public static readonly string[] AllowedMethods = ["GET", "POST", "PUT", "DELETE"];

    /// <summary>
    /// Só os headers que o frontend envia. <c>Authorization</c> é o essencial;
    /// os outros são o mínimo para o fetch montar a requisição.
    /// </summary>
    public static readonly string[] AllowedHeaders = ["Authorization", "Content-Type", "Accept"];

    /// <summary>
    /// Origens padrão de desenvolvimento. Cobrem <c>localhost</c> e
    /// <c>127.0.0.1</c> porque o navegador trata os dois como origens distintas:
    /// sem o segundo, abrir a interface por um dos dois quebra o CORS.
    /// </summary>
    private static readonly string[] DevelopmentOrigins =
    [
        "http://localhost:5173",
        "http://127.0.0.1:5173",
    ];

    /// <summary>
    /// Monta as origens permitidas a partir de <c>Frontend:BaseUrl</c>, que
    /// aceita uma ou várias separadas por <c>;</c> ou vírgula.
    /// </summary>
    /// <param name="configuredBaseUrl">Valor de <c>Frontend:BaseUrl</c>.</param>
    /// <param name="isDevelopment">Se está em ambiente de desenvolvimento.</param>
    public static string[] ResolveOrigins(string? configuredBaseUrl, bool isDevelopment)
    {
        var origins = new List<string>();

        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            foreach (var candidate in configuredBaseUrl.Split([';', ','], StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = candidate.Trim();

                if (trimmed.Length > 0 && !origins.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                {
                    origins.Add(trimmed);
                }
            }
        }

        if (isDevelopment)
        {
            foreach (var origin in DevelopmentOrigins)
            {
                if (!origins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                {
                    origins.Add(origin);
                }
            }
        }

        return [.. origins];
    }
}
