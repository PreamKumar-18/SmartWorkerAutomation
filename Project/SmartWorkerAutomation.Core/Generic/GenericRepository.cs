//using SmartWorkerAutomation.Common.Common;
//using SmartWorkerAutomation.Core.DBContext;
//using Microsoft.EntityFrameworkCore;
//using System.Linq.Expressions;
//using System.Linq.Dynamic.Core;

//namespace SmartWorkerAutomation.Core.Generic;

//public class GenericRepository<TEntity> : IGenericRepository<TEntity> where TEntity : class
//{
//    internal SmartWorkerAutomationContext _dbContext;
//    internal DbSet<TEntity> _dbSet;

//    public GenericRepository(SmartWorkerAutomationContext dbContext)
//    {
//        _dbContext = dbContext;
//        _dbSet = _dbContext.Set<TEntity>();
//    }

//    public virtual async Task<TEntity> GetByIdAsync(object id) => await _dbSet.FindAsync(id);

//    public virtual void Insert(TEntity entity) => _dbSet.Add(entity);
//    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate)
//    {
//        return await _dbSet.AnyAsync(predicate);
//    }
//    public virtual void InsertMany(IEnumerable<TEntity> entities)
//    {
//        foreach (var entity in entities)
//        {
//            Insert(entity);
//        }
//    }

//    public virtual void Update(TEntity entity) => _dbSet.Update(entity);
//    public async Task<IEnumerable<TEntity>> GetAllAsync()
//    {
//        return await _dbSet.ToListAsync();
//    }
//    public IQueryable<TEntity> GetAllQueryable(Expression<Func<TEntity, bool>>? predicate = null)
//    {
//        if (predicate != null)
//        {
//            return _dbSet.Where(predicate).AsQueryable();
//        }

//        return _dbSet.AsQueryable();
//    }
//    public virtual void UpdateMany(IEnumerable<TEntity> entities)
//    {
//        foreach (var entity in entities)
//        {
//            Update(entity);
//        }
//    }
//    public async Task AddAsync(TEntity entity)
//    {
//        await _dbSet.AddAsync(entity);
//        await SaveChangesAsync();
//    }
//    public async Task UpdateAsync(TEntity entity)
//    {
//        _dbSet.Update(entity);
//        await SaveChangesAsync();
//    }
//    public virtual void Delete(TEntity entity) => _dbSet.Remove(entity);

//    public virtual async Task<int> SaveChangesAsync() => await _dbContext.SaveChangesAsync();

//    public virtual async Task<int> SearchCountListAsync(IList<Expression<Func<TEntity, bool>>> filters = null, bool changeTrackingEnabled = true)
//        => await SearchCountAsync(GetWhereCondition(filters, changeTrackingEnabled));

//    public virtual async Task<int> SearchCountAsync(Expression<Func<TEntity, bool>> filters = null, bool changeTrackingEnabled = true)
//        => await SearchCountAsync(GetWhereCondition(filters, changeTrackingEnabled));

//    private async Task<int> SearchCountAsync(IQueryable<TEntity> query) => await query.CountAsync();

//    public virtual async Task<List<TEntity>> SearchAsync(Expression<Func<TEntity, bool>> filters = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
//        string includeProperties = "", int pageIndex = -1, int pageSize = -1, bool changeTrackingEnabled = true)
//        => await SearchAsync(GetWhereCondition(filters, changeTrackingEnabled), orderBy, includeProperties, pageIndex, pageSize, changeTrackingEnabled);

//    public virtual async Task<List<TEntity>> SearchAsync(IList<Expression<Func<TEntity, bool>>> filters = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
//        string includeProperties = "", int pageIndex = -1, int pageSize = -1, bool changeTrackingEnabled = true)
//        => await SearchAsync(GetWhereCondition(filters, changeTrackingEnabled), orderBy, includeProperties, pageIndex, pageSize, changeTrackingEnabled);

