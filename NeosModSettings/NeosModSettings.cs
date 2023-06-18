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
    // Maybe consider to split into a partial class
    public class NeosModSettings : NeosMod
    {
        public override string Name => "NeosModSettings";
        public override string Author => "badhaloninja";
        public override string Version => "1.3.0";
        public override string Link => "https://github.com/badhaloninja/NeosModSettings";

        [AutoRegisterConfigKey]
        private readonly ModConfigurationKey<float> ITEM_HEIGHT = new ModConfigurationKey<float>("itemHeight", "Determines height of config items like this one. You need to click on another page for it to apply.", () => 24);
        [AutoRegisterConfigKey]
        private readonly ModConfigurationKey<bool> SHOW_INTERNAL = new ModConfigurationKey<bool>("showInternal", "Whether to show internal use only config options, their text will be yellow.", () => false);
        [AutoRegisterConfigKey]
        private readonly ModConfigurationKey<bool> SHOW_NAMES = new ModConfigurationKey<bool>("showNames", "Whether to show the internal key names next to descriptions.", () => false);
        [AutoRegisterConfigKey]
        private readonly ModConfigurationKey<bool> HIGHLIGHT_ITEMS = new ModConfigurationKey<bool>("highlightAlternateItems", "Highlight alternate configuration items", () => false);
        [AutoRegisterConfigKey]
        private readonly ModConfigurationKey<color> HIGHLIGHT_TINT = new ModConfigurationKey<color>("highlightColor", "Highlight color", () => color.White.SetA(0.2f));
        [AutoRegisterConfigKey]
        private readonly ModConfigurationKey<bool> RESET_INTERNAL = new ModConfigurationKey<bool>("resetInternal", "Also reset internal use only config options, <b>Can cause unintended behavior</b>", () => false);


        // Test Variables
            [AutoRegisterConfigKey] // Huh dummy can be used as a spacer, neat
            private readonly ModConfigurationKey<dummy> TEST_DUMMY = new ModConfigurationKey<dummy>("dummy", "---------------------------------------------------------------------------------------------------------------------------------");
            [AutoRegisterConfigKey]
            private readonly ModConfigurationKey<bool> TEST_BOOL = new ModConfigurationKey<bool>("testBool", "Test Boolean", () => true);
            [AutoRegisterConfigKey]
            private readonly ModConfigurationKey<string> TEST_STRING = new ModConfigurationKey<string>("testStr", "Test String", () => "Value");
            [AutoRegisterConfigKey]
            private readonly ModConfigurationKey<Key> TEST_KEYENUM = new ModConfigurationKey<Key>("testKeyEnum", "Test Key Enum", () => Key.None);
            [AutoRegisterConfigKey]
            private readonly ModConfigurationKey<int4> TEST_INTVECTOR = new ModConfigurationKey<int4>("testIntVector", "Test int4", () => new int4(12), valueValidator: (value) => value.x == 12);
            [AutoRegisterConfigKey]
            private readonly ModConfigurationKey<float3x3> TEST_float3x3 = new ModConfigurationKey<float3x3>("testFloat3x3", "Test float3x3", () => float3x3.Identity);
            [AutoRegisterConfigKey]
            private readonly ModConfigurationKey<color> TEST_COLOR = new ModConfigurationKey<color>("testColor", "Test Color", () => color.Blue);
            [AutoRegisterConfigKey]
            private readonly ModConfigurationKey<Type> TEST_TYPE = new ModConfigurationKey<Type>("testType", "Test Type", () => typeof(Button));
            [AutoRegisterConfigKey]
            private readonly ModConfigurationKey<Uri> TEST_URI = new ModConfigurationKey<Uri>("testUri", "Test Uri", () => null);
            [AutoRegisterConfigKey]
            private readonly ModConfigurationKey<Uri> TEST_INTERNAL = new ModConfigurationKey<Uri>("testInternal", "Test internal access only key, must be http or https", () => new Uri("https://example.com"), true, (uri)=>uri.Scheme == "https" || uri.Scheme == "http");
        //

        private static NeosModSettings Current; // To easily get the overriden fields of this mod
        private static readonly Dictionary<string, NeosModBase> configuredModList = new Dictionary<string, NeosModBase>();

        private static Slot optionsRoot;
        private static Slot modsRoot;

        private static MethodInfo fireConfigurationChangedEvent;

        public override void OnEngineInit()
        {
            Current = this;
            ModConfiguration.OnAnyConfigurationChanged += OnConfigurationChanged;
            GetConfiguration().OnThisConfigurationChanged += OnThisConfigurationChanged;

            Harmony harmony = new Harmony("me.badhaloninja.NeosModSettings");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(UserspaceScreensManager))]
        class ModSettingsScreen
        {
            [HarmonyPostfix]
            [HarmonyPatch("SetupDefaults")]
            public static void SetupDefaults(UserspaceScreensManager __instance)
            { // If you don't have an account or sign out this will be generated
                if (__instance.World != Userspace.UserspaceWorld) return;
                RadiantDash componentInParents = __instance.Slot.GetComponentInParents<RadiantDash>();

                RadiantDashScreen radiantDashScreen = componentInParents.AttachScreen("NML", color.Orange, NeosAssets.Graphics.Icons.Dash.Tools);
                GenerateNMLScreen(radiantDashScreen);
            }
            [HarmonyPostfix]
            [HarmonyPatch("OnLoading")]
            public static void OnLoading(UserspaceScreensManager __instance)
            { // If you have an account/sign in OnLoading triggers and replaces the contents generated by SetupDefaults
                if (__instance.World != Userspace.UserspaceWorld) return;
                RadiantDash componentInParents = __instance.Slot.GetComponentInParents<RadiantDash>();

                RadiantDashScreen radiantDashScreen = componentInParents.AttachScreen("NML", color.Orange, NeosAssets.Graphics.Icons.Dash.Tools);
                GenerateNMLScreen(radiantDashScreen);
            }

            private static void GenerateNMLScreen(RadiantDashScreen radiantDashScreen)
            {
                Slot screenSlot = radiantDashScreen.Slot;
                screenSlot.OrderOffset = 70; // Settings Screen is 60, Exit screen is set to int.MaxValue 
                screenSlot.PersistentSelf = false; // So it doesn't save

                var ui = new UIBuilder(radiantDashScreen.ScreenCanvas);
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

                modsRoot = ui.Root;
                GenerateModButtons(ui);


                // Config Panel
                ui.NestInto(right);

                BuildNMSInfo(ui, out RectTransform configUiRoot); // Shows when no mod is selected

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
                optionsRoot = ui.Root;
                //



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

                defaultsBtnLabelDrive.State.SyncWithVariable("Config/_includeInternal");
                defaultsBtnLabelDrive.TargetField.TryLink(defaultsBtn.LabelTextField);

                defaultsBtn.RequireLockInToPress.Value = true; // So you can scroll with laser without worrying about pressing it
                defaultsBtn.LocalReleased += ResetCurrentConfig;


                var space = screenSlot.AttachComponent<DynamicVariableSpace>();
                space.SpaceName.Value = "Config";
                
                var highlightColor = screenSlot.AttachComponent<DynamicValueVariable<color>>();
                highlightColor.VariableName.Value = "Config/_highlightTint";
                highlightColor.Value.Value = Current.GetConfiguration().GetValue(Current.HIGHLIGHT_TINT);

                var selectedModVar = screenSlot.AttachComponent<DynamicValueVariable<string>>();
                selectedModVar.VariableName.Value = "Config/SelectedMod";
                selectedModVar.Value.OnValueChange += GenerateConfigItems; // Regen Config items on change
            }
            private static void BuildNMSInfo(UIBuilder ui, out RectTransform content)
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


                Uri githubMark = new Uri("neosdb:///0c2ea8c328f68cc70eaa017a17cda0533895f1bbaa8764db9646770cd1b1a0b4.png");
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
                List<NeosModBase> mods = new List<NeosModBase>(ModLoader.Mods());
                List<NeosModBase> configuredMods = mods
                    .Where(m => m.GetConfiguration() != null) // Get all mods with configs
                    .ToList();


                var dVar = ui.Root.GetComponentOrAttach<DynamicValueVariable<string>>(out bool varAttached);
                if (varAttached)
                {
                    dVar.VariableName.Value = "Config/SelectedMod";
                }
                bool flag = configuredModList.Count == 0;
                var config = Current.GetConfiguration();
                foreach (NeosModBase mod in configuredMods)
                {

                    int configCount = mod.GetConfiguration().ConfigurationItemDefinitions
                        .Where(c => (config.GetValue(Current.SHOW_INTERNAL) || !c.InternalAccessOnly) &&
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



                /*// Debug stuff, Remove this later
                ui.Button("Open inspector").LocalPressed += (btn, be) =>
                {
                    Slot slot = btn.Slot.LocalUserSpace.AddSlot("Inspector", true);
                    SceneInspector sceneInspector = slot.AttachComponent<SceneInspector>();
                    sceneInspector.Root.Target = optionsRoot;

                    //slot.PositionInFrontOfUser(float3.Backward); // This is what parents under the overlay manager apparently

                    slot.LocalUser.GetPointInFrontOfUser(out float3 globalPosition, out floatQ globalRotation, float3.Backward, null, 0.7f, true);

                    slot.GlobalPosition = globalPosition;
                    slot.GlobalRotation = globalRotation;
                };*/
            }

            public static void GenerateConfigItems(SyncField<string> syncField = null)
            {
                optionsRoot.DestroyChildren(); // Clear configs

                string SelectedMod;
                if(syncField != null)
                {
                    SelectedMod = syncField.Value;
                }
                else
                {
                    optionsRoot.TryReadDynamicValue("Config/SelectedMod", out SelectedMod);
                }
                


                // Reset footer
                optionsRoot.TryWriteDynamicValue("Config/SelectedMod.Name", "");
                optionsRoot.TryWriteDynamicValue("Config/SelectedMod.Author", "");
                optionsRoot.TryWriteDynamicValue("Config/SelectedMod.Version", "");
                optionsRoot.TryWriteDynamicValue<Uri>("Config/SelectedMod.Uri", null);

                if (String.IsNullOrWhiteSpace(SelectedMod) || !configuredModList.TryGetValue(SelectedMod, out NeosModBase mod) || mod == null)
                    return; // Skip if no mod is selected

                // Set footer values
                optionsRoot.TryWriteDynamicValue("Config/SelectedMod.Name", mod.Name);
                optionsRoot.TryWriteDynamicValue("Config/SelectedMod.Author", mod.Author);
                optionsRoot.TryWriteDynamicValue("Config/SelectedMod.Version", mod.Version);

                Uri.TryCreate(mod.Link, UriKind.RelativeOrAbsolute, out Uri modUri); // Catch invalid uris just incase
                optionsRoot.TryWriteDynamicValue("Config/SelectedMod.Uri", modUri);


                UIBuilder ui = new UIBuilder(optionsRoot);
                RadiantUI_Constants.SetupDefaultStyle(ui);

                // Mod name at top of config
                /*ui.Text(mod.Name);
                ui.Spacer(32f);*/
                ui.Style.PreferredHeight = 24f;

                ModConfiguration config = mod.GetConfiguration();


                var i = 0;
                foreach (ModConfigurationKey key in config.ConfigurationItemDefinitions)
                { // Generate field for every supported config
                    if (!Current.GetConfiguration().GetValue(Current.SHOW_INTERNAL) && key.InternalAccessOnly) continue; // Skip internal keys sometimes
                    var item = GenerateConfigFieldOfType(key.ValueType(), ui, SelectedMod, config, key);
                    if(item == null) continue;


                    item.ForeachComponentInChildren<Button>(button => button.RequireLockInToPress.Value = true);



                    if (!Current.GetConfiguration().GetValue(Current.HIGHLIGHT_ITEMS)) continue;
                    if (i % 2 == 1)
                    {
                        var bg = item.AddSlot("Background");
                        bg.OrderOffset = -1;
                        var rect = bg.AttachComponent<RectTransform>();
                        rect.AnchorMin.Value = new float2(-0.005f, 0f);
                        rect.AnchorMax.Value = new float2(1.005f, 1f);
                        bg.AttachComponent<Image>().Tint.DriveFromVariable("Config/_highlightTint");
                    }
                    i++;
                }
            }

            public static Slot GenerateConfigFieldOfType(Type type, UIBuilder ui, string ModName, ModConfiguration config, ModConfigurationKey key)
            { // Generics go brr
                if (type == typeof(Type))
                {
                    return GenerateConfigTypeField(ui, ModName, config, key);
                }

                var method = typeof(ModSettingsScreen).GetMethod(nameof(GenerateConfigField)); // Get MethodInfo 
                var genMethod = method.MakeGenericMethod(type); // Convert to whatever type requested
                object[] args = new object[] { ui, ModName, config, key }; // Pass the arguments
                
                return (Slot)genMethod.Invoke(null, args); // Run the method
            }
            public static Slot GenerateConfigField<T>(UIBuilder ui, string ModName, ModConfiguration config, ModConfigurationKey key)
            {
                if (!DynamicValueVariable<T>.IsValidGenericType) return null; // Check if supported type

                ui.Style.MinHeight = Current.GetConfiguration().GetValue(Current.ITEM_HEIGHT);
                if (typeof(T).IsMatrixType())
                { // If it is a matrix adjust the height of the field 
                    int2 matrixDimensions = typeof(T).GetMatrixDimensions();
                    ui.Style.MinHeight = Math.Max(ui.Style.MinHeight, matrixDimensions.y * 24 + (matrixDimensions.y - 1) * 4);
                }
                Slot root = ui.Empty("ConfigElement");
                ui.NestInto(root);

                var dynvar = root.AttachComponent<DynamicValueVariable<T>>();
                dynvar.VariableName.Value = $"Config/{ModName}.{key.Name}";

                dynvar.Value.Value = config.TryGetValue(key, out object cv) ? (T)cv : Coder<T>.Default; // Set initial value
                dynvar.Value.OnValueChange += (syncF) => // Cursed solution, I know
                { // Update config
                    var typedKey = key as ModConfigurationKey<T>;

                    bool isSet = config.TryGetValue(typedKey, out T configValue);
                    bool wasModified = !configValue.Equals(syncF.Value) && syncF.Value.Equals(syncF.Value);
                    if (isSet && !wasModified) return; // Skip if new value is unmodified or is logically inconsistent (self != self)

                    if (!key.Validate(syncF.Value))
                    { // Fallback if validation fails
                        Debug($"Failed Validation for {dynvar.VariableName.Value}");
                        // Writing to the variable here breaks the editor
                        // Also you wouldn't want you are typing to be reset while typing it
                        if (!optionsRoot.LocalUser.IsDirectlyInteracting())
                        { // Reset value here if failed validation when not using text editor, via the clear button for strings for example
                            syncF.World.RunInUpdates(1, () =>
                            { // Give OnReset time to unblock the field
                                dynvar.Value.Value = isSet ? configValue : Coder<T>.Default; // Set to old value if is set Else set to default for that value
                            });
                        }
                        return; // Skip updating config
                    }

                    config.Set(typedKey, syncF.Value, "NeosModSettings variable change");
                };

                bool nameAsKey = String.IsNullOrWhiteSpace(key.Description);
                string localeText = nameAsKey ? key.Name : key.Description;
                string format = "{0}";
                if (Current.GetConfiguration().GetValue(Current.SHOW_NAMES) && !nameAsKey)
                {
                    format = $"<i><b>{key.Name}</i></b> - " + "{0}";
                }

                if (key.InternalAccessOnly) format = $"<color=#dec15b>{format}</color>";

                RadiantUI_Constants.SetupDefaultStyle(ui);

                ui.Style.TextAutoSizeMax = Current.GetConfiguration().GetValue(Current.ITEM_HEIGHT);
                var localized = new LocaleString(localeText, format, true, true, null);
                ui.HorizontalElementWithLabel<Component>(localized, 0.7f, () =>
                {// Using HorizontalElementWithLabel because it formats nicer than SyncMemberEditorBuilder with text
                    SyncMemberEditorBuilder.Build(dynvar.Value, null, dynvar.GetSyncMemberFieldInfo(4), ui); // Using null for name makes it skip generating text
                    // Can't recolor fields because PrimitiveMemeberEditor sets the colors on changes

                    // This is horrid, I have given up trying to get it to work in the dynvar on changed event
                    // And because of matrixes there can be multiple memberEditors
                    // Lambdas all the way down!
                    ui.Root.ForeachComponentInChildren<PrimitiveMemberEditor>((pme) => // ;-;
                    { // Get every text editor from each primitive member editor

                        /*  This code is to fit the fields into the RadiantUI style 
                         *  It is commented bc I dislike that it flashes white for a frame when generated
                         *  Until I figure out a workaround this will be commented
                         *  
                        var _btn = pme.GetSyncMember(9) as SyncRef<Button>;
                        _btn.Target = null; // Clear the button ref bc Primitive member editor sets color on changes incase of drives and stuff
                                            // Not needed in this use case and I wanna recolor buttons
                        
                        // Using for each instead of just using the previous ref so I can include the reset buttons as well in one go
                        pme.Slot.ForeachComponentInChildren<Button>((btn) => {
                            btn.SetColors(RadiantUI_Constants.BUTTON_COLOR);
                            btn.Slot.GetComponentInChildren<Text>().Color.Value = RadiantUI_Constants.TEXT_COLOR;
                        }); 
                        */


                        SyncRef<TextEditor> _textEditor = pme.GetSyncMember(7) as SyncRef<TextEditor>; // Get TextEditor from PrimitiveMemberEditor
                        if (_textEditor == null) return;
                        _textEditor.Target.LocalEditingFinished += (te) =>
                        { // Value Validation
                            bool isSet = config.TryGetValue(key, out object configValue);
                            if (!key.Validate(dynvar.Value.Value))
                            { // Fallback if validation fails
                                Debug($"Failed Validation for {dynvar.VariableName.Value}");
                                dynvar.Value.Value = isSet ? (T)configValue : Coder<T>.Default; // Set to old value if is set Else set to default for that value
                                return;
                            }
                        };
                    });

                    ui.Root.ForeachComponentInChildren<Text>((text) =>
                    { // Make value path text readable
                        // XYZW, RGBA, etc.
                        if (text.Slot.Parent.GetComponent<Button>() != null) return; // Ignore text under buttons
                        text.Color.Value = RadiantUI_Constants.TEXT_COLOR;
                    });

                    return null; // HorizontalElementWithLabel requires a return type that implements a component
                });

                ui.Style.MinHeight = -1f;
                ui.NestOut();

                return root;
            }
            public static Slot GenerateConfigTypeField(UIBuilder ui, string ModName, ModConfiguration config, ModConfigurationKey key)
            { /* *:* )
               * I wanted these field generation functions to be more dynamic and not have to have a separate one just for type Type
               * but I am just too tired bc of current events
               *
               *
               */
                Debug($"GenerateConfigField for type Type");

                ui.Style.MinHeight = Current.GetConfiguration().GetValue(Current.ITEM_HEIGHT);
                Slot root = ui.Empty("ConfigElement");
                ui.NestInto(root);

                var typeField = root.AttachComponent<TypeField>();
                var dynvar = root.AttachComponent<DynamicReferenceVariable<SyncType>>();
                dynvar.VariableName.Value = $"Config/{ModName}.{key.Name}";

                dynvar.Reference.TrySet(typeField.Type);


                var typedKey = key as ModConfigurationKey<Type>;

                typeField.Type.Value = config.TryGetValue(typedKey, out Type cv) ? cv : null; // Set initial value
                typeField.Type.OnValueChange += (syncF) => // Cursed solution, I know
                { // Update config

                    bool isSet = config.TryGetValue(typedKey, out Type configValue);
                    if (isSet && configValue.Equals(syncF.Value)) return; // Skip if new value is equal to old

                    if (!key.Validate(syncF.Value))
                    { // Fallback if validation fails
                        Debug($"Failed Validation for {dynvar.VariableName.Value}");
                        // Writing to the variable here breaks the editor
                        // Also you wouldn't want you are typing to be reset while typing it
                        return; // Skip updating config
                    }

                    config.Set(key, syncF.Value, "NeosModSettings variable change");
                };

                bool nameAsKey = String.IsNullOrWhiteSpace(key.Description);
                string localeText = nameAsKey ? key.Name : key.Description;
                string format = "{0}";
                if (Current.GetConfiguration().GetValue(Current.SHOW_NAMES) && !nameAsKey)
                {
                    format = $"<i><b>{key.Name}</i></b> - " + "{0}";
                }

                if (key.InternalAccessOnly) format = $"<color=#dec15b>{format}</color>";

                RadiantUI_Constants.SetupDefaultStyle(ui);

                ui.Style.TextAutoSizeMax = Current.GetConfiguration().GetValue(Current.ITEM_HEIGHT);
                var localized = new LocaleString(localeText, format, true, true, null);
                ui.HorizontalElementWithLabel<Component>(localized, 0.7f, () =>
                {// Using HorizontalElementWithLabel because it formats nicer than SyncMemberEditorBuilder with text
                    SyncMemberEditorBuilder.Build(typeField.Type, null, dynvar.GetSyncMemberFieldInfo(4), ui); // Using null for name makes it skip generating text
                    // Can't recolor fields because PrimitiveMemeberEditor sets the colors on changes


                    // This is horrid, I have given up trying to get it to work in the dynvar on changed event
                    // And because of matrixes there can be multiple memberEditors
                    // Lambdas all the way down!
                    ui.Root.ForeachComponentInChildren<PrimitiveMemberEditor>((pme) => // ;-;
                    { // Get every text editor from each primitive member editor

                        SyncRef<TextEditor> _textEditor = pme.GetSyncMember(7) as SyncRef<TextEditor>; // Get TextEditor from PrimitiveMemberEditor
                        if (_textEditor == null) return;
                        _textEditor.Target.LocalEditingFinished += (te) =>
                        { // Value Validation
                            bool isSet = config.TryGetValue(typedKey, out Type configValue);
                            if (!key.Validate(typeField.Type.Value))
                            { // Fallback if validation fails
                                Debug($"Failed Validation for {dynvar.VariableName.Value}");
                                typeField.Type.Value = isSet ? configValue : null; // Set to old value if is set Else set to default for that value
                                return;
                            }
                        };
                    });


                    ui.Root.ForeachComponentInChildren<Text>((text) =>
                    { // Make value path text readable
                        // XYZW, RGBA, etc.
                        if (text.Slot.Parent.GetComponent<Button>() != null) return; // Ignore text under buttons
                        text.Color.Value = RadiantUI_Constants.TEXT_COLOR;
                    });


                    return null; // HorizontalElementWithLabel requires a return type that implements a component
                });

                ui.Style.MinHeight = -1f;
                ui.NestOut();

                return root;
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

                // Incase you are resetting NMS config
                bool resetInternal = Current.GetConfiguration().GetValue(Current.RESET_INTERNAL);
                foreach (ModConfigurationKey key in config.ConfigurationItemDefinitions)
                { // Generate field for every supported config
                    if (!resetInternal && key.InternalAccessOnly) continue;


                    config.Unset(key);
                    if(fireConfigurationChangedEvent == null) {
                        // Private method moment :(
                        fireConfigurationChangedEvent = AccessTools.Method(typeof(ModConfiguration), "FireConfigurationChangedEvent");
                    }
                    // Unset does not trigger the config changed event
                    fireConfigurationChangedEvent.Invoke(config, new object[] { key, "NeosModSettings reset" });

                    // Get default type
                    object value = key.TryComputeDefault(out object defaultValue) ? defaultValue : key.ValueType().GetDefaultValue(); // How did I miss this extension??
                        //typeof(Coder<>).MakeGenericType(key.ValueType()).GetProperty("Default").GetValue(null); // Feel free do to a pull request at any time *:*)

                    optionsRoot.TryWriteDynamicValueOfType(key.ValueType(), $"Config/{selectedMod}.{key.Name}", value);
                }
            }
        }


        private void OnConfigurationChanged(ConfigurationChangedEvent @event)
        {
            if (@event.Label == "NeosModSettings variable change") return;
            Debug($"ConfigurationChangedEvent fired for mod \"{@event.Config.Owner.Name}\" Config \"{@event.Key.Name}\"");
            if (optionsRoot == null) return; // Skip if options root hasn't been generated yet
            

            if (!@event.Config.TryGetValue(@event.Key, out object value)) return; // Skip if failed to get the value
            string modName = $"{@event.Config.Owner.Author}.{@event.Config.Owner.Name}";
            optionsRoot.SyncWriteDynamicValueType(@event.Key.ValueType(), $"Config/{modName}.{@event.Key.Name}", value);
        }
        private void OnThisConfigurationChanged(ConfigurationChangedEvent @event)
        {
            if (optionsRoot == null) return; // Skip if options root hasn't been generated yet
            if (@event.Key == SHOW_INTERNAL && modsRoot != null)
            {
                // we need to regenerate mod buttons in case there is a mod with ONLY internal keys
                var ui = new UIBuilder(modsRoot);
                modsRoot.DestroyChildren();
                ModSettingsScreen.GenerateModButtons(ui);
            }

            if (@event.Key == RESET_INTERNAL)
            {
                optionsRoot.SyncWriteDynamicValue("Config/_includeInternal", @event.Config.GetValue(RESET_INTERNAL));
            }
            if (@event.Key == HIGHLIGHT_TINT)
            {
                optionsRoot.SyncWriteDynamicValue("Config/_highlightTint", @event.Config.GetValue(HIGHLIGHT_TINT));
            }
        }
    }
}