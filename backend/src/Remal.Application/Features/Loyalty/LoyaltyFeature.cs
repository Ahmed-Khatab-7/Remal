using MediatR;
using Microsoft.EntityFrameworkCore;
using Remal.Application.Common.Exceptions;
using Remal.Application.Common.Interfaces;
using Remal.Domain.Entities;

namespace Remal.Application.Features.Loyalty;

// ---------- DTOs ----------
public record LoyaltyBalanceDto(int Balance, int LifetimeEarned, int LifetimeSpent,
    string TierName, string NextTierName, int PointsToNextTier);

public record PointsTransactionDto(Guid Id, DateTime Timestamp, string Type, int Points, string Description);

// ---------- Queries ----------
public record GetMyLoyaltyQuery(string UserId) : IRequest<LoyaltyBalanceDto>;

public class GetMyLoyaltyHandler : IRequestHandler<GetMyLoyaltyQuery, LoyaltyBalanceDto>
{
    private readonly IApplicationDbContext _db;
    public GetMyLoyaltyHandler(IApplicationDbContext db) => _db = db;

    public async Task<LoyaltyBalanceDto> Handle(GetMyLoyaltyQuery req, CancellationToken ct)
    {
        var acct = await _db.LoyaltyAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == req.UserId, ct);
        if (acct is null) return new(0, 0, 0, "رملة", "تل رملي", 500);

        var (next, needed) = acct.Tier switch
        {
            LoyaltyTier.Grain => ("تل رملي", 500 - acct.Balance),
            LoyaltyTier.SandHill => ("كثيب", 1500 - acct.Balance),
            LoyaltyTier.Dune => ("صحراء", 3000 - acct.Balance),
            _ => ("صحراء", 0),
        };
        return new(acct.Balance, acct.LifetimeEarned, acct.LifetimeSpent, acct.TierName, next, Math.Max(0, needed));
    }
}

public record GetMyLoyaltyTransactionsQuery(string UserId, int Page = 1, int PageSize = 50) : IRequest<List<PointsTransactionDto>>;

public class GetMyLoyaltyTransactionsHandler : IRequestHandler<GetMyLoyaltyTransactionsQuery, List<PointsTransactionDto>>
{
    private readonly IApplicationDbContext _db;
    public GetMyLoyaltyTransactionsHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<PointsTransactionDto>> Handle(GetMyLoyaltyTransactionsQuery req, CancellationToken ct)
    {
        var acct = await _db.LoyaltyAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == req.UserId, ct);
        if (acct is null) return new();

        return await _db.PointsTransactions.AsNoTracking()
            .Where(t => t.LoyaltyAccountId == acct.Id)
            .OrderByDescending(t => t.Timestamp)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(t => new PointsTransactionDto(t.Id, t.Timestamp, t.Type.ToString(), t.Points, t.Description))
            .ToListAsync(ct);
    }
}

// ---------- Commands (used internally + by admin) ----------
public record AwardPointsCommand(string UserId, int Points, PointsTransactionType Type, string Description, Guid? OrderId = null) : IRequest;

public class AwardPointsHandler : IRequestHandler<AwardPointsCommand>
{
    private readonly IApplicationDbContext _db;
    public AwardPointsHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(AwardPointsCommand req, CancellationToken ct)
    {
        var acct = await _db.LoyaltyAccounts.FirstOrDefaultAsync(a => a.UserId == req.UserId, ct);
        if (acct is null)
        {
            acct = new LoyaltyAccount { UserId = req.UserId, Balance = 0, LifetimeEarned = 0, LifetimeSpent = 0 };
            _db.LoyaltyAccounts.Add(acct);
            await _db.SaveChangesAsync(ct);
        }
        acct.Balance += req.Points;
        if (req.Points > 0) acct.LifetimeEarned += req.Points;
        else acct.LifetimeSpent += Math.Abs(req.Points);

        _db.PointsTransactions.Add(new PointsTransaction
        {
            LoyaltyAccountId = acct.Id, Type = req.Type, Points = req.Points,
            Description = req.Description, OrderId = req.OrderId,
        });
        await _db.SaveChangesAsync(ct);
    }
}
