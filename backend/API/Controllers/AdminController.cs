using System.Security.Claims;
using Application.DTOs;
using Application.Interfaces;
using API.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Área administrativa. Toda a classe exige a policy <c>AdminOnly</c>, então
/// qualquer <c>[AllowAnonymous]</c> aqui dentro abriria uma brecha — não haverá.
/// </summary>
/// <remarks>
/// A tela de Administração do frontend hoje avisa que um usuário comum consegue
/// abrir a página. Isso é o cenário de Broken Access Control do laboratório: a
/// correção é a policy daqui, e o frontend ainda não consome estes endpoints.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminController(IUserRepository users) : ControllerBase
{
    /// <summary>
    /// Contagens para o Dashboard e a tela de Administração. Sem isto o
    /// frontend calcula os totais no cliente, e o número vem de uma lista
    /// inteira em vez do banco.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType<AdminStats>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminStats>> GetStats(CancellationToken cancellationToken)
    {
        var totalUsers = await users.CountAsync(cancellationToken);
        var admins = await users.CountByRoleAsync(AuthorizationPolicies.AdminRole, cancellationToken);
        var regularUsers = await users.CountByRoleAsync(AuthorizationPolicies.UserRole, cancellationToken);

        // Evita divisão por zero em banco vazio.
        var percentage = totalUsers == 0 ? 0 : (int)Math.Round(admins * 100.0 / totalUsers);

        return Ok(new AdminStats(totalUsers, admins, regularUsers, percentage));
    }

    /// <summary>
    /// Troca o papel de um usuário (promover ou rebaixar).
    /// </summary>
    /// <remarks>
    /// Três travas, todas com o mesmo motivo: uma mudança de papel errada pode
    /// deixar o portal sem nenhum administrador.
    /// <list type="number">
    /// <item>o papel precisa estar na allowlist — um valor arbitrário do cliente
    /// criaria um papel que nenhuma policy reconhece;</item>
    /// <item>ninguém altera o próprio papel, nem Admin — evita que o
    /// administrador se rebaixe por engano e também impede escalar a si mesmo
    /// de forma inconsistente com o token em mãos;</item>
    /// <item>o último Admin não pode ser rebaixado.</item>
    /// </list>
    /// </remarks>
    [HttpPut("users/{id:guid}/role")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> ChangeRole(
        Guid id,
        [FromBody] ChangeRoleRequest request,
        CancellationToken cancellationToken)
    {
        var targetRole = request.Role?.Trim();

        if (targetRole is not (AuthorizationPolicies.AdminRole or AuthorizationPolicies.UserRole))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = $"Papel inválido. Use \"{AuthorizationPolicies.AdminRole}\" ou \"{AuthorizationPolicies.UserRole}\".",
            });
        }

        if (CurrentUserId() == id)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Um administrador não altera o próprio papel.",
            });
        }

        var user = await users.GetByIdForUpdateAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        if (user.Role == targetRole)
        {
            return Ok(UserDto.FromEntity(user));
        }

        var isDemotingAdmin =
            user.Role == AuthorizationPolicies.AdminRole
            && targetRole == AuthorizationPolicies.UserRole;

        if (isDemotingAdmin && await users.CountByRoleAsync(AuthorizationPolicies.AdminRole, cancellationToken) <= 1)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Não é possível rebaixar o último administrador.",
            });
        }

        user.Role = targetRole;

        await users.SaveChangesAsync(cancellationToken);

        return Ok(UserDto.FromEntity(user));
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
