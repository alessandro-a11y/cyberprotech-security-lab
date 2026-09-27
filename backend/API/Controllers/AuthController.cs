using System.Security.Claims;
using Application.DTOs;
using Application.Interfaces;
using Infrastructure.Lab;
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
    ITokenService tokenService,
    BioSanitizer bioSanitizer) : ControllerBase
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

    /// <summary>
    /// Atualiza o perfil de quem está logado. Só o próprio usuário, e só os
    /// campos de <see cref="UpdateProfileRequest"/> — papel e usuário ficam de fora.
    /// </summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> UpdateMe(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var id = CurrentUserId();
        if (id is null)
        {
            return Unauthorized();
        }

        if (await users.EmailInUseByOtherAsync(request.Email, id.Value, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "E-mail já cadastrado.",
            });
        }

        var user = await users.GetByIdForUpdateAsync(id.Value, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        user.Email = request.Email;
        user.Bio = bioSanitizer.Sanitize(request.Bio);

        await users.SaveChangesAsync(cancellationToken);

        // Token novo: a resposta é autenticada, e o token antigo continua valendo
        // até expirar (ver limitação registrada em backend/README.md).
        var (token, expiresAt) = tokenService.CreateToken(user);

        return Ok(new ProfileUpdateResponse(UserDto.FromEntity(user), token, expiresAt));
    }

    /// <summary>
    /// Troca a senha de quem está logado, exigindo a senha atual.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var id = CurrentUserId();
        if (id is null)
        {
            return Unauthorized();
        }

        var user = await users.GetByIdForUpdateAsync(id.Value, cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        // Sem a senha atual, um token vazado já seria suficiente para tomar a conta.
        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Senha atual inválida.",
            });
        }

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);

        await users.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
