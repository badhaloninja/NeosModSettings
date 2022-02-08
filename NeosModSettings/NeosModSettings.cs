using FrooxEngine;
using FrooxEngine.UIX;
using BaseX;
using HarmonyLib;
using NeosModLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NeosModSettings
{
    public class NeosModSettings : NeosMod
    {
        public override string Name => "NeosModSettings";
        public override string Author => "badhaloninja";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/badhaloninja/NeosModSettings";


        private static Dictionary<string, NeosModBase> configuredModList = new Dictionary<string, NeosModBase>();

        private static Slot optionsRoot;

        public override void OnEngineInit()
        {
            ModConfiguration.OnAnyConfigurationChanged += OnConfigurationChanged;

            Harmony harmony = new Harmony("me.badhaloninja.NeosModSettings");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(UserspaceScreensManager), "OnLoading")]
        class ModSettingsScreen
        {
            public static void Postfix(UserspaceScreensManager __instance) //, DataTreeNode node, LoadControl control
            {
                RadiantDash componentInParents = __instance.Slot.GetComponentInParents<RadiantDash>();
                
                RadiantDashScreen radiantDashScreen = componentInParents.AttachScreen("NML", color.Orange, NeosAssets.Graphics.Icons.Dash.Tools);

                generateNMLScreen(radiantDashScreen);
            }

            private static void generateNMLScreen(RadiantDashScreen radiantDashScreen)
            {
                Slot screenSlot = radiantDashScreen.Slot;
                screenSlot.OrderOffset = 70;
                screenSlot.PersistentSelf = false;

                var ui = new UIBuilder(radiantDashScreen.ScreenCanvas);
                RadiantUI_Constants.SetupDefaultStyle(ui);
                ui.Image(UserspaceRadiantDash.DEFAULT_BACKGROUND);

                ui.NestInto(ui.Empty("Split"));
                ui.SplitHorizontally(0.25f, out RectTransform left, out RectTransform right);

                ui.NestInto(left);
                left.Slot.AttachComponent<Image>().Tint.Value = new color(0.05f, 0.75f);

                ui.HorizontalFooter(56f, out RectTransform modsFooter, out RectTransform modsContent);
                ui.NestInto(modsFooter);
                ui.Button("Save All").LocalPressed += (bt,be) => saveAllConfigs();

                ui.NestInto(modsContent);
                ui.ScrollArea();
                ui.VerticalLayout(2f, childAlignment: Alignment.TopLeft)
                    .ForceExpandHeight.Value = false;

                ui.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);

                RadiantUI_Constants.SetupDefaultStyle(ui);
                ui.Style.PreferredHeight = 90f;
                GenerateModButtons(ui);


                ui.NestInto(right);
                ui.HorizontalFooter(100f, out RectTransform footer, out RectTransform content);

                ui.NestInto(footer);
                var splits = ui.SplitHorizontally(0.75f, 0.25f);
                ui.NestInto(splits[1]);

                //ui.Image(color.Red);
                var versionText = ui.Text(null);
                versionText.Content.SyncWithVariable("Config/SelectedModVersion");




                ui.NestInto(content);

                ui.ScrollArea();
                ui.VerticalLayout(2f, childAlignment: Alignment.TopLeft)
                    .ForceExpandHeight.Value = false;

                ui.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);

                RadiantUI_Constants.SetupDefaultStyle(ui);
                ui.Style.PreferredHeight = 45f;

                optionsRoot = ui.Root;

                var space = screenSlot.AttachComponent<DynamicVariableSpace>();
                space.SpaceName.Value = "Config";
                var selectedModVar = screenSlot.AttachComponent<DynamicValueVariable<string>>();
                selectedModVar.VariableName.Value = "Config/SelectedMod";
                selectedModVar.Value.OnValueChange += generateConfigItems;

                selectedModVar.Value.Value = "badhaloninja.NeosModSettings";

            }


            private static void generateConfigItems(SyncField<string> syncField)
            {
                optionsRoot.DestroyChildren();
                var dvSpace = optionsRoot.FindSpace("Config");
                
                dvSpace?.TryWriteValue<string>("Config/SelectedModVersion", "");
                if (String.IsNullOrWhiteSpace(syncField.Value)) return;
                if (!configuredModList.TryGetValue(syncField.Value, out NeosModBase mod) || mod == null) return;


                dvSpace?.TryWriteValue("Config/SelectedModVersion", mod.Version);

                UIBuilder ui = new UIBuilder(optionsRoot);

                RadiantUI_Constants.SetupDefaultStyle(ui);
                ui.Style.PreferredHeight = 45f;

                ModConfiguration config = mod.GetConfiguration();
                Msg(config.ConfigurationItemDefinitions.Count);
                foreach (ModConfigurationKey key in config.ConfigurationItemDefinitions)
                {
                    if (key.InternalAccessOnly) continue; // As we are an external mod enumerating configs, we should ignore internal-only configuration items
                    
                    generateConfigFieldOfType(key.ValueType(), ui, syncField.Value, config, key);
                    continue;
                }
            }
            private static void generateConfigFieldOfType(Type type, UIBuilder ui, string ModName, ModConfiguration config, ModConfigurationKey key)
            {
                var method = typeof(ModSettingsScreen).GetMethod(nameof(generateConfigField));
                var genMethod = method.MakeGenericMethod(type);
                object[] args = new object[] { ui, ModName, config, key };
                genMethod.Invoke(null, args);
                return;
            }
            public static void generateConfigField<T>(UIBuilder ui, string ModName, ModConfiguration config, ModConfigurationKey key)
            {

                Msg("huh");
                //ui.HorizontalElementWithLabel("Disable Ik's without active user.", 0.7f, () => ui.IntegerField(0, 100));

                if (!DynamicValueVariable<T>.IsValidGenericType) return;
                Slot root = ui.Empty("ConfigElement");
                ui.NestInto(root);
                var dynvar = root.AttachComponent<DynamicValueVariable<T>>();
                dynvar.VariableName.Value = $"Config/{ModName}.{key.Name}";
                
                dynvar.Value.Value = config.TryGetValue(key, out object cv) ? (T)cv : Coder<T>.Default;
                dynvar.Value.OnValueChange += (syncF) =>
                {
                    object value = syncF.Value;
                    if (config.TryGetValue(key, out object configValue) && configValue == value) return;

                    if (!key.Validate(value))
                    {
                        syncF.Value = config.TryGetValue(key, out object cV) ? (T)cV : Coder<T>.Default;
                        return;
                    }

                    config.Set(key, syncF.Value, "NeosModSettings variable change");
                };

                string varName = (String.IsNullOrWhiteSpace(key.Description)) ? key.Name : key.Description;

                ui.Style.TextColor = RadiantUI_Constants.TEXT_COLOR;
                SyncMemberEditorBuilder.Build(dynvar.Value, varName, dynvar.GetSyncMemberFieldInfo(4), ui);

                RadiantUI_Constants.SetupDefaultStyle(ui);

                var memberText = ui.Root.GetComponentInChildren<Text>((Text) =>
                {
                    var bt = Text.Slot.GetComponent<Button>();
                    var rp = Text.Slot.GetComponent<ReferenceProxySource>();
                    if (rp == null || bt == null) return false;
                    rp.Destroy();
                    bt.Destroy();
                    return true;
                });
                memberText.Color.Value = ui.Style.TextColor;
/*
                ui.Root.ForeachComponentInChildren<Text>((text) =>
                {
                    if (text.Color.IsBlockedByDrive) return;
                    text.Color.Value = ui.Style.TextColor;
                });


                ui.Root.ForeachComponentInChildren<Button>((button) =>
                {
                    //button.Slot.ActiveSelf = false;
                    Msg(button);
                    var image = button.Slot.GetComponent<Image>();
                    if (image == null) return;
                    Msg("Image Exists");
                    var colorDrive = button.ColorDrivers.GetFirst();
                    if (colorDrive == null) return;
                    Msg("Exists");
                    if (colorDrive.ColorDrive.Target == image.Tint) return;
                    Msg("IsTint");
                    colorDrive.ColorDrive.Target = null;

                    image.Tint.Value = color.Green;
                    button.SetupBackgroundColor(image.Tint);

                    //button.ColorDrivers.Remove(colorDrive);

                    button.ColorDrivers.Add().SetColors(color.Red);
                    //button.SetupBackgroundColor(image.Tint);

                    //colorDrive.NormalColor.Value = color.Green;
                    //colorDrive.SetColors(color.Red);

                    //Msg(colorDrive.NormalColor);

                    //colorDrive.NormalColor.Value = color.Green;
                    //colorDrive.DisabledColor.Value = ui.Style.DisabledColor.GetValueOrDefault();
                    //button.Enabled = false;
                });
*/
                ui.NestOut();
            }
            private static void GenerateModButtons(UIBuilder ui)
            {
                List<NeosModBase> mods = new List<NeosModBase>(ModLoader.Mods());
                List<NeosModBase> configuredMods = mods
                    .Where(m => m.GetConfiguration() != null) // mods that do not define a configuration have a null GetConfiguration() result.
                    .ToList();

                foreach (NeosModBase mod in configuredMods)
                {
                    string modKey = String.Join(".", mod.Author, mod.Name);

                    configuredModList.Add(modKey, mod);
                    var button = ui.Button(mod.Name);
                    var dVar = button.Slot.AttachComponent<DynamicValueVariable<string>>();
                    dVar.VariableName.Value = "Config/SelectedMod";

                    var deselected = new OptionDescription<string>(null, label: mod.Name, buttonColor: color.Black);
                    var selected = new OptionDescription<string>(modKey, label: mod.Name, buttonColor: RadiantUI_Constants.HIGHLIGHT_COLOR);

                    button.ConvertTintToAdditive();
                    button.SetupValueCycle(dVar.Value, selected, deselected);
                }
            }



            private static void saveAllConfigs()
            {
                foreach (NeosModBase mod in configuredModList.Values) {
                    mod.GetConfiguration().Save();
                }
            }
            /*private static void saveCurrentConfig()
            {

                if (!configuredModList.TryGetValue(syncField.Value, out NeosModBase mod) || mod == null) return;

                UIBuilder ui = new UIBuilder(optionsRoot);

                RadiantUI_Constants.SetupDefaultStyle(ui);
                ui.Style.PreferredHeight = 45f;

                ModConfiguration config = mod.GetConfiguration();
            }*/
        }
        private void OnConfigurationChanged(ConfigurationChangedEvent @event)
        {
            Msg($"ConfigurationChangedEvent fired for mod \"{@event.Config.Owner.Name}\" Config \"{@event.Key.Name}\"");
            if (optionsRoot == null) return;

        }
    }
}