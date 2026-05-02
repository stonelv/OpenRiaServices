using System;

namespace OpenRiaServices.Server
{
    /// <summary>
    /// Indicates whether an entity property should be marked with [ClientVisible] attribute 
    /// in the generated client code. When applied to a property, it controls whether the 
    /// [ClientVisible] attribute is generated on the client-side entity property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class ClientVisibleAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the ClientVisibleAttribute class.
        /// </summary>
        /// <param name="isVisible">True to generate [ClientVisible] attribute, false to omit it.</param>
        public ClientVisibleAttribute(bool isVisible)
        {
            this.IsVisible = isVisible;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the [ClientVisible] attribute should be generated.
        /// </summary>
        public bool IsVisible
        {
            get;
            set;
        }
    }
}
