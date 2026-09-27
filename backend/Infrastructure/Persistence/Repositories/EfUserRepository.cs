using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação EF Core do repositório de usuários.
/// </summary>
public sealed class EfUserRepository(AppDbContext dbContext) : IUserRepository
{
    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Users
            .AsNoTracking()
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    /// <remarks>
    /// Igual a <see cref="GetByIdAsync"/>, mas devolve a entidade rastreada para
    /// que alterações e remoções possam ser gravadas com
    /// <see cref="SaveChangesAsync"/>. Não use para simples leitura.
    /// </remarks>
    public async Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<IReadOnlyList<User>> SearchAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // O termo viaja como parâmetro (nunca concatenado no SQL), mas os
            // curingas do LIKE precisam ser escapados para que a busca por
            // "100%" não corresponda a qualquer coisa.
            var term = $"%{EscapeLikePattern(search.Trim())}%";

            query = query.Where(u =>
                EF.Functions.ILike(u.Username, term, LikeEscapeCharacter)
                || EF.Functions.ILike(u.Email, term, LikeEscapeCharacter));
        }

        return await query.OrderBy(u => u.Username).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> SearchAsync(
        string? search,
        string? role,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{EscapeLikePattern(search.Trim())}%";

            query = query.Where(u =>
                EF.Functions.ILike(u.Username, term, LikeEscapeCharacter)
                || EF.Functions.ILike(u.Email, term, LikeEscapeCharacter));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            // Papel exato e sem caixa: a coluna guarda "Admin"/"User".
            var normalizedRole = role.Trim();
            query = query.Where(u => u.Role == normalizedRole);
        }

        return await query
            .OrderBy(u => u.Username)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == Canonicalize(username), cancellationToken);

    public Task<bool> ExistsByUsernameOrEmailAsync(
        string username,
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = Canonicalize(username);
        var normalizedEmail = Canonicalize(email);

        return dbContext.Users.AnyAsync(
            u => u.Username == normalizedUsername || u.Email == normalizedEmail,
            cancellationToken);
    }

    /// <summary>
    /// Verdadeiro quando o e-mail já pertence a <em>outro</em> usuário.
    /// É o que o perfil precisa saber: reenviar o próprio e-mail não é conflito.
    /// </summary>
    public Task<bool> EmailInUseByOtherAsync(
        string email,
        Guid excludeUserId,
        CancellationToken cancellationToken = default) =>
        dbContext.Users.AnyAsync(
            u => u.Email == Canonicalize(email) && u.Id != excludeUserId,
            cancellationToken);

    public Task<int> CountByRoleAsync(string role, CancellationToken cancellationToken = default) =>
        dbContext.Users.CountAsync(u => u.Role == role, cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        dbContext.Users.CountAsync(cancellationToken);

    /// <remarks>
    /// Username e e-mail são canonicalizados para minúsculas na gravação. Como o
    /// índice único do PostgreSQL diferencia maiúsculas, normalizar na entrada é o
    /// que impede "Admin" e "admin" de virarem dois usuários distintos.
    /// </remarks>
    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        user.Username = Canonicalize(user.Username);
        user.Email = Canonicalize(user.Email);

        await dbContext.Users.AddAsync(user, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public Task DeleteAsync(User user, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Remove(user);
        return Task.CompletedTask;
    }

    private const string LikeEscapeCharacter = "\\";

    private static string EscapeLikePattern(string value) =>
        value.Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter)
             .Replace("%", LikeEscapeCharacter + "%")
             .Replace("_", LikeEscapeCharacter + "_");

    private static string Canonicalize(string value) =>
        value.Trim().ToLowerInvariant();
}
