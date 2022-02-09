using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
			var method = typeof(NMSExtensions).GetMethod(nameof(TryWriteDynamicValue));
			var genMethod = method.MakeGenericMethod(type);
			object[] args = new object[] { root, name, value };
			
			return (bool)genMethod.Invoke(null, args);
			//value = Convert.ChangeType(method.Invoke(null, new[] { value }), paramType);
		}
	}
}
