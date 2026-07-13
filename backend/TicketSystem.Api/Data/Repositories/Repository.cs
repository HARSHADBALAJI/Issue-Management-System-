using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace TicketSystem.Api.Data.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly TicketSystemDbContext Context;
    protected readonly DbSet<T> DbSet;

    public Repository(TicketSystemDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id) => await DbSet.FindAsync(id);

    public virtual async Task<List<T>> GetAllAsync() => await DbSet.ToListAsync();

    public virtual async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate)
        => await DbSet.Where(predicate).ToListAsync();

    public virtual async Task<T> AddAsync(T entity)
    {
        await DbSet.AddAsync(entity);
        await Context.SaveChangesAsync();
        return entity;
    }

    public virtual async Task UpdateAsync(T entity)
    {
        DbSet.Update(entity);
        await Context.SaveChangesAsync();
    }

    public virtual async Task DeleteAsync(T entity)
    {
        DbSet.Remove(entity);
        await Context.SaveChangesAsync();
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
        => predicate == null ? await DbSet.CountAsync() : await DbSet.CountAsync(predicate);

    public virtual IQueryable<T> Query() => DbSet.AsQueryable();
}
