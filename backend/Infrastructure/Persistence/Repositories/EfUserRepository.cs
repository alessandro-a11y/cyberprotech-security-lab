using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação EF Core do repositório de usuários (Fase 1: leitura e busca).
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

    private const string LikeEscapeCharacter = "\\";

    private static string EscapeLikePattern(string value) =>
        value.Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter)
             .Replace("%", LikeEscapeCharacter + "%")
             .Replace("_", LikeEscapeCharacter + "_");
}
