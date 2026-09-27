using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace API.Endpoints;

/// <summary>
/// Endpoints de health check usados pelo Docker Compose e pelo frontend.
/// </summary>
/// <remarks>
/// <para><b>/api/health</b> - liveness. Responde 200 enquanto o processo subir,
/// sem tocar no banco. É o que o <c>healthcheck</c> do container consulta.</para>
/// <para><b>/api/health/ready</b> - readiness. Verifica de fato o PostgreSQL,
/// então falha (503) enquanto o banco não estiver acessível.</para>
/// </remarks>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/api/health", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponse,
        });

        app.MapHealthChecks("/api/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("database"),
            ResponseWriter = WriteResponse,
        });
    }

    private static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            service = "cyberprotech-api",
            time = DateTime.UtcNow,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString().ToLowerInvariant(),
                description = entry.Value.Description,
                durationMs = entry.Value.Duration.TotalMilliseconds,
            }),
        };

        return context.Response.WriteAsJsonAsync(payload);
    }
}
