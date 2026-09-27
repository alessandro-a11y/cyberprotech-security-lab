using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Application.DTOs;
using Application.Interfaces;
using API.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Gestão de usuários.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>GET /api/users</c> exige usuário autenticado;</item>
/// <item>ler um usuário específico exige ser o próprio usuário ou Admin — é a
/// versão corrigida do cenário de IDOR que a Fase 3 vai introduzir de propósito;</item>
/// <item>criar, alterar e excluir são exclusivos de Admin.</item>
/// </list>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.Authenticated)]
public sealed class UsersController(IUserRepository users) : ControllerBase
{
    /// <summary>Teto de itens por página, para ninguém pedir a tabela inteira.</summary>
    private const int MaxPageSize = 100;

    private const int DefaultPageSize = 50;

    /// <summary>
    /// Lista os usuários, com busca opcional, filtro de papel e paginação.
    /// </summary>
    /// <param name="search">Termo em usuário ou e-mail.</param>
    /// <param name="role">Filtra por papel exato ("Admin" ou "User").</param>
    /// <param name="page">Página 1-based. Padrão 1.</param>
    /// <param name="pageSize">Itens por página, de 1 a 100. Padrão 50.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "page deve ser maior ou igual a 1.",
            });
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = $"pageSize deve estar entre 1 e {MaxPageSize}.",
            });
        }

        if (search is { Length: > 100 })
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "search deve ter no máximo 100 caracteres.",
            });
        }

        var skip = (page - 1) * pageSize;
        var result = await users.SearchAsync(search, role, skip, pageSize, cancellationToken);

        return Ok(result.Select(UserDto.FromEntity).ToList());
    }

    /// <summary>
    /// Obtém um usuário pelo identificador.
    /// </summary>
    /// <param name="id">Identificador do usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!CanAccess(User, id))
        {
            return Forbid();
        }

        var user = await users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(UserDto.FromEntity(user));
    }

    /// <summary>
    /// Atualiza o perfil (bio) de um usuário. O papel não é alterável por aqui.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdForUpdateAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.Bio = request.Bio;

        await users.SaveChangesAsync(cancellationToken);

        return Ok(UserDto.FromEntity(user));
    }

    /// <summary>
    /// Remove um usuário. Fora da produção, é o endpoint que mais aparece no
    /// laboratório de controle de acesso.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        // Sem esta trava o administrador apaga a própria conta e o portal pode
        // ficar sem nenhum Admin — ninguém consegue mais entrar para corrigir.
        if (CurrentUserId() == id)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Um administrador não remove a própria conta.",
            });
        }

        var user = await users.GetByIdForUpdateAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (user.Role == AuthorizationPolicies.AdminRole
            && await users.CountByRoleAsync(AuthorizationPolicies.AdminRole, cancellationToken) <= 1)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Não é possível remover o último administrador.",
            });
        }

        await users.DeleteAsync(user, cancellationToken);
        await users.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Admin vê qualquer usuário; usuário comum só o próprio.
    /// </summary>
    private static bool CanAccess(ClaimsPrincipal principal, Guid id)
    {
        if (principal.IsInRole(AuthorizationPolicies.AdminRole))
        {
            return true;
        }

        return CurrentId(principal) == id;
    }

    private static Guid? CurrentId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private Guid? CurrentUserId() => CurrentId(User);
}

/// <summary>
/// Campos editáveis por um administrador.
/// </summary>
/// <remarks>
/// Não inclui <c>Role</c> nem <c>PasswordHash</c>: trocar o próprio papel por
/// este endpoint permitiria escalada de privilégio. A troca de senha tem
/// endpoint próprio, com a senha atual.
/// </remarks>
public sealed class UpdateUserRequest
{
    [StringLength(512, ErrorMessage = "A bio deve ter no máximo 512 caracteres.")]
    public string? Bio { get; set; }
}