//    public virtual async Task<TEntity> SearchTop1Async(IList<Expression<Func<TEntity, bool>>> filters = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
//        string includeProperties = "", int pageIndex = -1, int pageSize = -1, bool changeTrackingEnabled = true)
//        => await SearchTop1Async(GetWhereCondition(filters, changeTrackingEnabled), orderBy, includeProperties, pageIndex, pageSize, changeTrackingEnabled);

//    public virtual async Task<TEntity> SearchTop1Async(Expression<Func<TEntity, bool>> filters = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
//        string includeProperties = "", int pageIndex = -1, int pageSize = -1, bool changeTrackingEnabled = true)
//        => await SearchTop1Async(GetWhereCondition(filters, changeTrackingEnabled), orderBy, includeProperties, pageIndex, pageSize, changeTrackingEnabled);

//    public virtual async Task<List<TEntity>> GetPageinatedDataAsync(Paging pagingConfig, IList<Expression<Func<TEntity, bool>>> filters = null, string includeProperties = "", bool changeTrackingEnabled = true)
//        => await GetPageinatedDataAsync(pagingConfig, GetWhereCondition(filters, changeTrackingEnabled), includeProperties, changeTrackingEnabled);

//    public virtual async Task<List<TEntity>> GetPageinatedDataAsync(Paging pagingConfig, Expression<Func<TEntity, bool>> filters = null, string includeProperties = "", bool changeTrackingEnabled = true)
//        => await GetPageinatedDataAsync(pagingConfig, GetWhereCondition(filters, changeTrackingEnabled), includeProperties, changeTrackingEnabled);

//    private async Task<List<TEntity>> SearchAsync(IQueryable<TEntity> query, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
//        string includeProperties = "", int pageIndex = -1, int pageSize = -1, bool changeTrackingEnabled = true)
//    {
//        int skipCount = (pageIndex - 1) * pageSize;

//        foreach (var includeProperty in includeProperties.Split(
//            new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
//        {
//            query = query.Include(includeProperty);
//        }

//        if (orderBy != null)
//        {
//            if (pageIndex > 0 && pageSize > 0)
//                query = orderBy(query).Skip(skipCount).Take(pageSize);
//            else
//                query = orderBy(query);
//        }
//        else
//        {
//            if (pageIndex > 0 && pageSize > 0)
//                query = query.Skip(skipCount).Take(pageSize);
//        }

//        await DetachEntity(query, changeTrackingEnabled);

//        return await query.ToListAsync();
//    }

//    private async Task<TEntity> SearchTop1Async(IQueryable<TEntity> query, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
//        string includeProperties = "", int pageIndex = -1, int pageSize = -1, bool changeTrackingEnabled = true)
//    {
//        int skipCount = (pageIndex - 1) * pageSize;

//        foreach (var includeProperty in includeProperties.Split(
//            new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
//        {
//            query = query.Include(includeProperty);
//        }

//        if (orderBy != null)
//        {
//            if (pageIndex > 0 && pageSize > 0)
//                query = orderBy(query).Skip(skipCount).Take(pageSize);
//            else
//                query = orderBy(query);
//        }
//        else
//        {
//            if (pageIndex > 0 && pageSize > 0)
//                query = query.Skip(skipCount).Take(pageSize);
//        }

//        TEntity entity = await query?.FirstOrDefaultAsync();

//        DetachEntity(entity, changeTrackingEnabled);

//        return entity;
//    }

//    private async Task<List<TEntity>> GetPageinatedDataAsync(Paging pagingConfig, IQueryable<TEntity> query,
//        string includeProperties = "", bool changeTrackingEnabled = true)
//    {
//        pagingConfig.TotalRecords = await query.CountAsync();

//        if (pagingConfig.TotalRecords == 0) return new();

//        if (pagingConfig.PageSize > pagingConfig.MaxPageSize)
//            pagingConfig.PageSize = pagingConfig.MaxPageSize;

