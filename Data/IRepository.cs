using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ThreadPilot.Data
{
    /// <summary>
    /// Generic repository interface for data access operations
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <typeparam name="TKey">Key type</typeparam>
    public interface IRepository<T, TKey> where T : class
    {
        /// <summary>
        /// Get entity by ID
        /// </summary>
        Task<T?> GetByIdAsync(TKey id);

        /// <summary>
        /// Get all entities
        /// </summary>
        Task<IEnumerable<T>> GetAllAsync();

        /// <summary>
        /// Find entities matching predicate
        /// </summary>
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Add new entity
        /// </summary>
        Task<T> AddAsync(T entity);

        /// <summary>
        /// Update existing entity
        /// </summary>
        Task<T> UpdateAsync(T entity);

        /// <summary>
        /// Delete entity by ID
        /// </summary>
        Task<bool> DeleteAsync(TKey id);

        /// <summary>
        /// Delete entity
        /// </summary>
        Task<bool> DeleteAsync(T entity);

        /// <summary>
        /// Check if entity exists
        /// </summary>
        Task<bool> ExistsAsync(TKey id);

        /// <summary>
        /// Get count of entities
        /// </summary>
        Task<int> CountAsync();

        /// <summary>
        /// Get count of entities matching predicate
        /// </summary>
        Task<int> CountAsync(Expression<Func<T, bool>> predicate);
    }

    /// <summary>
    /// Repository interface for entities with string keys
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    public interface IRepository<T> : IRepository<T, string> where T : class
    {
    }

    /// <summary>
    /// Unit of work pattern for coordinating multiple repository operations
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Get repository for entity type
        /// </summary>
        IRepository<T> GetRepository<T>() where T : class;

        /// <summary>
        /// Save all changes
        /// </summary>
        Task<int> SaveChangesAsync();

        /// <summary>
        /// Begin transaction
        /// </summary>
        Task BeginTransactionAsync();

        /// <summary>
        /// Commit transaction
        /// </summary>
        Task CommitTransactionAsync();

        /// <summary>
        /// Rollback transaction
        /// </summary>
        Task RollbackTransactionAsync();
    }
}
