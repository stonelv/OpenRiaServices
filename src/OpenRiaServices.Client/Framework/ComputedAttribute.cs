using System;
using System.Runtime.Serialization;

namespace OpenRiaServices.Client
{
    /// <summary>
    /// 标记一个属性为计算属性。计算属性仅用于客户端计算展示，
    /// 自动排除出变更追踪和序列化，不会提交到服务端。
    /// </summary>
    /// <remarks>
    /// 应用此特性的属性将具有以下行为：
    /// 1. 不会被包含在变更追踪中
    /// 2. 不会被序列化到服务端
    /// 3. 不会被提交到服务端
    /// 4. 仅用于客户端的计算和展示目的
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class ComputedAttribute : Attribute
    {
    }
}