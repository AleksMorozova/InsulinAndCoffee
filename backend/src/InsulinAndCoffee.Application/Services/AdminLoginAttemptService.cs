using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsulinAndCoffee.Application.Services;

public sealed class AdminLoginAttemptService(IAppDbContext db)
{
    public async Task<PaginatedResult<LoginAttemptDto>> GetAsync(
        int page,
        int pageSize,
        string? username,
        bool? success,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = ApplyFilters(db.LoginAttempts.AsNoTracking(), username, success, from, to);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(attempt => attempt.AttemptedAtUtc)
            .ThenByDescending(attempt => attempt.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(attempt => new LoginAttemptDto(
                attempt.Id, attempt.Username, attempt.UserId, attempt.IsSuccessful, attempt.AttemptedAtUtc,
                attempt.FailureReason, attempt.IpAddress, attempt.UserAgent))
            .ToListAsync(cancellationToken);
        return new PaginatedResult<LoginAttemptDto>(items, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<LoginAttemptStatsDto> GetStatsAsync(
        string? username,
        bool? success,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(db.LoginAttempts.AsNoTracking(), username, success, from, to);
        return new LoginAttemptStatsDto(
            await query.CountAsync(cancellationToken),
            await query.CountAsync(attempt => attempt.IsSuccessful, cancellationToken),
            await query.CountAsync(attempt => !attempt.IsSuccessful, cancellationToken),
            await query.Select(attempt => attempt.Username).Distinct().CountAsync(cancellationToken));
    }

    private static IQueryable<LoginAttempt> ApplyFilters(
        IQueryable<LoginAttempt> query,
        string? username,
        bool? success,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            var term = username.Trim().ToLower();
            query = query.Where(attempt => attempt.Username.ToLower().Contains(term));
        }
        if (success.HasValue) query = query.Where(attempt => attempt.IsSuccessful == success.Value);
        if (from.HasValue) query = query.Where(attempt => attempt.AttemptedAtUtc >= from.Value);
        if (to.HasValue) query = query.Where(attempt => attempt.AttemptedAtUtc <= to.Value);
        return query;
    }
}
