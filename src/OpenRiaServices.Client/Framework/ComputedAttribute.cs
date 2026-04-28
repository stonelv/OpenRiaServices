using System;

namespace OpenRiaServices.Client
{
    /// <summary>
    /// Attribute used to indicate that a property is a computed property. 
    /// Computed properties are used for client-side calculation and display only.
    /// They are automatically excluded from change tracking, serialization, 
    /// and will not be submitted to the server.
    /// </summary>
    /// <remarks>
    /// Properties marked with this attribute will have the following behaviors:
    /// 1. Not included in change tracking
    /// 2. Not serialized to the server
    /// 3. Not submitted to the server
    /// 4. Used only for client-side calculation and display purposes
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class ComputedAttribute : Attribute
    {
    }
}