using System;
using System.IO;
using Unity.Services.Core.Internal;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Services.Core.Editor
{
#if UNITY_2022_3_OR_NEWER
    [CustomPropertyDrawer(typeof(VisibilityAttribute))]
    class VisibilityPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var visibility = attribute as VisibilityAttribute;
            var path = GetPropertyPath(visibility.PropertyName, property.propertyPath);
            var target = property.serializedObject.FindProperty(path);

            var element = new PropertyField(property);
            element.TrackPropertyValue(target, (targetProperty) => { UpdateVisibility(element, targetProperty); });
            UpdateVisibility(element, target);
            return element;
        }

        void UpdateVisibility(VisualElement element, SerializedProperty target)
        {
            var visibility = attribute as VisibilityAttribute;
            var visible = target != null && IsConditionMet(target, visibility);
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        string GetPropertyPath(string propertyName, string propertyPath)
        {
            return propertyPath.Contains(".") ? Path.ChangeExtension(propertyPath, propertyName) : propertyName;
        }

        bool IsConditionMet(SerializedProperty target, VisibilityAttribute visibility)
        {
            switch (target.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return target.boolValue.Equals(visibility.Value);
                case SerializedPropertyType.Enum:
                    return target.enumValueIndex.Equals((int)visibility.Value);
                default:
                    return target.managedReferenceValue == visibility.Value;
            }
        }
    }
#else
    [CustomPropertyDrawer(typeof(VisibilityAttribute))]
    class VisibilityPropertyDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var visibility = attribute as VisibilityAttribute;
            var path = GetPropertyPath(visibility.PropertyName, property.propertyPath);
            var target = property.serializedObject.FindProperty(path);

            if (target != null && IsConditionMet(target, visibility))
            {
                return EditorGUI.GetPropertyHeight(property, label, true);
            }

            return 0f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var visibility = attribute as VisibilityAttribute;
            var path = GetPropertyPath(visibility.PropertyName, property.propertyPath);
            var target = property.serializedObject.FindProperty(path);

            if (target != null && IsConditionMet(target, visibility))
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        string GetPropertyPath(string propertyName, string propertyPath)
        {
            return propertyPath.Contains(".") ? Path.ChangeExtension(propertyPath, propertyName) : propertyName;
        }

        private bool IsConditionMet(SerializedProperty target, VisibilityAttribute visibility)
        {
            switch (target.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return target.boolValue.Equals(visibility.Value);
                case SerializedPropertyType.Enum:
                    return target.enumValueIndex.Equals((int)visibility.Value);
                default:
                    return target.managedReferenceValue == visibility.Value;
            }
        }
    }
#endif
}
