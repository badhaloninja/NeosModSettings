﻿using FrooxEngine;
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
    // Maybe consider to split into a partial class
    public class NeosModSettings : NeosMod
    {
        public override string Name => "NeosModSettings";
        public override string Author => "badhaloninja";
        public override string Version => "1.4.0";
        public override string Link => "https://github.com/badhaloninja/NeosModSettings";

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> ITEM_HEIGHT = new("itemHeight", "Determines height of config items like this one. You need to click on another page for it to apply.", () => 24);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> SHOW_INTERNAL = new("showInternal", "Whether to show internal use only config options, their text will be yellow.", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> SHOW_NAMES = new("showNames", "Whether to show the internal key names next to descriptions.", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> HIGHLIGHT_ITEMS = new("highlightAlternateItems", "Highlight alternate configuration items", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<color> HIGHLIGHT_TINT = new("highlightColor", "Highlight color", () => color.White.SetA(0.2f));
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> RESET_INTERNAL = new("resetInternal", "Also reset internal use only config options, <b>Can cause unintended behavior</b>", () => false);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> PER_KEY_RESET = new("showPerKeyResetButtons", "Show reset buttons for each config key", () => false);


        // Test Variables
            [AutoRegisterConfigKey] // Huh dummy can be used as a spacer, neat
            private static readonly ModConfigurationKey<dummy> TEST_DUMMY = new("dummy", "---------------------------------------------------------------------------------------------------------------------------------");
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<bool> TEST_BOOL = new("testBool", "Test Boolean", () => true);
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<string> TEST_STRING = new("testStr", "Test String", () => "Value");
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<Key> TEST_KEYENUM = new("testKeyEnum", "Test Key Enum", () => Key.None);
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<int4> TEST_INTVECTOR = new("testIntVector", "Test int4", () => new int4(12), valueValidator: (value) => value.x == 12);
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<float3x3> TEST_float3x3 = new("testFloat3x3", "Test float3x3", () => float3x3.Identity);
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<color> TEST_COLOR = new("testColor", "Test Color", () => color.Blue);
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<Type> TEST_TYPE = new("testType", "Test Type", () => typeof(Button));
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<Uri> TEST_URI = new("testUri", "Test Uri", () => null);
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<Uri> TEST_INTERNAL = new("testInternal", "Test internal access only key, must be http or https", () => new Uri("https://example.com"), true, (uri) => uri != null && (uri.Scheme == "https" || uri.Scheme == "http"));
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<Uri> TEST_INTERNAL_NO_NULL_CHECK = new("testInternalNoNull", "Test internal access only key, must be http or https, error thrown on null", () => new Uri("https://example.com"), true, (uri) => uri.Scheme == "https" || uri.Scheme == "http");
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<float2x2> TEST_NAN_VECTOR_INTERNAL = new("testNanVectorInternal", "Test internal access only NaN Vector for pr #11", () => new float2x2(float.NaN, float.NaN, float.NaN, float.NaN), true);
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<string> TEST_LOCAL_KEY = new("testLocaleKey", "Settings.Locale.ChangeLanguage", () => "Locale Test", true, (str) => str == "Locale Test");
            [AutoRegisterConfigKey]
            private static readonly ModConfigurationKey<string> TEST_ERROR_ON_STR_EMPTY = new("testErrOnStringEmpty", "Test error on string empty", () => "Value", valueValidator: (str) =>
            {
                if (string.IsNullOrWhiteSpace(str))
                    throw new ArgumentNullException(nameof(str));
                return true;
            });
        //

        private static NeosModSettings Current;
        private static ModConfiguration Config;
        private static RadiantDashScreen CurrentScreen;
        private static readonly Dictionary<string, NeosModBase> configuredModList = new();

        private static Slot configKeysRootSlot;
        private static Slot modButtonsRoot;

        private static readonly MethodInfo generateConfigFieldMethod = typeof(ModSettingsScreen).GetMethod(nameof(ModSettingsScreen.GenerateConfigField));
        private static readonly MethodInfo fireConfigurationChangedEventMethod = typeof(ModConfiguration).GetMethod("FireConfigurationChangedEvent", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static readonly string _internalConfigUpdateLabel = "NeosModSettings Edit Value";
        internal static readonly string _internalConfigResetLabel = "NeosModSettings Config Reset";

        private static readonly Dictionary<ModConfigurationKey, string> ConfigKeyVariableNames = new(){
            { SHOW_NAMES, "Config/_showFullName" },
            { SHOW_INTERNAL, "Config/_showInternal" },
            { RESET_INTERNAL, "Config/_resetInternal" },
            { PER_KEY_RESET, "Config/_showResetButtons" },
            { HIGHLIGHT_ITEMS, "Config/_highlightKeys" },
            { HIGHLIGHT_TINT, "Config/_highlightTint" }
        };

        public override void OnEngineInit()
        {
            Current = this;

            Config = GetConfiguration();
            ModConfiguration.OnAnyConfigurationChanged += OnConfigurationChanged;
            Config.OnThisConfigurationChanged += OnThisConfigurationChanged;

            Harmony harmony = new("me.badhaloninja.NeosModSettings");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(UserspaceScreensManager))]
        static class InjectScreenPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch("SetupDefaults")]
            // If you don't have an account or sign out this will be generated
            public static void SetupDefaults(UserspaceScreensManager __instance) => ModSettingsScreen.GenerateModSettingsScreen(__instance);
            [HarmonyPostfix]
            [HarmonyPatch("OnLoading")]
            // If you have an account/sign in OnLoading triggers and replaces the contents generated by SetupDefaults
            public static void OnLoading(UserspaceScreensManager __instance) => ModSettingsScreen.GenerateModSettingsScreen(__instance);
        }

        static class ModSettingsScreen
        {
            public static void GenerateModSettingsScreen(UserspaceScreensManager __instance)
            {
                bool screenExists = CurrentScreen != null && !CurrentScreen.IsRemoved;
                if (__instance.World != Userspace.UserspaceWorld || screenExists) return;

                RadiantDash dash = __instance.Slot.GetComponentInParents<RadiantDash>();

                //if (dash.GetScreen<RadiantDashScreen>(screen => screen.Name == "NML") != null) return;

                CurrentScreen = dash.AttachScreen("NML", color.Orange, NeosAssets.Graphics.Icons.Dash.Tools);
                
                Slot screenSlot = CurrentScreen.Slot;
                screenSlot.OrderOffset = 256; // Settings Screen is 60, Exit screen is set to int.MaxValue 
                screenSlot.PersistentSelf = false; // So it doesn't save

                var ui = new UIBuilder(CurrentScreen.ScreenCanvas);
                RadiantUI_Constants.SetupDefaultStyle(ui);
                ui.Image(UserspaceRadiantDash.DEFAULT_BACKGROUND);

                ui.NestInto(ui.Empty("Split"));
                ui.SplitHorizontally(0.25f, out RectTransform left, out RectTransform right);


                ui.NestInto(left); // Mod List
                left.Slot.AttachComponent<Image>()
                    .Tint.Value = new color(0.05f, 0.75f);

                ui.HorizontalFooter(56f, out RectTransform modsFooter, out RectTransform modsContent);
                ui.NestInto(modsFooter);
                var saveAllBtn = ui.Button("Save All");
                saveAllBtn.LocalPressed += SaveAllConfigs;

                // List Mods
                ui.NestInto(modsContent);
                ui.ScrollArea(Alignment.TopCenter);
                ui.VerticalLayout(4f);

                ui.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);

                modButtonsRoot = ui.Root;
                GenerateModButtons(ui);


                // Config Panel
                ui.NestInto(right);

                BuildInfoPage(ui, out RectTransform configUiRoot); // Shows when no mod is selected

                ui.NestInto(configUiRoot);
                var splitList = ui.SplitVertically(96f, 884f, 100f);
                var header = splitList[0];
                var content = splitList[1];
                var footer = splitList[2];


                ui.NestInto(header);
                ui.Style.PreferredHeight = 64f;
                ui.Text("")
                    .Content.SyncWithVariable("Config/SelectedMod.Name");



                ui.NestInto(footer);
                var splits = ui.SplitHorizontally(0.25f, 0.55f, 0.25f);

                ui.NestInto(splits[0]); //Author (Left)
                ui.Text(Current.Author)
                    .Content.SyncWithVariable("Config/SelectedMod.Author");

                ui.NestInto(splits[1]); //Link (Center)

                var linkText = ui.Text("");
                var hyperlink = linkText.Slot.AttachComponent<Hyperlink>();
                hyperlink.URL.SyncWithVariable("Config/SelectedMod.Uri");
                linkText.Content.DriveFrom(hyperlink.URL, "{0}"); // Drive the text 
                var hyperlinkButton = linkText.Slot.AttachComponent<Button>();
                hyperlinkButton.SetupBackgroundColor(linkText.Color); // Drive the text color

                ui.NestInto(splits[2]); // Version (Right)

                var versionText = ui.Text(Current.Version);
                versionText.Content.SyncWithVariable("Config/SelectedMod.Version");



                // Setup config root
                RadiantUI_Constants.SetupDefaultStyle(ui);
                ui.NestInto(content);



                ui.Style.PreferredHeight = 45f;
                ui.ScrollArea(Alignment.TopCenter);
                ui.VerticalLayout(4f, 24f);
                ui.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);




                // Config Items Vertical layout
                ui.Style.PreferredHeight = -1f;
                ui.VerticalLayout(4f); // New layout to easily clear config items and not delete buttons
                ui.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);
                configKeysRootSlot = ui.Root;
                //

                configKeysRootSlot.CreateVariable(ConfigKeyVariableNames[SHOW_NAMES], Config.GetValue(SHOW_NAMES));
                configKeysRootSlot.CreateVariable(ConfigKeyVariableNames[SHOW_INTERNAL], Config.GetValue(SHOW_INTERNAL));
                configKeysRootSlot.CreateVariable(ConfigKeyVariableNames[PER_KEY_RESET], Config.GetValue(PER_KEY_RESET));
                configKeysRootSlot.CreateVariable(ConfigKeyVariableNames[HIGHLIGHT_TINT], Config.GetValue(HIGHLIGHT_TINT));
                configKeysRootSlot.CreateVariable(ConfigKeyVariableNames[HIGHLIGHT_ITEMS], Config.GetValue(HIGHLIGHT_ITEMS));

                // Controls
                ui.NestOut();
                RadiantUI_Constants.SetupDefaultStyle(ui);
                ui.HorizontalLayout(4f).PaddingTop.Value = 8f;

                ui.Style.PreferredHeight = 24f;
                var saveCurrentBtn = ui.Button("Save Settings");
                saveCurrentBtn.RequireLockInToPress.Value = true; // So you can scroll with laser without worrying about pressing it
                saveCurrentBtn.LocalPressed += SaveCurrentConfig;

                var defaultsBtn = ui.Button("Reset Default Settings");
                var defaultsBtnLabelDrive = defaultsBtn.Label.Slot.AttachComponent<BooleanValueDriver<string>>();
                defaultsBtnLabelDrive.FalseValue.Value = "Reset Default Settings";
                defaultsBtnLabelDrive.TrueValue.Value = "Reset Default Settings  <size=90%><color=#c44>Including Internal</color></size>";

                defaultsBtnLabelDrive.State.SyncWithVariable(ConfigKeyVariableNames[RESET_INTERNAL]);
                defaultsBtnLabelDrive.TargetField.TryLink(defaultsBtn.LabelTextField);

                defaultsBtn.RequireLockInToPress.Value = true; // So you can scroll with laser without worrying about pressing it
                defaultsBtn.LocalReleased += ResetCurrentConfig;


                var space = screenSlot.AttachComponent<DynamicVariableSpace>();
                space.SpaceName.Value = "Config";

                var selectedModVar = screenSlot.AttachComponent<DynamicValueVariable<string>>();
                selectedModVar.VariableName.Value = "Config/SelectedMod";
                selectedModVar.Value.OnValueChange += (field) => {
                    try
                    {
                        GenerateConfigItems(field.Value); // Regen Config items on change
                    }
                    catch (Exception e) { Error(e); }
                };
            }
            private static void BuildInfoPage(UIBuilder ui, out RectTransform content)
            {
                Slot descRoot = ui.Next("Info"); // New Slot for the NeosModSettings info
                ui.Nest();

                ui.HorizontalFooter(100f, out RectTransform footer, out RectTransform body);
                ui.NestInto(body);

                ui.VerticalLayout(4f, 24f, childAlignment: Alignment.MiddleCenter);
                ui.FitContent(SizeFit.Disabled, SizeFit.PreferredSize);

                ui.Text($"<size=150%>{Current.Name}</size>"); //Title

                ui.Spacer(45f);
                ui.Style.PreferredHeight = 250f;
                string Desc = "NeosModSettings is a modification to the base game that allows the users to directly interact with the mods that they have installed onto their game from inside the application.\n\nCurrently only supports configs that are valid DynamicValueVariable types and those of type Type meaning <b>Arrays are not supported</b> <size=30%>including any other collections</size>";
                ui.Text(Desc, alignment: Alignment.MiddleCenter);



                // Media links
                ui.Spacer(16f);
                ui.Style.PreferredHeight = 64f;
                ui.HorizontalLayout(4f) // Links
                    .ForceExpandWidth.Value = false; // So buttons aren't entire width of the screen
                ui.Style.PreferredWidth = 64f;


                Uri githubMark = new("neosdb:///0c2ea8c328f68cc70eaa017a17cda0533895f1bbaa8764db9646770cd1b1a0b4.png");
                Slot ghBtn = ui.Image(githubMark).Slot;
                ghBtn.AttachComponent<Hyperlink>().URL.Value = new Uri(Current.Link);
                ghBtn.AttachComponent<Button>(); // There does not appear to be a UiBuilder func to make a button out of a sprite, only ones to put a sprite on a button

                ui.NestInto(footer);

                ui.Text(Current.Version);


                ui.NestInto(descRoot.Parent); // Go up one from Info

                var contentRoot = ui.Empty("Content");
                content = contentRoot.GetComponent<RectTransform>();
                // Drive the state of info based on if a mod is selected
                var dynVar = ui.Root.AttachComponent<DynamicValueVariable<string>>();
                dynVar.VariableName.Value = "Config/SelectedMod";

                var equalityDriver = ui.Root.AttachComponent<ValueEqualityDriver<string>>(); // Put value equality driver on the parent of Info
                equalityDriver.TargetValue.TrySet(dynVar.Value);
                equalityDriver.Target.TrySet(descRoot.ActiveSelf_Field); // Drive boolean value driver

                var invertEqualityDriver = ui.Root.AttachComponent<ValueEqualityDriver<bool>>(); // Drive contentRoot.Active to !descRoot.Active
                invertEqualityDriver.TargetValue.TrySet(descRoot.ActiveSelf_Field);
                invertEqualityDriver.Target.TrySet(contentRoot.ActiveSelf_Field);

                ui.Style.PreferredWidth = -1f;
                RadiantUI_Constants.SetupDefaultStyle(ui); // Reset style
            }

            internal static void GenerateModButtons(UIBuilder ui)
            {
                RadiantUI_Constants.SetupDefaultStyle(ui);
                ui.Style.PreferredHeight = 90f;
                List<NeosModBase> mods = ModLoader.Mods().ToList();
                List<NeosModBase> configuredMods = mods.Where(m => m.GetConfiguration() != null).ToList(); // Get all mods with configs
                    


                var dVar = ui.Root.GetComponentOrAttach<DynamicValueVariable<string>>(out bool varAttached);
                if (varAttached)
                {
                    dVar.VariableName.Value = "Config/SelectedMod";
                }
                bool flag = configuredModList.Count == 0;
                foreach (NeosModBase mod in configuredMods)
                {
                    int configCount = mod.GetConfiguration().ConfigurationItemDefinitions
                        .Where(c => (Config.GetValue(SHOW_INTERNAL) || !c.InternalAccessOnly) &&
                                    // check whether config item can be displayed
                                    (
                                        c.ValueType() == typeof(Type) ||
                                        (bool)typeof(DynamicValueVariable<>).MakeGenericType(c.ValueType()).GetProperty("IsValidGenericType").GetValue(null)
                                    ))
                        .Count();
                    Debug($"{mod.Name} has {configCount} available config items");
                    // Skip if it only has InternalAccessOnly definitions, or only ones that we can't render
                    if (configCount == 0) continue;

                    string modKey = $"{mod.Author}.{mod.Name}";

                    if (flag) // Incase you log out and log in
                    {
                        configuredModList.Add(modKey, mod);
                    }

                    var button = ui.Button(mod.Name);

                    // Adds a little bit of padding to the text, to prevent long mod names from touching the edges
                    var textRect = button.Slot[0].GetComponent<RectTransform>();
                    textRect.OffsetMin.Value = new float2(24, 0);
                    textRect.OffsetMax.Value = new float2(-24, 0);

                    var deselected = new OptionDescription<string>(null, label: mod.Name, buttonColor: RadiantUI_Constants.BUTTON_COLOR);
                    var selected = new OptionDescription<string>(modKey, label: mod.Name, buttonColor: RadiantUI_Constants.HIGHLIGHT_COLOR);

                    button.ConvertTintToAdditive();
                    button.SetupValueToggle(dVar.Value, modKey, selected, deselected);
                }

                Debug($"{configuredModList.Count} found mods with configs");
            }

            public static void GenerateConfigItems(string SelectedMod = null)
            {
                configKeysRootSlot.DestroyChildren(); // Clear configs

                if(SelectedMod == null)
                    configKeysRootSlot.TryReadDynamicValue("Config/SelectedMod", out SelectedMod);
                

                // Reset footer
                configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.Name", "");
                configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.Author", "");
                configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.Version", "");
                configKeysRootSlot.TryWriteDynamicValue<Uri>("Config/SelectedMod.Uri", null);

                if (string.IsNullOrWhiteSpace(SelectedMod) || !configuredModList.TryGetValue(SelectedMod, out NeosModBase mod) || mod == null)
                    return; // Skip if no mod is selected

                // Set footer values
                configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.Name", mod.Name);
                configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.Author", mod.Author);
                configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.Version", mod.Version);

                Uri.TryCreate(mod.Link, UriKind.RelativeOrAbsolute, out Uri modUri); // Catch invalid uris just incase
                configKeysRootSlot.TryWriteDynamicValue("Config/SelectedMod.Uri", modUri);


                UIBuilder ui = new(configKeysRootSlot);
                RadiantUI_Constants.SetupDefaultStyle(ui);

                // Mod name at top of config
                /*ui.Text(mod.Name);
                ui.Spacer(32f);*/
                ui.Style.PreferredHeight = Config.GetValue(ITEM_HEIGHT);//24f;

                ModConfiguration config = mod.GetConfiguration();

                var i = 0;
                foreach (ModConfigurationKey key in config.ConfigurationItemDefinitions)
                { // Generate field for every supported config
                    if (!Config.GetValue(SHOW_INTERNAL) && key.InternalAccessOnly && config != Config) continue; // Skip internal keys sometimes
                    var item = GenerateConfigFieldOfType(key.ValueType(), ui, SelectedMod, config, key);
                    if(item == null) continue;


                    item.ForeachComponentInChildren<Button>(button => button.RequireLockInToPress.Value = true);



                    if (!Config.GetValue(HIGHLIGHT_ITEMS) && config != Config) continue;
                    if (i % 2 == 1)
                    {
                        var bg = item.AddSlot("Background");
                        bg.ActiveSelf_Field.DriveFromVariable(ConfigKeyVariableNames[HIGHLIGHT_ITEMS]);

                        bg.OrderOffset = -1;
                        var rect = bg.AttachComponent<RectTransform>();
                        bg.AttachComponent<IgnoreLayout>();
                        rect.AnchorMin.Value = new float2(-0.005f, 0f);
                        rect.AnchorMax.Value = new float2(1.005f, 1f);
                        bg.AttachComponent<Image>().Tint.DriveFromVariable(ConfigKeyVariableNames[HIGHLIGHT_TINT]);
                    }
                    i++;
                }
            }

            public static Slot GenerateConfigFieldOfType(Type type, UIBuilder ui, string ModName, ModConfiguration config, ModConfigurationKey key)
            { // Generics go brr
                var genMethod = generateConfigFieldMethod.MakeGenericMethod(type); // Convert to whatever type requested
                object[] args = new object[] { ui, ModName, config, key }; // Pass the arguments
                
                return (Slot)genMethod.Invoke(null, args); // Run the method
            }
            public static Slot GenerateConfigField<T>(UIBuilder ui, string ModName, ModConfiguration config, ModConfigurationKey key)
            {
                bool isType = typeof(T) == typeof(Type);
                if (!(isType || DynamicValueVariable<T>.IsValidGenericType)) return null; // Check if supported type

                if (isType) Debug($"GenerateConfigField for type Type");

                string configName = $"{ModName}.{key.Name}";

                ui.Style.MinHeight = Config.GetValue(ITEM_HEIGHT);
                if (typeof(T).IsMatrixType())
                { // If it is a matrix adjust the height of the field 
                    int2 matrixDimensions = typeof(T).GetMatrixDimensions();
                    ui.Style.MinHeight = Math.Max(ui.Style.MinHeight, matrixDimensions.y * Config.GetValue(ITEM_HEIGHT) + (matrixDimensions.y - 1) * 4);
                }
                Slot root = ui.Empty("ConfigElement");
                if (key.InternalAccessOnly) root.ActiveSelf_Field.DriveFromVariable(ConfigKeyVariableNames[SHOW_INTERNAL]);

                ui.NestInto(root);

                SyncField<T> syncField;
                FieldInfo fieldInfo;

                if (!isType)
                {
                    var dynvar = root.AttachComponent<DynamicValueVariable<T>>();
                    dynvar.VariableName.Value = $"Config/{configName}";

                    syncField = dynvar.Value;
                    fieldInfo = dynvar.GetSyncMemberFieldInfo(4);
                } else
                {
                    var dynvar = root.AttachComponent<DynamicReferenceVariable<SyncType>>();
                    dynvar.VariableName.Value = $"Config/{configName}";

                    var typeField = root.AttachComponent<TypeField>();
                    dynvar.Reference.TrySet(typeField.Type);

                    syncField = typeField.Type as SyncField<T>;
                    fieldInfo = typeField.GetSyncMemberFieldInfo(3);
                }


                var typedKey = key as ModConfigurationKey<T>;

                
                T defaultValue = default;
                if (Coder<T>.IsSupported) defaultValue = Coder<T>.Default;

                var initialValue = config.TryGetValue(key, out object currentValue) ? (T)currentValue : defaultValue; // Set initial value

                syncField.Value = initialValue;
                syncField.OnValueChange += (syncF) => HandleConfigFieldChange(syncF, config, typedKey);

                // Validate the value changes
                // LocalFilter changes the value passed to InternalSetValue
                syncField.LocalFilter = (value, field) => ValidateConfigField(value, config, typedKey, defaultValue);


                RadiantUI_Constants.SetupDefaultStyle(ui);
                ui.Style.TextAutoSizeMax = Config.GetValue(ITEM_HEIGHT);

                bool nameAsKey = string.IsNullOrWhiteSpace(key.Description);
                string localeText = nameAsKey ? key.Name : key.Description;
                string format = "{0}";
                if (Config.GetValue(SHOW_NAMES) && !nameAsKey)
                {
                    format = $"<i><b>{key.Name}</i></b> - " + "{0}";
                }

                if (key.InternalAccessOnly) format = $"<color=#dec15b>{format}</color>";

                // Build ui

                var localized = new LocaleString(localeText, format, true, true, null);
                ui.HorizontalElementWithLabel<Component>(localized, 0.7f, () =>
                {// Using HorizontalElementWithLabel because it formats nicer than SyncMemberEditorBuilder with text
                    if(config == Config && !nameAsKey)
                    {
                        var localeDriver = root.GetComponentInChildren<LocaleStringDriver>();
                        if(localeDriver != null)
                        {
                            var nameDrive = localeDriver.Slot.AttachComponent<BooleanValueDriver<string>>();

                            nameDrive.State.DriveFromVariable(ConfigKeyVariableNames[SHOW_NAMES]);

                            var fullName = $"<i><b>{key.Name}</i></b> - " + "{0}";
                            if (key.InternalAccessOnly) fullName = $"<color=#dec15b>{fullName}</color>";

                            nameDrive.FalseValue.Value = key.InternalAccessOnly ? "<color=#dec15b>{0}</color>" : "{0}";
                            nameDrive.TrueValue.Value = fullName;

                            nameDrive.TargetField.TrySet(localeDriver.Format);
                        }
                    }

                    ui.HorizontalLayout(4f, childAlignment: Alignment.MiddleLeft).ForceExpandHeight.Value = false;

                    ui.Style.FlexibleWidth = 10f;
                    SyncMemberEditorBuilder.Build(syncField, null, fieldInfo, ui); // Using null for name makes it skip generating text
                    ui.Style.FlexibleWidth = -1f;

                    ui.Root.ForeachComponentInChildren<Text>((text) =>
                    { // Make value path text readable
                        // XYZW, RGBA, etc.
                        if (text.Slot.Parent.GetComponent<Button>() != null) return; // Ignore text under buttons
                        text.Color.Value = RadiantUI_Constants.TEXT_COLOR;
                    });

                    AddResetKeyButton(ui, config, typedKey);
                    ui.NestOut();

                    return null; // HorizontalElementWithLabel requires a return type that implements a component
                });

                ui.Style.MinHeight = -1f;
                ui.NestOut();

                return root;
            }


            private static void AddResetKeyButton<T>(UIBuilder ui, ModConfiguration modConfiguration, ModConfigurationKey<T> configKey)
            {
                if (modConfiguration != Config && !Config.GetValue(PER_KEY_RESET)) return;

                ui.PushStyle();
                ui.Style.MinHeight = Config.GetValue(ITEM_HEIGHT);
                ui.Style.MinWidth = Config.GetValue(ITEM_HEIGHT);
                ui.Panel();
                ui.PopStyle();

                ui.Root.ActiveSelf_Field.DriveFromVariable(ConfigKeyVariableNames[PER_KEY_RESET]);
                

                ui.Image(RadiantUI_Constants.BUTTON_COLOR).RectTransform.Pivot.Value = new float2(0f, 0.5f);
                ui.Current.AttachComponent<AspectRatioFitter>();
                ui.Current.AttachComponent<Button>().LocalPressed += (btn, evt) => ResetConfigKey(modConfiguration, configKey);

                ui.Nest();
                var text = ui.Text("🗘");

                if (configKey.InternalAccessOnly) text.Color.Value = color.FromHexCode("#c44");

                ui.NestOut();
                ui.NestOut();
            }


            private static T ValidateConfigField<T>(T value, ModConfiguration modConfiguration, ModConfigurationKey<T> configKey, T defaultValue)
            {
                bool isValid = false;

                try {
                    isValid = configKey.Validate(value);
                } catch (Exception e) {
                    //optionsRoot.LocalUser.IsDirectlyInteracting()
                    
                    string valueString = $"the value \"{value}\"";

                    if (value == null)
                        valueString = "a null value";
                    else if (string.IsNullOrWhiteSpace(value.ToString())) 
                        valueString += " (This value is not null)";

                    if (configKeysRootSlot.LocalUser.IsDirectlyInteracting())
                    {
                        Debug($"Validation method for configuration item {configKey.Name} from {modConfiguration.Owner.Name} has thrown an error for {valueString}\n\tThis was hidden as the user is currently editing a field", e);
                    } else
                    {
                        Error($"Validation method for configuration item {configKey.Name} from {modConfiguration.Owner.Name} has thrown an error for {valueString}", e);
                    }
                    
                }

                if (!isValid)
                { // Fallback if validation fails
                    Debug($"Failed Validation for {modConfiguration.Owner.Name}.{configKey.Name}");

                    bool isSet = modConfiguration.TryGetValue(configKey, out T configValue);
                    return isSet ? configValue : defaultValue; // Set to old value if is set Else set to default for that value
                }
                return value;
            }
            private static void HandleConfigFieldChange<T>(SyncField<T> syncField, ModConfiguration modConfiguration, ModConfigurationKey<T> configKey)
            {
                bool isSet = modConfiguration.TryGetValue(configKey, out T configValue);
                if (isSet && (Equals(configValue, syncField.Value) || !Equals(syncField.Value, syncField.Value))) return; // Skip if new value is unmodified or is logically inconsistent (self != self)

                try {
                    if (!configKey.Validate(syncField.Value)) return;
                } catch { return; }

                modConfiguration.Set(configKey, syncField.Value, _internalConfigUpdateLabel);
            }


            private static int SaveAllConfigs()
            {
                Debug("Save All Configs");
                int errCount = 0;
                foreach (NeosModBase mod in configuredModList.Values)
                { // Iterate over every mod with configs
                    Debug($"Saving Config for {mod.Name}");
                    try
                    {
                        mod.GetConfiguration().Save(); // Save config
                    }
                    catch (Exception e)
                    {
                        errCount++;
                        Error($"Failed to save Config for {mod.Name}");
                        Error(e);
                    }
                }
                return errCount;
            }
            private static void SaveAllConfigs(IButton button, ButtonEventData data)
            {
                button.LabelText = "Saving"; // Saves so fast this might be unnecessary 

                var errCount = SaveAllConfigs();

                if (errCount == 0)
                { // Show Saved! for 1 second
                    button.LabelText = "Saved!";
                    button.RunInSeconds(1f, () => button.LabelText = "Save All");
                    return;
                };
                // Errors

                button.Enabled = false;
                button.LabelText = $"<color=red>Failed to save {errCount} configs\n(Check Logs)</color>";
                button.RunInSeconds(5f, () => // Show error count for 5 seconds
                {
                    button.Enabled = true;
                    button.LabelText = "Save All";
                });
            }
            private static void SaveCurrentConfig(IButton button, ButtonEventData data)
            {
                button.Slot.TryReadDynamicValue("Config/SelectedMod", out string selectedMod);
                if (string.IsNullOrWhiteSpace(selectedMod) || !configuredModList.TryGetValue(selectedMod, out NeosModBase mod) || mod == null)
                    return;

                button.LabelText = "Saving"; // Saves so fast this might be unnecessary 

                Debug($"Saving Config for {mod.Name}");
                try
                {
                    mod.GetConfiguration().Save(); // Save config
                    button.LabelText = "Saved!"; // Show Saved! for 1 second

                    button.RunInSeconds(1f, () => button.LabelText = "Save Settings");
                }
                catch (Exception e)
                {
                    button.Enabled = false;
                    button.LabelText = $"<color=red>An Error Occurred\n(Check Logs)</color>";
                    button.RunInSeconds(5f, () => // Show error for 5 seconds
                    {
                        button.Enabled = true;
                        button.LabelText = "Save Settings";
                    });
                    Error($"Failed to save Config for {mod.Name}");
                    Error(e);
                }
            }


            private static void ResetCurrentConfig(IButton button, ButtonEventData data)
            {
                button.Slot.TryReadDynamicValue("Config/SelectedMod", out string selectedMod);
                if (string.IsNullOrWhiteSpace(selectedMod) || !configuredModList.TryGetValue(selectedMod, out NeosModBase mod) || mod == null)
                    return;
                var config = mod.GetConfiguration();

                bool resetInternal = Config.GetValue(RESET_INTERNAL);
                foreach (ModConfigurationKey key in config.ConfigurationItemDefinitions)
                { // Generate field for every supported config
                    if (!resetInternal && key.InternalAccessOnly) continue;

                    ResetConfigKey(config, key);
                }
            }
            private static void ResetConfigKey(ModConfiguration config, ModConfigurationKey key)
            {
                config.Unset(key);

                // Unset does not trigger the config changed event
                fireConfigurationChangedEventMethod.Invoke(config, new object[] { key, _internalConfigResetLabel });

                // Get default type
                object value = key.TryComputeDefault(out object defaultValue) ? defaultValue : key.ValueType().GetDefaultValue(); // How did I miss this extension??

                configKeysRootSlot.TryWriteDynamicValueOfType(key.ValueType(), $"Config/{config.Owner.Name}.{key.Name}", value);
            }
        }

        private static void OnConfigurationChanged(ConfigurationChangedEvent @event)
        {
            if (@event.Label == _internalConfigUpdateLabel) return;
            Debug($"ConfigurationChangedEvent fired for mod \"{@event.Config.Owner.Name}\" Config \"{@event.Key.Name}\"");
            if (configKeysRootSlot == null) return; // Skip if options root hasn't been generated yet


            if (!@event.Config.TryGetValue(@event.Key, out object value)) return; // Skip if failed to get the value
            string modName = $"{@event.Config.Owner.Author}.{@event.Config.Owner.Name}";
            configKeysRootSlot.SyncWriteDynamicValueType(@event.Key.ValueType(), $"Config/{modName}.{@event.Key.Name}", value);
        }
        private static void OnThisConfigurationChanged(ConfigurationChangedEvent @event)
        {
            if (configKeysRootSlot == null) return; // Skip if options root hasn't been generated yet
            if (@event.Key == SHOW_INTERNAL && modButtonsRoot != null)
            {
                // we need to regenerate mod buttons in case there is a mod with ONLY internal keys
                var ui = new UIBuilder(modButtonsRoot);
                modButtonsRoot.DestroyChildren();
                ModSettingsScreen.GenerateModButtons(ui);
            }
            if (ConfigKeyVariableNames.ContainsKey(@event.Key))
            {
                configKeysRootSlot.SyncWriteDynamicValueType(@event.Key.ValueType(), ConfigKeyVariableNames[@event.Key], Config.GetValue(@event.Key));
            }
        }
    }
}