//        pagingConfig.PageCount = Convert.ToInt32(Math.Ceiling((float)pagingConfig.TotalRecords / pagingConfig.PageSize));

//        if (pagingConfig.PageIndex > pagingConfig.PageCount)
//            throw new Exception("Invalid Index : index is greater that page count");
//        else if (pagingConfig.PageIndex < 1)
//            throw new Exception("Invalid Index : index should greater than 1");

//        foreach (var includeProperty in includeProperties.Split(
//            new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
//        {
//            query = query.Include(includeProperty);
//        }

//        if (!string.IsNullOrEmpty(pagingConfig.SortBy))
//        {
//            query = query.OrderBy($"{pagingConfig.SortBy} {(pagingConfig.SortAsc ? "ascending" : "descending")}");

//            if (pagingConfig.PageIndex > 0 && pagingConfig.PageSize > 0)
//                query = query.Skip((pagingConfig.PageIndex - 1) * pagingConfig.PageSize).Take(pagingConfig.PageSize);
//        }
//        else
//        {
//            var property = typeof(TEntity).GetProperties().FirstOrDefault();

//            if (property == null)
//                throw new Exception("Entity should contain property");

//            query = query.OrderBy($"{property.Name} {(pagingConfig.SortAsc ? "ascending" : "descending")}");

//            if (pagingConfig.PageIndex > 0 && pagingConfig.PageSize > 0)
//                query = query.Skip((pagingConfig.PageIndex - 1) * pagingConfig.PageSize).Take(pagingConfig.PageSize);
//        }

//        await DetachEntity(query, changeTrackingEnabled);

//        return await query.ToListAsync();
//    }

//    private IQueryable<TEntity> GetWhereCondition(IList<Expression<Func<TEntity, bool>>> filters = null, bool changeTrackingEnabled = true)
//    {
//        IQueryable<TEntity> query = changeTrackingEnabled ? _dbSet : _dbSet.AsNoTrackingWithIdentityResolution();

//        if (filters != null && filters.Any())
//        {
//            foreach (var filter in filters)
//            {
//                query = query.Where(filter);
//            }
//        }

//        return query;
//    }

//    private IQueryable<TEntity> GetWhereCondition(Expression<Func<TEntity, bool>> filter = null, bool changeTrackingEnabled = true)
//    {
//        IQueryable<TEntity> query = changeTrackingEnabled ? _dbSet : _dbSet.AsNoTrackingWithIdentityResolution();

//        if (filter != null)
//        {
//            query = query.Where(filter);
//        }
//        var res = query.ToQueryString();
//        return query;
//    }

//    private async Task DetachEntity(IQueryable<TEntity>? query = null, bool changeTrackingEnabled = true)
//    {
//        if (query != null)
//            if (!changeTrackingEnabled)
//                if (_dbContext?.Model?.FindEntityType(typeof(TEntity))?.GetKeys()?.FirstOrDefault(x => x.IsPrimaryKey()) != null)
//                    if (await query.CountAsync() > 0)
//                        foreach (var entity in query)
//                            _dbContext.Entry(entity).State = EntityState.Detached;
//    }

//    private void DetachEntity(TEntity entity = null, bool changeTrackingEnabled = true)
//    {
//        if (entity != null)
//            if (!changeTrackingEnabled)
//                if (_dbContext?.Model?.FindEntityType(typeof(TEntity))?.GetKeys()?.FirstOrDefault(x => x.IsPrimaryKey()) != null)
//                    _dbContext.Entry(entity).State = EntityState.Detached;
//    }

//    public async Task RemoveAllAsync(Expression<Func<TEntity, bool>> predicate)
//    {
//        var entities = await _dbContext.Set<TEntity>().Where(predicate).ToListAsync();

//        if (entities.Any())
//        {
//            _dbContext.Set<TEntity>().RemoveRange(entities);
//            await _dbContext.SaveChangesAsync();
//        }

//    }

//}
