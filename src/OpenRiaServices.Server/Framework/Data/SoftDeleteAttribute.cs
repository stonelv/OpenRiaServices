using System;

namespace OpenRiaServices.Server
{
    /// <summary>
    /// Attribute applied to an entity type to indicate that it supports soft deletion.
    /// Soft deletion means that entities are not permanently deleted from the data store,
    /// but instead marked as deleted through a flag field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public sealed class SoftDeleteAttribute : Attribute
    {
        private const string DefaultPropertyName = "IsDeleted";

        /// <summary>
        /// Initializes a new instance of the <see cref="SoftDeleteAttribute"/> class
        /// using the default property name "IsDeleted".
        /// </summary>
        public SoftDeleteAttribute()
        {
            this.PropertyName = DefaultPropertyName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SoftDeleteAttribute"/> class
        /// with the specified property name.
        /// </summary>
        /// <param name="propertyName">The name of the property that serves as the soft delete flag.</param>
        public SoftDeleteAttribute(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            this.PropertyName = propertyName;
        }

        /// <summary>
        /// Gets the name of the property that serves as the soft delete flag.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the soft delete property should be automatically 
        /// included in queries when the entity is marked for deletion.
        /// </summary>
        /// <remarks>
        /// When true, queries will automatically filter out soft-deleted entities.
        /// Set to false if you want to handle filtering manually.
        /// </remarks>
        public bool AutoFilterEnabled { get; set; } = true;
    }
}
