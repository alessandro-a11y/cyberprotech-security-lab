using System.Security.Claims;
using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers;

/// <summary>
/// Cadastro, login e identidade da sessão.
/// </summary>
/// <remarks>
/// Decisões de segurança desta fase:
/// <list type="bullet">
/// <item>mensagem de erro única no login, para não revelar se o usuário existe;</item>
/// <item>conferência de hash mesmo quando o usuário não existe, para que o tempo
/// de resposta não denuncie a existência da conta;</item>
/// <item>o auto-cadastro sempre cria <c>User</c> — o papel nunca vem do cliente.</item>
/// </list>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class AuthController(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokenService) : ControllerBase
{
    private const string InvalidCredentialsMessage = "Usuário ou senha inválidos.";

    /// <summary>
    /// Cadastra um novo usuário e devolve um token de sessão.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (await users.ExistsByUsernameOrEmailAsync(request.Username, request.Email, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Usuário ou e-mail já cadastrado.",
            });
        }

        var user = new Domain.Entities.User
        {
            Username = request.Username,
            Email = request.Email,
            Bio = request.Bio,
            // Papel fixo: o cliente não escolhe o próprio nível de acesso.
            Role = "User",
            PasswordHash = passwordHasher.Hash(request.Password),
        };

        await users.AddAsync(user, cancellationToken);
        await users.SaveChangesAsync(cancellationToken);

        var (token, expiresAt) = tokenService.CreateToken(user);

        return CreatedAtAction(
            nameof(Me),
            new { id = user.Id },
            new AuthResponse(token, expiresAt, UserDto.FromEntity(user)));
    }

    /// <summary>
    /// Autentica e emite um token de sessão.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByUsernameAsync(request.Username, cancellationToken);

        // Hash é conferido mesmo sem usuário, para que o login inexistente não
        // seja detectavelmente mais rápido que o existente.
        var passwordMatches = passwordHasher.VerifyOrDummy(request.Password, user?.PasswordHash);

        if (user is null || !passwordMatches)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = InvalidCredentialsMessage,
            });
        }

        var (token, expiresAt) = tokenService.CreateToken(user);

        return Ok(new AuthResponse(token, expiresAt, UserDto.FromEntity(user)));
    }

    /// <summary>
    /// Devolve o usuário do token informado. Serve ao frontend para restaurar a
    /// sessão depois de um recarregamento de página.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        var id = CurrentUserId();

        if (id is null)
        {
            return Unauthorized();
        }

        var user = await users.GetByIdAsync(id.Value, cancellationToken);
        if (user is null)
        {
            // Token válido para um usuário que não existe mais (excluído).
            return Unauthorized();
        }

        return Ok(UserDto.FromEntity(user));
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
