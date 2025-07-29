using System;
using System.ComponentModel;

namespace ThreadPilot.Models.Core
{
    /// <summary>
    /// Base interface for all domain models
    /// </summary>
    public interface IModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Unique identifier for the model instance
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Timestamp when the model was created
        /// </summary>
        DateTime CreatedAt { get; }

        /// <summary>
        /// Timestamp when the model was last updated
        /// </summary>
        DateTime UpdatedAt { get; }

        /// <summary>
        /// Validate the model state
        /// </summary>
        ValidationResult Validate();

        /// <summary>
        /// Create a copy of the model
        /// </summary>
        IModel Clone();
    }

    /// <summary>
    /// Validation result for model validation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; }
        public string[] Errors { get; }

        public ValidationResult(bool isValid, params string[] errors)
        {
            IsValid = isValid;
            Errors = errors ?? Array.Empty<string>();
        }

        public static ValidationResult Success() => new(true);
        public static ValidationResult Failure(params string[] errors) => new(false, errors);
    }

    /// <summary>
    /// Base implementation for domain models
    /// </summary>
    public abstract class BaseModel : IModel
    {
        public string Id { get; protected set; }
        public DateTime CreatedAt { get; protected set; }
        public DateTime UpdatedAt { get; protected set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected BaseModel()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        protected BaseModel(string id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            UpdatedAt = DateTime.UtcNow;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value)) return false;
            
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public abstract ValidationResult Validate();
        public abstract IModel Clone();
    }
}
