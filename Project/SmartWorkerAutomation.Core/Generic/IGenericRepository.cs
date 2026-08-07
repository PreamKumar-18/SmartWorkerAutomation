using SmartWorkerAutomation.Common.Common;
using System.Linq.Expressions;

namespace SmartWorkerAutomation.Core.Generic;

public interface IGenericRepository<TEntity>
{
    Task<TEntity> GetByIdAsync(object id);

    void Insert(TEntity entity);

    void InsertMany(IEnumerable<TEntity> entities);

    void Update(TEntity entity);
    Task UpdateAsync(TEntity entity);

    void UpdateMany(IEnumerable<TEntity> entities);

    void Delete(TEntity entity);
    IQueryable<TEntity> GetAllQueryable(Expression<Func<TEntity, bool>>? predicate = null);

    Task<int> SaveChangesAsync();
    Task AddAsync(TEntity entity);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate);
    Task<IEnumerable<TEntity>> GetAllAsync();

    Task<int> SearchCountListAsync(IList<Expression<Func<TEntity, bool>>> filters = null, bool changeTrackingEnabled = true);

    Task<int> SearchCountAsync(Expression<Func<TEntity, bool>> filters = null, bool changeTrackingEnabled = true);

    Task<List<TEntity>> SearchAsync(Expression<Func<TEntity, bool>> filters = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
      string includeProperties = "", int pageIndex = -1, int pageSize = -1, bool changeTrackingEnabled = true);

    Task<List<TEntity>> SearchAsync(IList<Expression<Func<TEntity, bool>>> filters = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
      string includeProperties = "", int pageIndex = -1, int pageSize = -1, bool changeTrackingEnabled = true);

    Task<TEntity> SearchTop1Async(IList<Expression<Func<TEntity, bool>>> filters = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
      string includeProperties = "", int pageIndex = -1, int pageSize = -1, bool changeTrackingEnabled = true);

    Task<TEntity> SearchTop1Async(Expression<Func<TEntity, bool>> filters = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
      string includeProperties = "", int pageIndex = -1, int pageSize = -1, bool changeTrackingEnabled = true);

    Task<List<TEntity>> GetPageinatedDataAsync(Paging pagingConfig, IList<Expression<Func<TEntity, bool>>> filters = null, string includeProperties = "", bool changeTrackingEnabled = true);

    Task<List<TEntity>> GetPageinatedDataAsync(Paging pagingConfig, Expression<Func<TEntity, bool>> filters = null, string includeProperties = "", bool changeTrackingEnabled = true);

    Task RemoveAllAsync(Expression<Func<TEntity, bool>> predicate);

}
