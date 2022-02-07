using FrooxEngine;
using FrooxEngine.UIX;
using BaseX;
using HarmonyLib;
using NeosModLoader;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeosModSettings
{
    public class NeosModSettings : NeosMod
    {
        public override string Name => "NeosModSettings";
        public override string Author => "badhaloninja";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/badhaloninja/NeosModSettings";


        [AutoRegisterConfigKey]
        private readonly ModConfigurationKey<bool> KEY_ENABLE = new ModConfigurationKey<bool>("enabled", "Enables the NeosModConfigurationExample mod", () => true);

        [AutoRegisterConfigKey]
        private readonly ModConfigurationKey<int> KEY_COUNT = new ModConfigurationKey<int>("count", "Example counter", internalAccessOnly: true);




        private static DictionaryList<string, NeosModBase> configuredModList;
        private static NeosModBase selectedNeosMod = null;

        public override void OnEngineInit()
        {

            // disable the mod if the enabled config has been set to false
            ModConfiguration config = GetConfiguration();
            if (!config.GetValue(KEY_ENABLE)) // this is safe as the config has a default value
            {
                Debug("Mod disabled, returning early.");
                return;
            }

            ModConfiguration.OnAnyConfigurationChanged += OnConfigurationChanged;



            Harmony harmony = new Harmony("me.badhaloninja.NeosModSettings");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(UserspaceScreensManager), "OnLoading")]
        class ModSettingsScreen
        {
            public static void Postfix(UserspaceScreensManager __instance) //, DataTreeNode node, LoadControl control
            {
                /*int typeVersion = control.GetTypeVersion<UserspaceScreensManager>();
                Msg("UserspaceScreensManagerVersion" + typeVersion);*/
                RadiantDash componentInParents = __instance.Slot.GetComponentInParents<RadiantDash>();
                
                RadiantDashScreen radiantDashScreen = componentInParents.AttachScreen("NML", color.Orange, NeosAssets.Graphics.Icons.Dash.Tools);

                Slot screenSlot = radiantDashScreen.Slot;
                screenSlot.OrderOffset = 70;
                screenSlot.PersistentSelf = false;

                var ui = new UIBuilder(radiantDashScreen.ScreenCanvas);
                ui.Image(UserspaceRadiantDash.DEFAULT_BACKGROUND);
                ui.SplitHorizontally(0.25f, out RectTransform left, out RectTransform right);

                ui.NestInto(left);
                //ui.Image(new color(0f,1f,0f,0.5f));
                ui.ScrollArea();
                ui.VerticalLayout(2f, childAlignment: Alignment.TopLeft)
                    .ForceExpandHeight.Value = false;
                
                ui.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);

                RadiantUI_Constants.SetupDefaultStyle(ui);
                ui.Style.PreferredHeight = 90f;
                GenerateModButtons(ui);

                var space = screenSlot.AttachComponent<DynamicVariableSpace>();
                space.SpaceName.Value = "Config";

                EnumerateConfigs();
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
                    //mod.Name;
                    var button = ui.Button(mod.Name);
                    button.LocalPressed += (bt, d) =>
                    { // Very wip
                        if (selectedNeosMod == mod)
                        {
                            selectedNeosMod = null;
                            return;
                        }
                        selectedNeosMod = mod;
                    };
                }
            }
        }
        private void OnConfigurationChanged(ConfigurationChangedEvent @event)
        {
            Debug($"ConfigurationChangedEvent fired for mod \"{@event.Config.Owner.Name}\" Config \"{@event.Key.Name}\"");
        }

        private static void EnumerateConfigs()
        {
            List<NeosModBase> mods = new List<NeosModBase>(ModLoader.Mods());
            List<NeosModBase> configuredMods = mods
                .Where(m => m.GetConfiguration() != null) // mods that do not define a configuration have a null GetConfiguration() result.
                .ToList();
            Msg($"{mods.Count} mods are loaded, {configuredMods.Count} of them have configurations");

            foreach (NeosModBase mod in configuredMods)
            {
                ModConfiguration config = mod.GetConfiguration();
                foreach (ModConfigurationKey key in config.ConfigurationItemDefinitions)
                {
                    if (!key.InternalAccessOnly) // As we are an external mod enumerating configs, we should ignore internal-only configuration items
                    {
                        if (config.TryGetValue(key, out object value))
                        {
                            Msg($"{mod.Name} has configuration \"{key.Name}\" with type \"{key.ValueType()}\" and value \"{value}\"");
                        }
                        else
                        {
                            Msg($"{mod.Name} has configuration \"{key.Name}\" with type \"{key.ValueType()}\" and no value");
                        }
                    }
                }
            }
        }
    }
}