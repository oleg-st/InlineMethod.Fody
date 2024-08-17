using System;

namespace InlineMethod;

/// <summary>
/// Resolve delegate parameter
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class ResolveDelegateAttribute : Attribute
{
    /// <summary>
    /// Inline after resolve
    /// </summary>
    public bool Inline { get; }

    /// <summary>
    /// Initialize a new instance
    /// </summary>
    public ResolveDelegateAttribute(bool inline = true)
    {
        Inline = inline;
    }
}