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
        /// Remove method after inline (if private)
        /// </summary>
        public bool Remove { get; }

        /// <summary>
        /// Initialize a new instance
        /// </summary>
        /// <param name="remove"></param>
        public InlineAttribute(bool remove = true)
        {
            Remove = remove;
        }
    }
}
