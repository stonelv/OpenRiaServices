using System;

namespace OpenRiaServices.Client
{
    /// <summary>
    /// Indicates that an entity property is visible to client-side logic.
    /// This attribute is generated on client-side entity properties based on 
    /// the server-side ClientVisibleAttribute usage.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class ClientVisibleAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the ClientVisibleAttribute class.
        /// </summary>
        public ClientVisibleAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the ClientVisibleAttribute class.
        /// </summary>
        /// <param name="isVisible">True to mark the property as visible.</param>
        public ClientVisibleAttribute(bool isVisible)
        {
            this.IsVisible = isVisible;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the property is visible.
        /// </summary>
        public bool IsVisible
        {
            get;
            set;
        }
    }
}
