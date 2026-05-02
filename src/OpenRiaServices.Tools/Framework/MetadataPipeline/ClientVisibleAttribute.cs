using System;

namespace OpenRiaServices.Client
{
    /// <summary>
    /// 内部使用的 ClientVisibleAttribute 类，用于代码生成时生成正确的类型名称。
    /// 这个类只在代码生成器内部使用，实际的客户端实现位于 OpenRiaServices.Client 程序集中。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    internal sealed class ClientVisibleAttribute : Attribute
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
