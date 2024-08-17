using System;

namespace InlineMethod;

/// <summary>
/// Method to inline
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class InlineAttribute : Attribute
{
    /// <summary>
    /// InlineMethod behavior
    /// </summary>
    public InlineBehavior Behavior { get; }

    /// <summary>
    /// Export attribute
    /// </summary>
    public bool Export { get; }

    /// <summary>
    /// Initialize a new instance
    /// </summary>
    /// <param name="behavior"></param>
    /// <param name="export"></param>
    public InlineAttribute(InlineBehavior behavior = InlineBehavior.RemovePrivate, bool export = false)
    {
        Behavior = behavior;
        Export = export;
    }
}