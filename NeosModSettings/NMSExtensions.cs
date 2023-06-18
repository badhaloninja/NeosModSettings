﻿using System;
using FrooxEngine;
using FrooxEngine.UIX;
using BaseX;
namespace NeosModSettings
{
    public static class NMSExtensions
    {
		public static ButtonValueCycle<T> SetupValueToggle<T>(this Button button, IField<T> target, T value, OptionDescription<T>? enabled, OptionDescription<T>? disabled)
		{ // SetupValueCycle does not seem to have a way to set a fallback for the DescriptionDriver
			ButtonValueCycle<T> buttonValueCycle = button.Slot.AttachComponent<ButtonValueCycle<T>>();
			buttonValueCycle.TargetValue.Target = target;
			buttonValueCycle.Values.Add(value);
			buttonValueCycle.Values.Add(Coder<T>.Default);
			if (enabled != null)
			{
				ValueOptionDescriptionDriver<T> valueOptionDescriptionDriver = button.EnsureOptionDescriptionDriver(target, ((enabled != null) ? enabled.GetValueOrDefault().label.content : null) != null || ((disabled != null) ? disabled.GetValueOrDefault().label.content : null) != null, false, null);

				ValueOptionDescriptionDriver<T>.Option option = valueOptionDescriptionDriver.Options.Add();
				option.SetupFrom(enabled.Value);
				option.ReferenceValue.Value = value;
				if (disabled != null)
				{
					valueOptionDescriptionDriver.DefaultOption.SetupFrom(disabled.Value);
				}
			}
			return buttonValueCycle;
		}

		public static bool TryWriteDynamicValue<T>(this Slot root, string name, T value)
		{
			DynamicVariableHelper.ParsePath(name, out string spaceName, out string text);

			if (string.IsNullOrEmpty(text)) return false;

			DynamicVariableSpace dynamicVariableSpace = root.FindSpace(spaceName);
			if (dynamicVariableSpace == null) return false;
			return dynamicVariableSpace.TryWriteValue<T>(text, value);
		}
		public static bool TryWriteDynamicType(this Slot root, string name, Type value)
		{
			DynamicVariableHelper.ParsePath(name, out string spaceName, out string text);

			if (string.IsNullOrEmpty(text)) return false;

			DynamicVariableSpace dynamicVariableSpace = root.FindSpace(spaceName);
			if (dynamicVariableSpace == null) return false;
			if(dynamicVariableSpace.TryReadValue(text, out SyncType typeField))
            {
				if (typeField == null) return false;
				typeField.Value = value;
				return true;
            }
			return false;
		}
		public static bool TryReadDynamicValue<T>(this Slot root, string name, out T value)
		{
			value = Coder<T>.Default;
			DynamicVariableHelper.ParsePath(name, out string spaceName, out string text);

			if (string.IsNullOrEmpty(text)) return false;

			DynamicVariableSpace dynamicVariableSpace = root.FindSpace(spaceName);
			if (dynamicVariableSpace == null) return false;
			return dynamicVariableSpace.TryReadValue<T>(text, out value);
		}



		public static bool TryWriteDynamicValueOfType(this Slot root, Type type, string name, object value)
		{
			if (type == typeof(Type)) return root.TryWriteDynamicType(name, (Type)value);

			var method = typeof(NMSExtensions).GetMethod(nameof(TryWriteDynamicValue));
			var genMethod = method.MakeGenericMethod(type);
			object[] args = new object[] { root, name, value };
			
			return (bool)genMethod.Invoke(null, args);
        }



        public static void SyncWriteDynamicValue<T>(this Slot root, string name, T value)
        {
            if (root.World.ConnectorManager.CanCurrentThreadModify) // Check if current thread can interact with data model
            { // Try to update config field
                root.TryWriteDynamicValue(name, value);
                return;
            }
            root.RunSynchronously(() => // Move to thread that can interact with data model
            { // Try to update config field
                root.TryWriteDynamicValue(name, value);
            });
        }
        public static void SyncWriteDynamicValueType(this Slot root, Type type, string name, object value)
		{
            if (root.World.ConnectorManager.CanCurrentThreadModify) // Check if current thread can interact with data model
            { // Try to update config field
                root.TryWriteDynamicValueOfType(type, name, value);
                return;
            }
            root.RunSynchronously(() => // Move to thread that can interact with data model
            { // Try to update config field
                root.TryWriteDynamicValueOfType(type, name, value);
            });
        }
    }
}
