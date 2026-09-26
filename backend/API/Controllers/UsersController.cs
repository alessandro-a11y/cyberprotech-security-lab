using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Leitura de usuários.
/// </summary>
/// <remarks>
/// Fase 1: endpoints públicos, sem autenticação. A autenticação, a autorização
/// por papel e o controle de acesso (IDOR / Broken Access Control) chegam na Fase 2.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(UserDto.FromEntity(user));
    }
}
