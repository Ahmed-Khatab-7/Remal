using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Remal.Application.Common.Interfaces;

namespace Remal.Infrastructure.Persistence.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    private readonly ApplicationDbContext _db;
    private readonly DbSet<T> _set;

    public Repository(ApplicationDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public Task<T?> GetByIdAsync(object id, CancellationToken ct = default) => _set.FindAsync([id], ct).AsTask();

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        _set.AsNoTracking().FirstOrDefaultAsync(predicate, ct);

    public async Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
    {
        var q = _set.AsNoTracking();
        if (predicate != null) q = q.Where(predicate);
        return await q.ToListAsync(ct);
    }

    public IQueryable<T> Query(bool tracked = false) => tracked ? _set : _set.AsNoTracking();

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        _set.AnyAsync(predicate, ct);

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default) =>
        predicate is null ? _set.CountAsync(ct) : _set.CountAsync(predicate, ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) => await _set.AddAsync(entity, ct);
    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) => await _set.AddRangeAsync(entities, ct);

    public void Update(T entity) => _set.Update(entity);
    public void Remove(T entity) => _set.Remove(entity);
    public void RemoveRange(IEnumerable<T> entities) => _set.RemoveRange(entities);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _db;
    private readonly ConcurrentDictionary<Type, object> _repos = new();
    private IDbContextTransaction? _tx;

    public UnitOfWork(ApplicationDbContext db) => _db = db;

    public IRepository<T> Repository<T>() where T : class =>
        (IRepository<T>)_repos.GetOrAdd(typeof(T), _ => new Repository<T>(_db));

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        _tx ??= await _db.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_tx is null) return;
        await _tx.CommitAsync(ct);
        await _tx.DisposeAsync();
        _tx = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_tx is null) return;
        await _tx.RollbackAsync(ct);
        await _tx.DisposeAsync();
        _tx = null;
    }
}
