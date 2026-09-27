using Infrastructure.Lab;

namespace API.Infrastructure;

/// <summary>
/// Adiciona headers de segurança às respostas.
/// </summary>
/// <remarks>
/// <para>
/// Com <c>sec-headers</c> ligado, a API responde com CSP, HSTS,
/// <c>X-Content-Type-Options</c>, <c>X-Frame-Options</c> e
/// <c>Referrer-Policy</c>. Desligado, nenhum header é enviado — que é o cenário
/// <c>misconfig</c> do laboratório, e o que o Security Toolkit procura quando
/// roda sem esses headers.
/// </para>
/// <para>
/// A CSP é restritiva de propósito: o frontend é servido por outra origem
/// (porta 5173), então o estilo inline do Vite em desenvolvimento exigiria
/// <c>'unsafe-inline'</c>. Ela já está na lista, senão a interface quebraria
/// quando o toggle estivesse ligado.
/// </para>
/// </remarks>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-CyberProtech-Lab";

    public async Task InvokeAsync(HttpContext context, LabState labState)
    {
        if (labState.Get(LabToggleId.SecurityHeaders))
        {
            AddHeaders(context);
        }

        await next(context);
    }

    private static void AddHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Sem default-src '*': a política só permite o que a interface usa.
        // 'unsafe-inline' em style-src cobre o CSS injetado pelo Vite.
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data:; " +
            "connect-src 'self' http://localhost:5000 http://127.0.0.1:5000; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'";

        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";

        // Serve ao toolkit confirmar o estado do toggle por header, sem precisar
        // chamar /api/lab/config.
        headers[HeaderName] = "on";
    }
}
