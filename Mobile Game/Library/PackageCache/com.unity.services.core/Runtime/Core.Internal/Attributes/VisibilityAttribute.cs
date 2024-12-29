
using System;
using UnityEngine;

namespace Unity.Services.Core.Internal
{
    /// <summary>
    /// Sets a criteria for the visibility of a property in the inspector
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public class VisibilityAttribute : PropertyAttribute
    {
        /// <summary>
        /// The property name to validate against
        /// </summary>
        public string PropertyName { get; private set; }

        /// <summary>
        /// The value to validate against
        /// </summary>
        public object Value { get; private set; }

        /// <summary>
        /// Sets a criteria for the visibility of a property in the inspector
        /// </summary>
        /// <param name="propertyName">The property name to validate against</param>
        /// <param name="value">The value to compare to</param>
        public VisibilityAttribute(string propertyName, object value)
        {
            PropertyName = propertyName;
            Value = value;
        }
    }
}
