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
    /// <summary>
    /// Lista os usuários, opcionalmente filtrando por usuário ou e-mail.
    /// </summary>
    /// <param name="search">Termo de busca. Vazio ou ausente devolve todos.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await users.SearchAsync(search, cancellationToken);
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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdForUpdateAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
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

        var ownId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(ownId, out var parsed) && parsed == id;
    }
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
