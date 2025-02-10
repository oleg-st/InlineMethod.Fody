using System;

namespace InlineMethod;

/// <summary>
/// Inline parameter usage
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class InlineParameterAttribute : Attribute
{
    /// <summary>
    /// Initialize a new instance
    /// </summary>
    public InlineParameterAttribute()
    {
    }
}