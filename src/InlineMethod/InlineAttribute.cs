using System;

namespace InlineMethod
{
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
        /// Initialize a new instance
        /// </summary>
        /// <param name="behavior"></param>
        public InlineAttribute(InlineBehavior behavior = InlineBehavior.RemovePrivate)
        {
            Behavior = behavior;
        }
    }
}
