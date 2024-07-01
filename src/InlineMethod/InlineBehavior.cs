namespace InlineMethod;

/// <summary>
/// InlineMethod behavior
/// </summary>
public enum InlineBehavior
{
    /// <summary>
    /// Keep method after inline
    /// </summary>
    Keep,
    /// <summary>
    /// Remove method after inline (if private)
    /// </summary>
    RemovePrivate,
    /// <summary>
    /// Remove method after inline
    /// </summary>
    Remove
}
