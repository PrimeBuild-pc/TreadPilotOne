using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading.Tasks;
using ThreadPilot.Models.Core;

namespace ThreadPilot.Data
{
    /// <summary>
    /// JSON file-based repository implementation
    /// </summary>
    /// <typeparam name="T">Entity type that implements IModel</typeparam>
    public class JsonRepository<T> : IRepository<T> where T : class, IModel
    {
        private readonly string _filePath;
        private readonly ILogger<JsonRepository<T>> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _fileLock = new(1, 1);

        public JsonRepository(string filePath, ILogger<JsonRepository<T>> logger)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public async Task<T?> GetByIdAsync(string id)
        {
            var entities = await GetAllAsync();
            return entities.FirstOrDefault(e => e.Id == id);
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                if (!File.Exists(_filePath))
                {
                    return new List<T>();
                }

                var json = await File.ReadAllTextAsync(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<T>();
                }

                var entities = JsonSerializer.Deserialize<List<T>>(json, _jsonOptions);
                return entities ?? new List<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read entities from {FilePath}", _filePath);
                return new List<T>();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            var entities = await GetAllAsync();
            var compiledPredicate = predicate.Compile();
            return entities.Where(compiledPredicate);
        }

        public async Task<T> AddAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var entities = (await GetAllAsync()).ToList();
            
            // Check if entity already exists
            if (entities.Any(e => e.Id == entity.Id))
            {
                throw new InvalidOperationException($"Entity with ID {entity.Id} already exists");
            }

            entities.Add(entity);
            await SaveAllAsync(entities);
            
            _logger.LogDebug("Added entity {EntityType} with ID {EntityId}", typeof(T).Name, entity.Id);
            return entity;
        }

        public async Task<T> UpdateAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var entities = (await GetAllAsync()).ToList();
            var existingIndex = entities.FindIndex(e => e.Id == entity.Id);
            
            if (existingIndex == -1)
            {
                throw new InvalidOperationException($"Entity with ID {entity.Id} not found");
            }

            entities[existingIndex] = entity;
            await SaveAllAsync(entities);
            
            _logger.LogDebug("Updated entity {EntityType} with ID {EntityId}", typeof(T).Name, entity.Id);
            return entity;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var entities = (await GetAllAsync()).ToList();
            var entityToRemove = entities.FirstOrDefault(e => e.Id == id);
            
            if (entityToRemove == null)
            {
                return false;
            }

            entities.Remove(entityToRemove);
            await SaveAllAsync(entities);
            
            _logger.LogDebug("Deleted entity {EntityType} with ID {EntityId}", typeof(T).Name, id);
            return true;
        }

        public async Task<bool> DeleteAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return await DeleteAsync(entity.Id);
        }

        public async Task<bool> ExistsAsync(string id)
        {
            var entities = await GetAllAsync();
            return entities.Any(e => e.Id == id);
        }

        public async Task<int> CountAsync()
        {
            var entities = await GetAllAsync();
            return entities.Count();
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
        {
            var entities = await FindAsync(predicate);
            return entities.Count();
        }

        private async Task SaveAllAsync(IEnumerable<T> entities)
        {
            await _fileLock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(entities, _jsonOptions);
                await File.WriteAllTextAsync(_filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save entities to {FilePath}", _filePath);
                throw;
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }
}
