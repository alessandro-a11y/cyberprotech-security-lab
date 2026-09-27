using Infrastructure.Lab;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace API.Infrastructure;

/// <summary>
/// Converte exceções não tratadas em respostas RFC 9457 (ProblemDetails).
/// </summary>
/// <remarks>
/// <para>
/// O comportamento normal esconde o detalhe técnico: o stack trace e a mensagem
/// do banco vão para o log, e a resposta devolve só título, <c>traceId</c> e
/// uma frase genérica.
/// </para>
/// <para>
/// Com o controle <c>verbose-errors</c> ligado, a resposta passa a carregar a
/// exceção, o stack trace e o SQL que o PostgreSQL recusou. É o cenário
/// <c>exposure</c>/"stack trace exposto em erro" do laboratório — e o alvo
/// concreto que o Security Toolkit do Daniel procura.
/// </para>
/// <para>
/// O vazamento é sempre <b>sob demanda</b> e nunca parcial: ou a resposta traz
/// o diagnóstico inteiro, ou traz o genérico. Não há caminho intermediário em
/// que o detalhe vaze sem ser pedido.
/// </para>
/// </remarks>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    LabState labState) : IExceptionHandler
{
    /// <summary>unique_violation do PostgreSQL.</summary>
    private const string UniqueViolation = "23505";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = Classify(exception);

        // No modo detalhado o stack trace também vai para a resposta, então o
        // log não é o único caminho de diagnóstico.
        if (labState.Get(LabToggleId.VerboseErrors))
        {
            logger.LogError(
                exception,
                "Erro não tratado em {Method} {Path} (modo detalhado)",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            logger.LogError(
                exception,
                "Erro não tratado em {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        var verbose = labState.Get(LabToggleId.VerboseErrors);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = verbose ? exception.Message : "Ocorreu um erro ao processar a requisição.",
            Instance = httpContext.Request.Path,
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (verbose)
        {
            problemDetails.Extensions["exception"] = exception.GetType().FullName;
            problemDetails.Extensions["stackTrace"] = exception.ToString();
            problemDetails.Extensions["path"] = httpContext.Request.Path.Value;

            // A mensagem do PostgreSQL traz o SQL, os valores e o próprio DDL da
            // tabela. É exatamente o que o cenário de exposição de dados promete.
            if (exception.InnerException is PostgresException postgres)
            {
                problemDetails.Extensions["sqlState"] = postgres.SqlState;
                problemDetails.Extensions["postgresMessage"] = postgres.MessageText;
            }
        }

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
