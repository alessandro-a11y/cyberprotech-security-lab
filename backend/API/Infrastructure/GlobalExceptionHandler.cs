using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Infrastructure;

/// <summary>
/// Converte exceções não tratadas em respostas RFC 9457 (ProblemDetails),
/// evitando vazar stack trace ou mensagem de banco para o cliente.
/// </summary>
/// <remarks>
/// Na Fase 5 este comportamento é desfalhado de propósito para o laboratório
/// de tratamento de erros e, depois, restaurado.
/// </remarks>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Erro não tratado em {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var (statusCode, title) = exception switch
        {
            DbUpdateException => (StatusCodes.Status409Conflict, "Conflito ao gravar no banco."),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Recurso não encontrado."),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Acesso negado."),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno."),
        };

        // O detalhe técnico fica no log, não na resposta.
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = "Ocorreu um erro ao processar a requisição.",
            Instance = httpContext.Request.Path,
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
