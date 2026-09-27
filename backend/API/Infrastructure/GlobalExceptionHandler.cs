using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
    /// <summary>unique_violation do PostgreSQL.</summary>
    private const string UniqueViolation = "23505";

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

        var (statusCode, title) = Classify(exception);

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

    private static (int StatusCode, string Title) Classify(Exception exception) => exception switch
    {
        // Conflito de unicidade tem resposta própria e útil; qualquer outro erro
        // de gravação continua sendo opaco, para não servir de oráculo do banco.
        DbUpdateException { InnerException: PostgresException { SqlState: UniqueViolation } } =>
            (StatusCodes.Status409Conflict, "Já existe um registro com este usuário ou e-mail."),

        DbUpdateException =>
            (StatusCodes.Status409Conflict, "Conflito ao gravar no banco."),

        KeyNotFoundException =>
            (StatusCodes.Status404NotFound, "Recurso não encontrado."),

        UnauthorizedAccessException =>
            (StatusCodes.Status403Forbidden, "Acesso negado."),

        _ =>
            (StatusCodes.Status500InternalServerError, "Erro interno."),
    };
}
