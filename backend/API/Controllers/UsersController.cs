using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Fase 1: listagem/leitura simples de usuários (sem autenticação).
/// Autenticação, autorização e as vulnerabilidades controladas
/// serão implementadas nas Fases 2 e 3, respectivamente.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class UsersController(IUserRepository users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await users.GetAllAsync(cancellationToken);
        return Ok(result.Select(u => new UserDto(u.Id, u.Username, u.Email, u.Role)).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(new UserDto(user.Id, user.Username, user.Email, user.Role));
    }
}
