using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PetBestiary.Common.Configs;
using PetBestiary.Common.Players;
using PetBestiary.Common.Systems;
using PetBestiary.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace PetBestiary.UI;

public sealed class PetBestiaryUIState : UIState
{
    private const float PanelWidth = 920f;
    private const float PanelHeight = 560f;
    private const float PageRowTop = 2f;
    private const float TabRowTop = 14f;
    private const float FilterRowTop = 52f;
    private const float ContentTop = 96f;
    private const float GridLeft = 24f;
    private const float GridWidth = 536f;
    private const float ContentHeight = 392f;
    private const float DetailLeft = 584f;
    private const float DetailWidth = 312f;
    private const float ProgressBarTop = 532f;
    private const int PetsPerPage = 54;
    private const int PresetsPerPage = 8;

    private PetBestiaryPanel panel;
    private UIElement tabRow;
    private UIElement filterRow;
    private UIElement contentHost;
    private UIElement pageRow;
    private UIText pageText;
    private PetGridPanel petGrid;
    private PetDetailPanel petDetail;
    private PresetPanel presetPanel;
    private DebugPanel debugPanel;
    private SearchTextBox searchBox;
    private DyePalettePanel dyePalette;
    private BestiaryProgressBar progressBar;
    private BestiaryTab activeTab = BestiaryTab.NormalPets;
    private PetFilter activeFilter = PetFilter.All;
    private string activeSourceFilter;
    private string searchText = string.Empty;
    private string selectedPetKey;
    private string dyePalettePetKey;
    private bool dyePaletteApplyToActivePets;
    private int selectedPresetIndex = -1;
    private int page;
    private bool lastDebugMode;
    private KeyboardState previousKeyboardState;
    private bool previousMouseLeft;
    private bool previousMouseRight;

    public override void OnInitialize()
    {
        panel = new PetBestiaryPanel();
        panel.Width.Set(PanelWidth, 0f);
        panel.Height.Set(PanelHeight, 0f);
        panel.Left.Set((Main.screenWidth - PanelWidth) / 2f, 0f);
        panel.Top.Set((Main.screenHeight - PanelHeight) / 2f, 0f);
        panel.SetPadding(12f);
        Append(panel);

        tabRow = CreateRow(TabRowTop, 30f);
        filterRow = CreateRow(FilterRowTop, 30f);
        contentHost = CreateRow(ContentTop, ContentHeight);
        pageRow = CreateRow(PageRowTop, 40f);
        pageRow.Width.Set(240f, 0f);
        panel.Append(tabRow);
        panel.Append(filterRow);
        panel.Append(contentHost);
        panel.Append(pageRow);

        petGrid = new PetGridPanel(SelectPet, TogglePetFromGrid);
        petGrid.Left.Set(GridLeft, 0f);
        petGrid.Width.Set(GridWidth, 0f);
        petGrid.Height.Set(ContentHeight, 0f);

        petDetail = new PetDetailPanel(OpenDyePalette);
        petDetail.Left.Set(DetailLeft, 0f);
        petDetail.Width.Set(DetailWidth, 0f);
        petDetail.Height.Set(ContentHeight, 0f);

        presetPanel = new PresetPanel(SelectPreset);
        presetPanel.Left.Set(GridLeft, 0f);
        presetPanel.Width.Set(PanelWidth - GridLeft * 2f, 0f);
        presetPanel.Height.Set(ContentHeight, 0f);

        debugPanel = new DebugPanel(DebugUnlockAll, DebugRelockAll, DebugClearActive, DebugResyncNative, DebugClearDyes, DebugUnlockAllDyes, DebugRelockAllDyes);
        debugPanel.Left.Set(GridLeft, 0f);
        debugPanel.Width.Set(PanelWidth - GridLeft * 2f, 0f);
        debugPanel.Height.Set(ContentHeight, 0f);

        searchBox = new SearchTextBox(UpdateSearchText);
        searchBox.Left.Set(GridLeft, 0f);
        searchBox.Width.Set(248f, 0f);
        searchBox.Height.Set(28f, 0f);

        dyePalette = new DyePalettePanel(AssignPaletteDye, ClearAllDyesFromPalette, CloseDyePalette);
        dyePalette.Left.Set((PanelWidth - 448f) / 2f, 0f);
        dyePalette.Top.Set(92f, 0f);
        dyePalette.Width.Set(448f, 0f);
        dyePalette.Height.Set(356f, 0f);
        dyePalette.SetVisible(false);
        panel.Append(dyePalette);

        BuildPageNavigation();
        pageText = new UIText(string.Empty, 0.82f);
        pageText.Left.Set(12f, 0f);
        pageText.Top.Set(0f, 0f);
        pageText.Width.Set(82f, 0f);
        pageRow.Append(pageText);

        progressBar = new BestiaryProgressBar();
        progressBar.Left.Set(24f, 0f);
        progressBar.Top.Set(ProgressBarTop, 0f);
        progressBar.Width.Set(PanelWidth - 48f, 0f);
        progressBar.Height.Set(10f, 0f);
        panel.Append(progressBar);

        CloseIconButton closeButton = new(PetBestiaryUISystem.Close);
        closeButton.Left.Set(PanelWidth - 64f, 0f);
        closeButton.Top.Set(-6f, 0f);
        closeButton.Width.Set(30f, 0f);
        closeButton.Height.Set(30f, 0f);
        panel.Append(closeButton);

        RebuildStaticRows();
    }

    public override void Update(GameTime gameTime)
    {
        KeyboardState keyboardState = Keyboard.GetState();
        bool leftClicked = Main.mouseLeft && !previousMouseLeft;
        bool rightClicked = Main.mouseRight && !previousMouseRight;
        if (keyboardState.IsKeyDown(Keys.Escape) && previousKeyboardState.IsKeyUp(Keys.Escape))
        {
            CloseDyePalette();
            searchBox?.Blur();
            PetBestiaryUISystem.Close();
            previousKeyboardState = keyboardState;
            previousMouseLeft = Main.mouseLeft;
            previousMouseRight = Main.mouseRight;
            return;
        }

        UpdateSearchFocus(leftClicked || rightClicked);
        base.Update(gameTime);

        if (panel.IsMouseHovering || panel.Dragging)
        {
            Main.LocalPlayer.mouseInterface = true;
        }

        Refresh();
        previousKeyboardState = keyboardState;
        previousMouseLeft = Main.mouseLeft;
        previousMouseRight = Main.mouseRight;
    }

    private void UpdateSearchFocus(bool mouseClicked)
    {
        if (searchBox == null || !searchBox.IsFocused)
        {
            return;
        }

        if (!IsPetTab())
        {
            searchBox.Blur();
            return;
        }

        if (mouseClicked)
        {
            Vector2 mousePosition = Main.MouseScreen;
            if (!searchBox.ContainsPoint(mousePosition))
            {
                searchBox.Blur();
            }
        }
    }

    public void DismissTransientUi()
    {
        CloseDyePalette();
        searchBox?.Blur();
    }

    private UIElement CreateRow(float top, float height)
    {
        UIElement row = new();
        row.Width.Set(0f, 1f);
        row.Height.Set(height, 0f);
        row.Top.Set(top, 0f);
        return row;
    }

    private void RebuildStaticRows()
    {
        bool debugMode = ModContent.GetInstance<PetBestiaryConfig>().DebugMode;
        lastDebugMode = debugMode;

        if (!debugMode && activeTab == BestiaryTab.Debug)
        {
            activeTab = BestiaryTab.NormalPets;
            page = 0;
        }

        if (!IsPetTab())
        {
            searchBox?.Blur();
        }

        tabRow.RemoveAllChildren();
        List<UITextPanel<string>> tabs = new()
        {
            CreateButton("Normal Pets", 112f, () => SelectTab(BestiaryTab.NormalPets)),
            CreateButton("Light Pets", 112f, () => SelectTab(BestiaryTab.LightPets)),
            CreateButton("Presets", 112f, () => SelectTab(BestiaryTab.Presets))
        };

        if (debugMode)
        {
            tabs.Add(CreateButton("Debug", 88f, () => SelectTab(BestiaryTab.Debug)));
        }

        AddButtonsFromLeft(tabRow, 322f, tabs, 8f);

        filterRow.RemoveAllChildren();
        if (IsPetTab())
        {
            filterRow.Append(searchBox);
            searchBox.Top.Set(4f, 0f);
            AddButtonsFromLeft(filterRow, 322f, new[]
            {
                CreateButton($"Filters: {FilterName(activeFilter)}", 138f, CycleFilter, 0.66f),
                CreateButton($"Source: {SourceFilterName()}", 124f, CycleSourceFilter, 0.66f),
                CreateButton("Dye All", 76f, OpenDyeAllPalette, 0.66f),
                CreateButton("Equip All", 88f, EquipAllCurrentTab, 0.66f),
                CreateButton("Unequip All", 104f, UnequipAllCurrentTab, 0.66f)
            }, 8f);
        }
        else if (activeTab == BestiaryTab.Presets)
        {
            AddCenteredButtons(filterRow, new[]
            {
                CreateButton("Save Preset", 116f, SavePreset, 0.68f),
                CreateButton("Load", 76f, LoadSelectedPreset, 0.68f),
                CreateButton("Delete", 82f, DeleteSelectedPreset, 0.68f)
            }, 8f);
        }
    }

    private UITextPanel<string> CreateButton(string label, float width, Action action, float textScale = 0.72f, bool large = false, float height = 28f)
    {
        UITextPanel<string> button = new(label, textScale, large);
        button.Width.Set(width, 0f);
        button.Height.Set(height, 0f);
        button.OnLeftClick += (_, _) =>
        {
            action();
            SoundEngine.PlaySound(SoundID.MenuTick);
        };
        return button;
    }

    private void BuildPageNavigation()
    {
        pageRow.RemoveAllChildren();

        UITextPanel<string> previous = CreateButton("<", 32f, () => ChangePage(-1), 0.78f, false, 26f);
        previous.Left.Set(16f, 0f);
        previous.Top.Set(18f, 0f);
        pageRow.Append(previous);

        UITextPanel<string> next = CreateButton(">", 32f, () => ChangePage(1), 0.78f, false, 26f);
        next.Left.Set(54f, 0f);
        next.Top.Set(18f, 0f);
        pageRow.Append(next);

        DiceButton randomPet = new(RandomPetFromCurrentBestiary, "Random pet");
        randomPet.Left.Set(96f, 0f);
        randomPet.Top.Set(18f, 0f);
        randomPet.Width.Set(28f, 0f);
        randomPet.Height.Set(28f, 0f);
        pageRow.Append(randomPet);
    }

    private static void AddCenteredButtons(UIElement row, IReadOnlyList<UITextPanel<string>> buttons, float gap)
    {
        float totalWidth = buttons.Sum(button => button.Width.Pixels) + gap * Math.Max(0, buttons.Count - 1);
        float left = (PanelWidth - totalWidth) / 2f;

        foreach (UITextPanel<string> button in buttons)
        {
            button.Left.Set(left, 0f);
            button.Top.Set(0f, 0f);
            row.Append(button);
            left += button.Width.Pixels + gap;
        }
    }

    private static void AddButtonsFromLeft(UIElement row, float left, IReadOnlyList<UITextPanel<string>> buttons, float gap)
    {
        foreach (UITextPanel<string> button in buttons)
        {
            button.Left.Set(left, 0f);
            button.Top.Set(0f, 0f);
            row.Append(button);
            left += button.Width.Pixels + gap;
        }
    }

    private void SelectTab(BestiaryTab tab)
    {
        activeTab = tab;
        page = 0;
        selectedPresetIndex = -1;
        selectedPetKey = null;
        RebuildStaticRows();
    }

    private void SelectFilter(PetFilter filter)
    {
        activeFilter = filter;
        page = 0;
        selectedPetKey = null;
    }

    private void CycleFilter()
    {
        activeFilter = activeFilter switch
        {
            PetFilter.All => PetFilter.Unlocked,
            PetFilter.Unlocked => PetFilter.Locked,
            PetFilter.Locked => PetFilter.Active,
            _ => PetFilter.All
        };

        page = 0;
        selectedPetKey = null;
        CloseDyePalette();
        RebuildStaticRows();
    }

    private void CycleSourceFilter()
    {
        IReadOnlyList<string> sources = CurrentCategorySources();
        if (sources.Count == 0)
        {
            activeSourceFilter = null;
        }
        else if (string.IsNullOrWhiteSpace(activeSourceFilter))
        {
            activeSourceFilter = sources[0];
        }
        else
        {
            int index = -1;
            for (int i = 0; i < sources.Count; i++)
            {
                if (string.Equals(sources[i], activeSourceFilter, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }

            activeSourceFilter = index < 0 || index >= sources.Count - 1
                ? null
                : sources[index + 1];
        }

        page = 0;
        selectedPetKey = null;
        CloseDyePalette();
        RebuildStaticRows();
    }

    private void UpdateSearchText(string value)
    {
        searchText = value ?? string.Empty;
        page = 0;
        selectedPetKey = null;
        CloseDyePalette();
    }

    private void SelectPet(PetDefinition pet)
    {
        selectedPetKey = pet?.Key;
    }

    private void SelectPreset(int index)
    {
        selectedPresetIndex = index;
    }

    private void TogglePetFromGrid(PetDefinition pet)
    {
        PetBestiaryPlayer player = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>();
        if (pet == null || !player.IsUnlocked(pet.Key))
        {
            SoundEngine.PlaySound(SoundID.MenuClose);
            return;
        }

        if (!player.IsActive(pet.Key) && player.IsPetSlotLimitReached(pet.Category))
        {
            Main.NewText("Pet slot limit reached.", 255, 230, 130);
            SoundEngine.PlaySound(SoundID.MenuClose);
            return;
        }

        player.TryTogglePet(pet.Key);
        SoundEngine.PlaySound(SoundID.MenuTick);
    }

    private void ChangePage(int direction)
    {
        int totalPages = CurrentTotalPages();
        page = Math.Clamp(page + direction, 0, totalPages - 1);
    }

    private void RandomPetFromCurrentBestiary()
    {
        if (!IsPetTab())
        {
            Main.NewText("Open a pet tab before using random pet.", 255, 230, 130);
            return;
        }

        PetBestiaryPlayer player = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>();
        List<PetDefinition> candidates = CurrentPets()
            .Where(pet => player.IsUnlocked(pet.Key)
                && !player.IsActive(pet.Key)
                && !player.IsPetSlotLimitReached(pet.Category))
            .ToList();

        if (candidates.Count <= 0)
        {
            Main.NewText("No unlocked inactive pet can be activated from this tab.", 255, 230, 130);
            return;
        }

        PetDefinition selected = candidates[Main.rand.Next(candidates.Count)];
        if (!player.TryTogglePet(selected.Key))
        {
            Main.NewText("Could not activate a random pet.", 255, 230, 130);
            return;
        }

        selectedPetKey = selected.Key;
        IReadOnlyList<PetDefinition> pets = CurrentPets();
        int selectedIndex = pets.ToList().FindIndex(pet => pet.Key == selected.Key);
        if (selectedIndex >= 0)
        {
            page = selectedIndex / PetsPerPage;
        }

        Main.NewText($"Activated {selected.DisplayName}.", 180, 255, 180);
    }

    private void Refresh()
    {
        bool debugMode = ModContent.GetInstance<PetBestiaryConfig>().DebugMode;
        if (debugMode != lastDebugMode)
        {
            RebuildStaticRows();
        }

        contentHost.RemoveAllChildren();

        int totalPages = CurrentTotalPages();
        page = Math.Clamp(page, 0, totalPages - 1);

        PetBestiaryPlayer player = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>();
        if (IsPetTab())
        {
            IReadOnlyList<PetDefinition> pets = CurrentPets();
            if (selectedPetKey == null || !pets.Any(pet => pet.Key == selectedPetKey))
            {
                selectedPetKey = pets.Count > 0 ? pets[Math.Min(page * PetsPerPage, pets.Count - 1)].Key : null;
            }

            int start = page * PetsPerPage;
            List<PetDefinition> pagePets = pets.Skip(start).Take(PetsPerPage).ToList();
            PetDefinition selectedPet = pets.FirstOrDefault(pet => pet.Key == selectedPetKey);
            petGrid.SetPets(pagePets, player, selectedPetKey);
            petDetail.SetPet(selectedPet, player);
            contentHost.Append(petGrid);
            contentHost.Append(petDetail);
            pageText.SetText(BuildRangeText(start, pagePets.Count, pets.Count));
            (int unlockedCount, int totalCount) = CurrentCategoryProgress(player);
            progressBar.SetProgress(unlockedCount, totalCount, player.IsProgressionModeEnabledFor(CurrentCategory()));
        }
        else if (activeTab == BestiaryTab.Presets)
        {
            int start = page * PresetsPerPage;
            presetPanel.SetPresets(player, start, PresetsPerPage, selectedPresetIndex);
            contentHost.Append(presetPanel);
            pageText.SetText(BuildRangeText(start, Math.Min(PresetsPerPage, Math.Max(0, player.Presets.Count - start)), player.Presets.Count));
            progressBar.SetHidden();
        }
        else
        {
            debugPanel.SetVisible(debugMode);
            contentHost.Append(debugPanel);
            pageText.SetText("Debug");
            progressBar.SetHidden();
        }
    }

    private IReadOnlyList<PetDefinition> CurrentPets()
    {
        PetRegistry registry = PetRegistry.Instance;
        PetBestiaryPlayer player = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>();
        if (registry == null)
        {
            return Array.Empty<PetDefinition>();
        }

        PetCategory category = CurrentCategory();
        IEnumerable<PetDefinition> pets = registry.GetByCategory(category);
        pets = activeFilter switch
        {
            PetFilter.Unlocked => pets.Where(pet => player.IsUnlocked(pet.Key)),
            PetFilter.Locked => pets.Where(pet => !player.IsUnlocked(pet.Key)),
            PetFilter.Active => pets.Where(pet => player.IsActive(pet.Key)),
            _ => pets
        };

        if (!string.IsNullOrWhiteSpace(activeSourceFilter))
        {
            pets = pets.Where(pet => string.Equals(pet.SourceMod, activeSourceFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string query = searchText.Trim();
            pets = pets.Where(pet =>
                pet.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || pet.SourceMod.Contains(query, StringComparison.OrdinalIgnoreCase)
                || pet.Key.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return pets.ToList();
    }

    private IReadOnlyList<string> CurrentCategorySources()
    {
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null)
        {
            return Array.Empty<string>();
        }

        return registry.GetByCategory(CurrentCategory())
            .Select(pet => pet.SourceMod)
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(source => source == "Terraria" ? string.Empty : source)
            .ToList();
    }

    private (int Unlocked, int Total) CurrentCategoryProgress(PetBestiaryPlayer player)
    {
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null)
        {
            return (0, 0);
        }

        IReadOnlyList<PetDefinition> categoryPets = registry.GetByCategory(CurrentCategory()).ToList();
        return (categoryPets.Count(pet => player.IsUnlocked(pet.Key)), categoryPets.Count);
    }

    private int CurrentTotalPages()
    {
        if (IsPetTab())
        {
            return TotalPages(CurrentPets().Count, PetsPerPage);
        }

        if (activeTab == BestiaryTab.Presets)
        {
            return TotalPages(Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>().Presets.Count, PresetsPerPage);
        }

        return 1;
    }

    private void EquipAllCurrentTab()
    {
        if (!IsPetTab())
        {
            return;
        }

        int added = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>().EquipAll(CurrentCategory());
        Main.NewText($"Equipped {added} pet{(added == 1 ? string.Empty : "s")}.", 180, 255, 180);
    }

    private void UnequipAllCurrentTab()
    {
        if (!IsPetTab())
        {
            return;
        }

        int removed = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>().UnequipAll(CurrentCategory());
        Main.NewText($"Unequipped {removed} unlocked pet{(removed == 1 ? string.Empty : "s")}.", 180, 255, 180);
    }

    private void SavePreset()
    {
        PetBestiaryPlayer player = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>();
        player.SaveCurrentPreset();
        selectedPresetIndex = player.Presets.Count - 1;
        page = Math.Max(0, selectedPresetIndex / PresetsPerPage);
        Main.NewText("Saved current pet preset.", 180, 255, 180);
    }

    private void LoadSelectedPreset()
    {
        PetBestiaryPlayer player = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>();
        PresetLoadResult result = player.LoadPreset(selectedPresetIndex);
        if (!result.Loaded)
        {
            Main.NewText("Select a preset first.", 255, 230, 130);
            return;
        }

        string warning = result.MissingCount > 0 ? $" Skipped {result.MissingCount} missing pet{(result.MissingCount == 1 ? string.Empty : "s")}." : string.Empty;
        Main.NewText($"Loaded {player.Presets[selectedPresetIndex].Name}.{warning}", 180, 255, 180);
    }

    private void DeleteSelectedPreset()
    {
        PetBestiaryPlayer player = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>();
        if (selectedPresetIndex < 0 || selectedPresetIndex >= player.Presets.Count)
        {
            Main.NewText("Select a preset first.", 255, 230, 130);
            return;
        }

        string name = player.Presets[selectedPresetIndex].Name;
        if (player.DeletePreset(selectedPresetIndex))
        {
            selectedPresetIndex = Math.Min(selectedPresetIndex, player.Presets.Count - 1);
            Main.NewText($"Deleted {name}.", 255, 210, 150);
        }
    }

    private void DebugUnlockAll()
    {
        int added = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>().UnlockAllKnownPets();
        Main.NewText($"Debug: unlocked {added} known pet{(added == 1 ? string.Empty : "s")}.", 255, 230, 130);
    }

    private void DebugRelockAll()
    {
        Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>().RelockAllPets();
        page = 0;
        selectedPetKey = null;
        selectedPresetIndex = -1;
        Main.NewText("Debug: relocked all pets, cleared active pets, locks, and presets.", 255, 180, 130);
    }

    private void DebugClearActive()
    {
        Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>().ClearActivePets();
        Main.NewText("Debug: cleared active virtual pets.", 255, 230, 130);
    }

    private void DebugResyncNative()
    {
        PetSpawnManager.ClearNativePetState(Main.LocalPlayer);
        Main.NewText("Debug: cleared recognized native pet state.", 255, 230, 130);
    }

    private void DebugClearDyes()
    {
        int cleared = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>().ClearAllDyes();
        Main.NewText($"Debug: cleared {cleared} pet dye assignment{(cleared == 1 ? string.Empty : "s")}.", 255, 230, 130);
    }

    private void DebugUnlockAllDyes()
    {
        int added = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>().UnlockAllKnownDyes();
        Main.NewText($"Debug: unlocked {added} dye{(added == 1 ? string.Empty : "s")}.", 255, 230, 130);
    }

    private void DebugRelockAllDyes()
    {
        int removed = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>().RelockAllDyes();
        Main.NewText($"Debug: relocked {removed} dye{(removed == 1 ? string.Empty : "s")} and cleared pet dye assignments.", 255, 180, 130);
    }

    private void OpenDyePalette(PetDefinition pet)
    {
        PetBestiaryPlayer player = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>();
        if (pet == null || !player.IsUnlocked(pet.Key))
        {
            Main.NewText("Unlock this pet before assigning dye.", 255, 230, 130);
            return;
        }

        dyePalettePetKey = pet.Key;
        dyePaletteApplyToActivePets = false;
        string selectedDyeKey = player.TryGetDye(pet.Key, out PetDyeData dyeData) ? dyeData.DyeItemKey : string.Empty;
        dyePalette.SetDyes(player.GetUnlockedDyes(), selectedDyeKey);
        dyePalette.SetVisible(true);
    }

    private void OpenDyeAllPalette()
    {
        PetBestiaryPlayer player = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>();
        int activeCount = player.ActiveNormalPets.Count + player.ActiveLightPets.Count;
        if (activeCount <= 0)
        {
            Main.NewText("Activate at least one pet before using Dye All.", 255, 230, 130);
            return;
        }

        IReadOnlyList<PetDyeData> dyes = player.GetUnlockedDyes();
        if (dyes.Count <= 0)
        {
            Main.NewText("Pick up dye items to unlock them in the Prismatic Palette.", 255, 230, 130);
            return;
        }

        dyePalettePetKey = null;
        dyePaletteApplyToActivePets = true;
        dyePalette.SetDyes(dyes, string.Empty);
        dyePalette.SetVisible(true);
    }

    private void AssignPaletteDye(PetDyeData dyeData)
    {
        if (dyeData == null)
        {
            CloseDyePalette();
            return;
        }

        PetBestiaryPlayer player = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>();
        if (dyePaletteApplyToActivePets)
        {
            int assigned = player.TryAssignDyeToActivePets(dyeData);
            Main.NewText($"Assigned {dyeData.DisplayName} to {assigned} active pet{(assigned == 1 ? string.Empty : "s")}.", 180, 255, 180);
            CloseDyePalette();
            return;
        }

        if (string.IsNullOrWhiteSpace(dyePalettePetKey))
        {
            CloseDyePalette();
            return;
        }

        if (player.TryAssignDye(dyePalettePetKey, dyeData))
        {
            string petName = PetRegistry.Instance != null && PetRegistry.Instance.TryResolve(dyePalettePetKey, out PetDefinition pet)
                ? pet.DisplayName
                : "selected pet";
            Main.NewText($"Assigned {dyeData.DisplayName} to {petName}.", 180, 255, 180);
        }
        else
        {
            Main.NewText("That dye is not unlocked or no longer available.", 255, 230, 130);
        }

        CloseDyePalette();
    }

    private void ClearAllDyesFromPalette()
    {
        PetBestiaryPlayer player = Main.LocalPlayer.GetModPlayer<PetBestiaryPlayer>();
        int cleared = player.ClearAllDyes();
        dyePalette?.SetDyes(player.GetUnlockedDyes(), string.Empty);
        Main.NewText($"Cleared {cleared} pet dye assignment{(cleared == 1 ? string.Empty : "s")}.", 255, 230, 130);
    }

    private void CloseDyePalette()
    {
        dyePalettePetKey = null;
        dyePaletteApplyToActivePets = false;
        dyePalette?.SetVisible(false);
    }

    private bool IsPetTab()
    {
        return activeTab == BestiaryTab.NormalPets || activeTab == BestiaryTab.LightPets;
    }

    private PetCategory CurrentCategory()
    {
        return activeTab == BestiaryTab.LightPets ? PetCategory.Light : PetCategory.Normal;
    }

    private static int TotalPages(int entryCount, int pageSize)
    {
        return Math.Max(1, (int)Math.Ceiling(entryCount / (float)pageSize));
    }

    private static string BuildRangeText(int start, int visibleCount, int total)
    {
        if (total <= 0)
        {
            return "0-0 (0)";
        }

        return $"{start + 1}-{start + visibleCount} ({total})";
    }

    private static string FilterName(PetFilter filter)
    {
        return filter switch
        {
            PetFilter.Unlocked => "Unlocked",
            PetFilter.Locked => "Locked",
            PetFilter.Active => "Active",
            _ => "All"
        };
    }

    private string SourceFilterName()
    {
        return string.IsNullOrWhiteSpace(activeSourceFilter)
            ? "All"
            : ShortenLabel(activeSourceFilter, 12);
    }

    private static string ShortenLabel(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text[..Math.Max(0, maxLength - 3)] + "...";
    }
}

internal enum BestiaryTab
{
    NormalPets,
    LightPets,
    Presets,
    Debug
}

internal enum PetFilter
{
    All,
    Unlocked,
    Locked,
    Active
}

internal sealed class CloseIconButton : UIElement
{
    private readonly Action action;

    public CloseIconButton(Action action)
    {
        this.action = action;
        OnLeftClick += (_, _) =>
        {
            action();
            SoundEngine.PlaySound(SoundID.MenuClose);
        };
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        Color border = IsMouseHovering ? new Color(255, 226, 90) : Color.Black;
        Color fill = IsMouseHovering ? new Color(178, 54, 54) : new Color(128, 37, 44);
        Color inner = IsMouseHovering ? new Color(225, 92, 82) : new Color(170, 58, 61);

        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, border);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bounds.X + 3, bounds.Y + 3, bounds.Width - 6, bounds.Height - 6), fill);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bounds.X + 6, bounds.Y + 6, bounds.Width - 12, bounds.Height - 12), inner);

        DrawX(spriteBatch, bounds, Color.White);
        if (IsMouseHovering)
        {
            Main.instance.MouseText("Close");
        }
    }

    private static void DrawX(SpriteBatch spriteBatch, Rectangle bounds, Color color)
    {
        int left = bounds.X + 10;
        int top = bounds.Y + 10;
        int size = bounds.Width - 20;
        for (int i = 0; i <= size; i++)
        {
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(left + i, top + i, 2, 2), color);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(left + size - i, top + i, 2, 2), color);
        }
    }
}

internal sealed class DiceButton : UIElement
{
    private readonly string tooltip;

    public DiceButton(Action action, string tooltip)
    {
        this.tooltip = tooltip;
        OnLeftClick += (_, _) =>
        {
            action();
            SoundEngine.PlaySound(SoundID.MenuTick);
        };
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        Rectangle bounds = GetDimensions().ToRectangle();
        Color border = IsMouseHovering ? new Color(255, 226, 90) : Color.Black;
        Color fill = IsMouseHovering ? new Color(88, 112, 190) : new Color(58, 73, 145);

        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, border);
        Rectangle inner = new(bounds.X + 3, bounds.Y + 3, bounds.Width - 6, bounds.Height - 6);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, inner, fill);

        Rectangle dieShadow = new(bounds.X + 8, bounds.Y + 8, 16, 16);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, dieShadow, Color.Black * 0.35f);

        Rectangle die = new(bounds.X + 6, bounds.Y + 6, 16, 16);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, die, Color.White);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(die.X, die.Y, die.Width, 2), new Color(210, 220, 255));
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(die.X, die.Bottom - 2, die.Width, 2), new Color(170, 180, 220));
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(die.X, die.Y, 2, die.Height), new Color(210, 220, 255));
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(die.Right - 2, die.Y, 2, die.Height), new Color(170, 180, 220));

        DrawPip(spriteBatch, die.X + 4, die.Y + 4);
        DrawPip(spriteBatch, die.X + die.Width - 7, die.Y + 4);
        DrawPip(spriteBatch, die.X + die.Width / 2 - 1, die.Y + die.Height / 2 - 1);
        DrawPip(spriteBatch, die.X + 4, die.Y + die.Height - 7);
        DrawPip(spriteBatch, die.X + die.Width - 7, die.Y + die.Height - 7);

        if (IsMouseHovering)
        {
            Main.instance.MouseText(tooltip);
        }
    }

    private static void DrawPip(SpriteBatch spriteBatch, int x, int y)
    {
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x, y, 3, 3), new Color(33, 42, 84));
    }
}

internal sealed class PetBestiaryPanel : UIPanel
{
    private Vector2 dragOffset;

    public bool Dragging { get; private set; }

    public PetBestiaryPanel()
    {
        BackgroundColor = new Color(39, 55, 107, 238);
        BorderColor = new Color(114, 144, 214, 255);
    }

    public override void LeftMouseDown(UIMouseEvent evt)
    {
        base.LeftMouseDown(evt);

        if (evt.Target == this && evt.MousePosition.Y <= GetDimensions().Y + 42f)
        {
            dragOffset = evt.MousePosition - new Vector2(Left.Pixels, Top.Pixels);
            Dragging = true;
        }
    }

    public override void LeftMouseUp(UIMouseEvent evt)
    {
        base.LeftMouseUp(evt);
        Dragging = false;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (Dragging)
        {
            Left.Set(Main.mouseX - dragOffset.X, 0f);
            Top.Set(Main.mouseY - dragOffset.Y, 0f);
            Recalculate();
        }

        Left.Pixels = Utils.Clamp(Left.Pixels, 0f, Main.screenWidth - Width.Pixels);
        Top.Pixels = Utils.Clamp(Top.Pixels, 0f, Main.screenHeight - Height.Pixels);
    }
}

internal sealed class PetGridPanel : UIElement
{
    private const int Columns = 9;
    private const int Rows = 6;
    private const float CellSize = 52f;
    private const float CellGap = 8f;
    private readonly List<PetGridCell> cells = new();

    public PetGridPanel(Action<PetDefinition> selectPet, Action<PetDefinition> togglePet)
    {
        for (int i = 0; i < Columns * Rows; i++)
        {
            PetGridCell cell = new(selectPet, togglePet);
            cell.Left.Set((i % Columns) * (CellSize + CellGap), 0f);
            cell.Top.Set((i / Columns) * (CellSize + CellGap), 0f);
            cell.Width.Set(CellSize, 0f);
            cell.Height.Set(CellSize, 0f);
            Append(cell);
            cells.Add(cell);
        }
    }

    public void SetPets(IReadOnlyList<PetDefinition> pets, PetBestiaryPlayer player, string selectedKey)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            cells[i].SetPet(i < pets.Count ? pets[i] : null, player, selectedKey);
        }
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        CalculatedStyle dimensions = GetDimensions();
        Rectangle bounds = new((int)dimensions.X - 6, (int)dimensions.Y - 6, (int)dimensions.Width + 12, (int)dimensions.Height + 12);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, new Color(23, 33, 78, 190));
    }
}

internal sealed class PetGridCell : UIElement
{
    private readonly Action<PetDefinition> selectPet;
    private readonly Action<PetDefinition> togglePet;
    private PetDefinition pet;
    private PetBestiaryPlayer petPlayer;
    private bool selected;

    public PetGridCell(Action<PetDefinition> selectPet, Action<PetDefinition> togglePet)
    {
        this.selectPet = selectPet;
        this.togglePet = togglePet;
    }

    public void SetPet(PetDefinition definition, PetBestiaryPlayer player, string selectedKey)
    {
        pet = definition;
        petPlayer = player;
        selected = pet != null && pet.Key == selectedKey;
    }

    public override void LeftClick(UIMouseEvent evt)
    {
        base.LeftClick(evt);
        if (pet != null)
        {
            selectPet(pet);
            SoundEngine.PlaySound(SoundID.MenuTick);
        }
    }

    public override void RightClick(UIMouseEvent evt)
    {
        base.RightClick(evt);
        togglePet(pet);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        CalculatedStyle dimensions = GetDimensions();
        Rectangle bounds = new((int)dimensions.X, (int)dimensions.Y, (int)dimensions.Width, (int)dimensions.Height);
        Color border = selected ? Color.Gold : IsMouseHovering ? Color.LightSkyBlue : new Color(91, 124, 199);
        Color fill = IsMouseHovering ? new Color(45, 67, 138, 240) : new Color(34, 49, 111, 230);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, border);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bounds.X + 3, bounds.Y + 3, bounds.Width - 6, bounds.Height - 6), fill);

        if (pet == null)
        {
            return;
        }

        bool unlocked = petPlayer?.IsUnlocked(pet.Key) == true;
        bool active = petPlayer?.IsActive(pet.Key) == true;
        bool petLocked = petPlayer?.IsLocked(pet.Key) == true;
        Rectangle iconBounds = new(bounds.X + 6, bounds.Y + 6, bounds.Width - 12, bounds.Height - 12);
        PetIconRenderer.Draw(spriteBatch, pet, iconBounds, unlocked, selected || IsMouseHovering);

        if (!unlocked)
        {
            Utils.DrawBorderString(spriteBatch, "?", new Vector2(bounds.X + 23f, bounds.Y + 18f), Color.LightSteelBlue, 1.1f);
        }

        if (active)
        {
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bounds.X + 4, bounds.Bottom - 8, bounds.Width - 8, 4), Color.LightGreen);
        }

        if (petLocked)
        {
            Utils.DrawBorderString(spriteBatch, "L", new Vector2(bounds.Right - 15f, bounds.Y + 4f), Color.Gold, 0.62f);
        }

        if (IsMouseHovering)
        {
            Main.LocalPlayer.mouseInterface = true;
            string name = unlocked ? pet.DisplayName : "???";
            Main.hoverItemName = $"{name}\nLeft click to inspect\nRight click to toggle active";
        }
    }
}

internal sealed class PetDetailPanel : UIElement
{
    private readonly Action<PetDefinition> openDyePalette;
    private PetDefinition pet;
    private PetBestiaryPlayer petPlayer;
    private readonly UITextPanel<string> toggleButton;
    private readonly UITextPanel<string> lockButton;
    private readonly UITextPanel<string> clearDyeButton;
    private readonly DyeSwatchButton dyeSwatch;

    public PetDetailPanel(Action<PetDefinition> openDyePalette)
    {
        this.openDyePalette = openDyePalette;
        dyeSwatch = new DyeSwatchButton(AssignHeldDye, ClearDye);
        dyeSwatch.Left.Set(58f, 0f);
        dyeSwatch.Top.Set(308f, 0f);
        dyeSwatch.Width.Set(26f, 0f);
        dyeSwatch.Height.Set(26f, 0f);
        Append(dyeSwatch);

        clearDyeButton = CreateButton("Clear", ClearDye, 0.62f, 58f, 26f);
        clearDyeButton.Left.Set(232f, 0f);
        clearDyeButton.Top.Set(308f, 0f);
        Append(clearDyeButton);

        toggleButton = CreateButton("Toggle", ToggleActive);
        toggleButton.Left.Set(18f, 0f);
        toggleButton.Top.Set(350f, 0f);
        Append(toggleButton);

        lockButton = CreateButton("Lock", ToggleLock);
        lockButton.Left.Set(156f, 0f);
        lockButton.Top.Set(350f, 0f);
        Append(lockButton);
    }

    public void SetPet(PetDefinition definition, PetBestiaryPlayer player)
    {
        pet = definition;
        petPlayer = player;
        if (pet == null || petPlayer == null)
        {
            toggleButton.SetText("Toggle");
            lockButton.SetText("Lock");
            dyeSwatch.SetDye(null);
            return;
        }

        toggleButton.SetText(petPlayer.IsActive(pet.Key) ? "Deactivate" : "Activate");
        lockButton.SetText(petPlayer.IsLocked(pet.Key) ? "Unlock" : "Lock");
        dyeSwatch.SetDye(petPlayer.TryGetDye(pet.Key, out PetDyeData dyeData) ? dyeData : null);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        CalculatedStyle dimensions = GetDimensions();
        Rectangle bounds = new((int)dimensions.X, (int)dimensions.Y, (int)dimensions.Width, (int)dimensions.Height);
        DrawPanel(spriteBatch, bounds, new Color(72, 94, 174, 235), new Color(130, 162, 235));

        if (pet == null || petPlayer == null)
        {
            Utils.DrawBorderString(spriteBatch, "Select a pet", new Vector2(bounds.X + 90f, bounds.Y + 24f), Color.White, 0.9f);
            return;
        }

        bool unlocked = petPlayer.IsUnlocked(pet.Key);
        bool active = petPlayer.IsActive(pet.Key);
        bool locked = petPlayer.IsLocked(pet.Key);
        string categoryName = pet.Category == PetCategory.Light ? "Light Pet" : "Normal Pet";

        Utils.DrawBorderString(spriteBatch, unlocked ? pet.DisplayName : "???", new Vector2(bounds.X + 18f, bounds.Y + 16f), Color.White, 0.92f);
        Rectangle preview = new(bounds.X + 18, bounds.Y + 54, bounds.Width - 36, 86);
        DrawPanel(spriteBatch, preview, new Color(48, 64, 128, 230), new Color(116, 148, 222));
        DrawPetPreviewBackdrop(spriteBatch, preview);
        DyeRendererDebugMode debugMode = ModContent.GetInstance<PetBestiaryConfig>().DyeRendererDebugMode;
        bool hasPreviewDye = petPlayer.TryGetDye(pet.Key, out PetDyeData previewDye);
        DyeRendererDebugMode previewMode = ResolvePreviewMode(debugMode);
        int previewShader = hasPreviewDye ? ResolvePreviewShader(previewMode, previewDye) : 0;
        int previewDyeItemType = hasPreviewDye ? previewDye.DyeItemType : ItemID.None;
        Color? previewTint = debugMode == DyeRendererDebugMode.ForceLimePreview && hasPreviewDye ? Color.Lime : null;
        PetIconRenderer.Draw(spriteBatch, pet, preview, unlocked, true, previewShader, previewTint, previewMode, previewDyeItemType);

        int textY = bounds.Y + 154;
        DrawLine(spriteBatch, bounds.X + 18, ref textY, $"Status: {(unlocked ? active ? "Active" : "Unlocked" : "Locked")}", active ? Color.LightGreen : unlocked ? Color.Khaki : Color.LightGray);
        DrawLine(spriteBatch, bounds.X + 18, ref textY, $"Pet Lock: {(locked ? "Locked" : "Off")}", locked ? Color.Gold : Color.LightGray);
        DrawLine(spriteBatch, bounds.X + 18, ref textY, $"Category: {categoryName}", Color.White);
        DrawLine(spriteBatch, bounds.X + 18, ref textY, $"Source: {Shorten(pet.SourceMod, 30)}", Color.White);
        DrawLine(spriteBatch, bounds.X + 18, ref textY, $"Slots: {petPlayer.GetActiveCount(pet.Category)} / {petPlayer.GetCap(pet.Category)}", Color.White);

        Utils.DrawBorderString(spriteBatch, "Unlock:", new Vector2(bounds.X + 18f, bounds.Y + 260f), Color.Khaki, 0.74f);
        DrawWrapped(spriteBatch, string.IsNullOrWhiteSpace(pet.UnlockHint) ? "???" : pet.UnlockHint, new Vector2(bounds.X + 76f, bounds.Y + 260f), 34, 2, Color.White);

        string dyeName = petPlayer.TryGetDye(pet.Key, out PetDyeData dyeData) ? Shorten(dyeData.DisplayName, 22) : "None";
        Utils.DrawBorderString(spriteBatch, "Dye:", new Vector2(bounds.X + 18f, bounds.Y + 312f), Color.Khaki, 0.72f);
        Utils.DrawBorderString(spriteBatch, dyeName, new Vector2(bounds.X + 90f, bounds.Y + 312f), Color.White, 0.66f);
    }

    private void ToggleActive()
    {
        if (pet == null || petPlayer == null || !petPlayer.IsUnlocked(pet.Key))
        {
            Main.NewText("Pet is not unlocked.", 255, 230, 130);
            return;
        }

        if (!petPlayer.IsActive(pet.Key) && petPlayer.IsPetSlotLimitReached(pet.Category))
        {
            Main.NewText("Pet slot limit reached.", 255, 230, 130);
            return;
        }

        petPlayer.TryTogglePet(pet.Key);
    }

    private static int ResolvePreviewShader(DyeRendererDebugMode mode, PetDyeData dyeData)
    {
        return mode == DyeRendererDebugMode.ArmorShaderApplyByShaderId
            || mode == DyeRendererDebugMode.ArmorShaderApplySecondaryByShaderId
            || mode == DyeRendererDebugMode.ArmorShaderGetSecondaryShaderByShaderId
            || mode == DyeRendererDebugMode.ArmorShaderFromItemType
            ? dyeData.DyeShaderId
            : 0;
    }

    private static DyeRendererDebugMode ResolvePreviewMode(DyeRendererDebugMode mode)
    {
        if (mode == DyeRendererDebugMode.ForceLimePreview)
        {
            return mode;
        }

        if (mode == DyeRendererDebugMode.ArmorShaderApplySecondaryByShaderId
            || mode == DyeRendererDebugMode.ArmorShaderGetSecondaryShaderByShaderId
            || mode == DyeRendererDebugMode.ArmorShaderFromItemType)
        {
            return mode;
        }

        return DyeRendererDebugMode.ArmorShaderApplyByShaderId;
    }

    private void ToggleLock()
    {
        if (pet == null || petPlayer == null || !petPlayer.IsActive(pet.Key))
        {
            Main.NewText("Activate this pet before locking it.", 255, 230, 130);
            return;
        }

        petPlayer.TryToggleLock(pet.Key);
    }

    private void AssignHeldDye()
    {
        openDyePalette?.Invoke(pet);
    }

    private void ClearDye()
    {
        if (pet == null || petPlayer == null)
        {
            return;
        }

        if (petPlayer.ClearDye(pet.Key))
        {
            Main.NewText($"Cleared dye for {pet.DisplayName}.", 255, 230, 130);
        }
    }

    private static UITextPanel<string> CreateButton(string label, Action action, float textScale = 0.68f, float width = 120f, float height = 30f)
    {
        UITextPanel<string> button = new(label, textScale);
        button.Width.Set(width, 0f);
        button.Height.Set(height, 0f);
        button.OnLeftClick += (_, _) =>
        {
            action();
            SoundEngine.PlaySound(SoundID.MenuTick);
        };
        return button;
    }

    private static void DrawLine(SpriteBatch spriteBatch, int x, ref int y, string text, Color color)
    {
        Utils.DrawBorderString(spriteBatch, text, new Vector2(x, y), color, 0.72f);
        y += 22;
    }

    private static void DrawPanel(SpriteBatch spriteBatch, Rectangle bounds, Color fill, Color border)
    {
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, border);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bounds.X + 3, bounds.Y + 3, bounds.Width - 6, bounds.Height - 6), fill);
    }

    private static void DrawPetPreviewBackdrop(SpriteBatch spriteBatch, Rectangle bounds)
    {
        Rectangle inner = new(bounds.X + 4, bounds.Y + 4, bounds.Width - 8, bounds.Height - 8);
        Rectangle sky = new(inner.X, inner.Y, inner.Width, inner.Height - 20);
        Rectangle ground = new(inner.X, inner.Bottom - 22, inner.Width, 22);
        Rectangle grass = new(inner.X, inner.Bottom - 25, inner.Width, 5);

        spriteBatch.Draw(TextureAssets.MagicPixel.Value, sky, new Color(88, 140, 218, 210));
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, ground, new Color(67, 89, 58, 230));
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, grass, new Color(48, 163, 77, 230));
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(inner.X + 24, inner.Y + 26, 8, 34), new Color(91, 58, 42, 230));
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(inner.X + 12, inner.Y + 12, 34, 24), new Color(54, 151, 78, 210));
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(inner.Right - 42, inner.Y + 12, 18, 18), new Color(255, 238, 130, 220));
    }

    private static string Shorten(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text[..Math.Max(0, maxLength - 3)] + "...";
    }

    private static void DrawWrapped(SpriteBatch spriteBatch, string text, Vector2 position, int maxCharsPerLine, int maxLines, Color color)
    {
        string remaining = text ?? string.Empty;
        for (int line = 0; line < maxLines && remaining.Length > 0; line++)
        {
            string segment = Shorten(remaining, maxCharsPerLine);
            if (remaining.Length > maxCharsPerLine)
            {
                int split = remaining.LastIndexOf(' ', Math.Min(maxCharsPerLine, remaining.Length - 1));
                if (split > 6)
                {
                    segment = remaining[..split];
                    remaining = remaining[(split + 1)..];
                }
                else
                {
                    remaining = remaining[Math.Min(maxCharsPerLine, remaining.Length)..];
                }
            }
            else
            {
                remaining = string.Empty;
            }

            if (line == maxLines - 1 && remaining.Length > 0)
            {
                segment = Shorten(segment, Math.Max(0, maxCharsPerLine - 3)) + "...";
            }

            Utils.DrawBorderString(spriteBatch, segment, position + new Vector2(0f, line * 18f), color, 0.66f);
        }
    }
}

internal sealed class DyeSwatchButton : UIElement
{
    private readonly Action assignHeldDye;
    private readonly Action clearDye;
    private PetDyeData dyeData;

    public DyeSwatchButton(Action assignHeldDye, Action clearDye)
    {
        this.assignHeldDye = assignHeldDye;
        this.clearDye = clearDye;
    }

    public void SetDye(PetDyeData value)
    {
        dyeData = value;
    }

    public override void LeftClick(UIMouseEvent evt)
    {
        base.LeftClick(evt);
        assignHeldDye();
        SoundEngine.PlaySound(SoundID.MenuTick);
    }

    public override void RightClick(UIMouseEvent evt)
    {
        base.RightClick(evt);
        clearDye();
        SoundEngine.PlaySound(SoundID.MenuTick);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        CalculatedStyle dimensions = GetDimensions();
        Rectangle bounds = new((int)dimensions.X, (int)dimensions.Y, (int)dimensions.Width, (int)dimensions.Height);
        Color border = IsMouseHovering ? Color.LightSkyBlue : new Color(91, 124, 199);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, border);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4), new Color(34, 49, 111, 230));

        if (dyeData != null && PetDyeManager.TryResolveItemType(dyeData, out int itemType))
        {
            DrawItemIcon(spriteBatch, itemType, bounds);
        }
        else
        {
            Utils.DrawBorderString(spriteBatch, "-", new Vector2(bounds.X + 9f, bounds.Y + 3f), Color.LightGray, 0.78f);
        }

        if (IsMouseHovering)
        {
            Main.LocalPlayer.mouseInterface = true;
            Main.hoverItemName = dyeData?.HasShader == true
                ? $"{dyeData.DisplayName}\nLeft click to open Prismatic Palette\nRight click to clear"
                : "Dye: None\nLeft click to open Prismatic Palette";
        }
    }

    public static void DrawItemIcon(SpriteBatch spriteBatch, int itemType, Rectangle bounds)
    {
        if (itemType <= ItemID.None || itemType >= TextureAssets.Item.Length)
        {
            return;
        }

        try
        {
            Main.instance.LoadItem(itemType);
            Texture2D texture = TextureAssets.Item[itemType].Value;
            Rectangle source = Main.itemAnimations[itemType]?.GetFrame(texture) ?? texture.Bounds;
            float scale = Math.Min(1f, (bounds.Width - 6f) / Math.Max(source.Width, source.Height));
            Vector2 center = bounds.Center.ToVector2();
            spriteBatch.Draw(texture, center, source, Color.White, 0f, source.Size() / 2f, scale, SpriteEffects.None, 0f);
        }
        catch
        {
            Utils.DrawBorderString(spriteBatch, "?", new Vector2(bounds.X + 8f, bounds.Y + 4f), Color.Gray, 0.68f);
        }
    }
}

internal sealed class SearchTextBox : UIElement
{
    private readonly Action<string> onChanged;
    private readonly string placeholder;
    private string text = string.Empty;
    private bool focused;
    private bool ownsInputBlock;
    private KeyboardState previousKeyboardState;

    public bool IsFocused => focused;

    public SearchTextBox(Action<string> onChanged, string placeholder = "Search pets")
    {
        this.onChanged = onChanged;
        this.placeholder = placeholder;
    }

    public override void LeftClick(UIMouseEvent evt)
    {
        base.LeftClick(evt);
        Focus();
        SoundEngine.PlaySound(SoundID.MenuTick);
    }

    public override void RightClick(UIMouseEvent evt)
    {
        base.RightClick(evt);
        Clear();
        SoundEngine.PlaySound(SoundID.MenuTick);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (!focused)
        {
            ReleaseInputBlock();
            return;
        }

        Main.LocalPlayer.mouseInterface = true;
        SuppressGameplayInput();
        KeyboardState keyState = Keyboard.GetState();
        if (keyState.IsKeyDown(Keys.Enter) && previousKeyboardState.IsKeyUp(Keys.Enter))
        {
            Blur();
            previousKeyboardState = keyState;
            return;
        }

        string originalText = text;
        foreach (Keys key in keyState.GetPressedKeys())
        {
            if (!previousKeyboardState.IsKeyUp(key))
            {
                continue;
            }

            ApplyKey(key, keyState);
        }

        if (!string.Equals(originalText, text, StringComparison.Ordinal))
        {
            onChanged(text);
        }

        previousKeyboardState = keyState;
    }

    public void Focus()
    {
        if (!focused)
        {
            Main.clrInput();
            previousKeyboardState = Keyboard.GetState();
        }

        focused = true;
    }

    public void Blur()
    {
        if (!focused)
        {
            return;
        }

        focused = false;
        Main.clrInput();
        ReleaseInputBlock();
    }

    private void Clear()
    {
        if (text.Length <= 0)
        {
            return;
        }

        text = string.Empty;
        onChanged(text);
    }

    private void ApplyKey(Keys key, KeyboardState keyState)
    {
        if (key == Keys.Back)
        {
            if (text.Length > 0)
            {
                text = text[..^1];
            }

            return;
        }

        if (key == Keys.Delete)
        {
            Clear();
            return;
        }

        if (text.Length >= 28)
        {
            return;
        }

        if (TryGetSearchCharacter(key, keyState, out char character))
        {
            text += character;
        }
    }

    private static bool TryGetSearchCharacter(Keys key, KeyboardState keyState, out char character)
    {
        character = '\0';
        bool shift = keyState.IsKeyDown(Keys.LeftShift) || keyState.IsKeyDown(Keys.RightShift);

        if (key >= Keys.A && key <= Keys.Z)
        {
            char letter = (char)('a' + (key - Keys.A));
            character = shift ? char.ToUpperInvariant(letter) : letter;
            return true;
        }

        if (key >= Keys.D0 && key <= Keys.D9)
        {
            character = (char)('0' + (key - Keys.D0));
            return true;
        }

        if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
        {
            character = (char)('0' + (key - Keys.NumPad0));
            return true;
        }

        switch (key)
        {
            case Keys.Space:
                character = ' ';
                return true;
            case Keys.OemMinus:
                character = '-';
                return true;
            case Keys.OemPeriod:
                character = '.';
                return true;
            case Keys.OemComma:
                character = ',';
                return true;
            case Keys.OemQuotes:
                character = '\'';
                return true;
        }

        return false;
    }

    private void SuppressGameplayInput()
    {
        PlayerInput.WritingText = true;
        Main.blockInput = true;
        ownsInputBlock = true;
        Player player = Main.LocalPlayer;
        player.controlLeft = false;
        player.controlRight = false;
        player.controlUp = false;
        player.controlDown = false;
        player.controlJump = false;
        player.controlUseItem = false;
        player.controlUseTile = false;
        player.controlHook = false;
        player.controlMount = false;
        player.controlQuickHeal = false;
        player.controlQuickMana = false;
        player.controlThrow = false;
        player.controlSmart = false;
    }

    private void ReleaseInputBlock()
    {
        if (!ownsInputBlock)
        {
            return;
        }

        ownsInputBlock = false;
        Main.blockInput = false;
        PlayerInput.WritingText = false;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        CalculatedStyle dimensions = GetDimensions();
        Rectangle bounds = new((int)dimensions.X, (int)dimensions.Y, (int)dimensions.Width, (int)dimensions.Height);
        Color border = focused ? Color.Gold : IsMouseHovering ? Color.LightSkyBlue : new Color(91, 124, 199);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, border);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4), new Color(34, 49, 111, 235));

        string display = string.IsNullOrEmpty(text) ? placeholder : text;
        Color color = string.IsNullOrEmpty(text) ? Color.LightSteelBlue : Color.White;
        Utils.DrawBorderString(spriteBatch, display, new Vector2(bounds.X + 10f, bounds.Y + 7f), color, 0.62f);

        if (IsMouseHovering)
        {
            Main.LocalPlayer.mouseInterface = true;
            Main.hoverItemName = $"{placeholder}\nRight click to clear";
        }
    }
}

internal sealed class DyePalettePanel : UIPanel
{
    private const int Columns = 8;
    private const int Rows = 4;
    private const int DyesPerPage = Columns * Rows;
    private readonly Action<PetDyeData> selectDye;
    private readonly Action clearAllDyes;
    private readonly Action close;
    private readonly List<DyePaletteCell> cells = new();
    private readonly SearchTextBox searchBox;
    private readonly UIText pageText;
    private IReadOnlyList<PetDyeData> allDyes = Array.Empty<PetDyeData>();
    private IReadOnlyList<PetDyeData> filteredDyes = Array.Empty<PetDyeData>();
    private string selectedDyeKey = string.Empty;
    private string searchText = string.Empty;
    private int page;
    private bool visible;
    private bool previousMouseLeft;
    private bool previousMouseRight;

    public DyePalettePanel(Action<PetDyeData> selectDye, Action clearAllDyes, Action close)
    {
        this.selectDye = selectDye;
        this.clearAllDyes = clearAllDyes;
        this.close = close;
        BackgroundColor = new Color(39, 55, 107, 248);
        BorderColor = new Color(130, 162, 235);
        SetPadding(0f);

        UITextPanel<string> closeButton = new("X", 0.7f);
        closeButton.Left.Set(402f, 0f);
        closeButton.Top.Set(12f, 0f);
        closeButton.Width.Set(28f, 0f);
        closeButton.Height.Set(26f, 0f);
        closeButton.OnLeftClick += (_, _) =>
        {
            close();
            SoundEngine.PlaySound(SoundID.MenuClose);
        };
        Append(closeButton);

        searchBox = new SearchTextBox(UpdateSearchText, "Search dyes");
        searchBox.Left.Set(24f, 0f);
        searchBox.Top.Set(44f, 0f);
        searchBox.Width.Set(248f, 0f);
        searchBox.Height.Set(28f, 0f);
        Append(searchBox);

        UITextPanel<string> clearAllButton = new("Clear All", 0.62f);
        clearAllButton.Left.Set(292f, 0f);
        clearAllButton.Top.Set(44f, 0f);
        clearAllButton.Width.Set(92f, 0f);
        clearAllButton.Height.Set(28f, 0f);
        clearAllButton.OnLeftClick += (_, _) =>
        {
            clearAllDyes();
            SoundEngine.PlaySound(SoundID.MenuTick);
        };
        Append(clearAllButton);

        for (int i = 0; i < DyesPerPage; i++)
        {
            DyePaletteCell cell = new(SelectDye);
            cell.Left.Set(24f + (i % Columns) * 50f, 0f);
            cell.Top.Set(86f + (i / Columns) * 50f, 0f);
            cell.Width.Set(38f, 0f);
            cell.Height.Set(38f, 0f);
            Append(cell);
            cells.Add(cell);
        }

        DiceButton randomDye = new(SelectRandomDye, "Random dye");
        randomDye.Left.Set(70f, 0f);
        randomDye.Top.Set(312f, 0f);
        randomDye.Width.Set(28f, 0f);
        randomDye.Height.Set(28f, 0f);
        Append(randomDye);

        UITextPanel<string> previous = new("<", 0.82f);
        previous.Left.Set(122f, 0f);
        previous.Top.Set(312f, 0f);
        previous.Width.Set(38f, 0f);
        previous.Height.Set(28f, 0f);
        previous.OnLeftClick += (_, _) => ChangePage(-1);
        Append(previous);

        UITextPanel<string> next = new(">", 0.82f);
        next.Left.Set(288f, 0f);
        next.Top.Set(312f, 0f);
        next.Width.Set(38f, 0f);
        next.Height.Set(28f, 0f);
        next.OnLeftClick += (_, _) => ChangePage(1);
        Append(next);

        pageText = new UIText("0-0 (0)", 0.72f);
        pageText.Left.Set(178f, 0f);
        pageText.Top.Set(321f, 0f);
        pageText.Width.Set(96f, 0f);
        Append(pageText);
    }

    public void SetVisible(bool value)
    {
        visible = value;
        if (!visible)
        {
            searchBox.Blur();
        }
    }

    public void SetDyes(IReadOnlyList<PetDyeData> values, string selectedKey)
    {
        allDyes = values ?? Array.Empty<PetDyeData>();
        selectedDyeKey = selectedKey ?? string.Empty;
        ApplySearchFilter(resetPage: false);
        page = Math.Clamp(page, 0, TotalPages() - 1);
        RefreshCells();
    }

    public override bool ContainsPoint(Vector2 point)
    {
        return visible && base.ContainsPoint(point);
    }

    public override void Update(GameTime gameTime)
    {
        if (!visible)
        {
            return;
        }

        bool mouseClicked = (Main.mouseLeft && !previousMouseLeft) || (Main.mouseRight && !previousMouseRight);
        if (mouseClicked && searchBox.IsFocused && !searchBox.ContainsPoint(Main.MouseScreen))
        {
            searchBox.Blur();
        }

        base.Update(gameTime);
        if (IsMouseHovering)
        {
            Main.LocalPlayer.mouseInterface = true;
        }

        previousMouseLeft = Main.mouseLeft;
        previousMouseRight = Main.mouseRight;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!visible)
        {
            return;
        }

        base.Draw(spriteBatch);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);
        CalculatedStyle dimensions = GetDimensions();
        Utils.DrawBorderString(spriteBatch, "Prismatic Palette", new Vector2(dimensions.X + 22f, dimensions.Y + 16f), Color.White, 0.84f);
        if (allDyes.Count == 0)
        {
            Utils.DrawBorderString(spriteBatch, "Pick up dye items to unlock them here.", new Vector2(dimensions.X + 76f, dimensions.Y + 170f), Color.LightSteelBlue, 0.66f);
        }
        else if (filteredDyes.Count == 0)
        {
            Utils.DrawBorderString(spriteBatch, "No dyes match this search.", new Vector2(dimensions.X + 128f, dimensions.Y + 170f), Color.LightSteelBlue, 0.66f);
        }
    }

    private void ChangePage(int direction)
    {
        page = Math.Clamp(page + direction, 0, TotalPages() - 1);
        RefreshCells();
        SoundEngine.PlaySound(SoundID.MenuTick);
    }

    private int TotalPages()
    {
        return Math.Max(1, (int)Math.Ceiling(filteredDyes.Count / (float)DyesPerPage));
    }

    private void RefreshCells()
    {
        int start = page * DyesPerPage;
        for (int i = 0; i < cells.Count; i++)
        {
            int dyeIndex = start + i;
            PetDyeData dye = dyeIndex < filteredDyes.Count ? filteredDyes[dyeIndex] : null;
            cells[i].SetDye(dye, dye != null && string.Equals(dye.DyeItemKey, selectedDyeKey, StringComparison.Ordinal));
        }

        int visibleCount = Math.Min(DyesPerPage, Math.Max(0, filteredDyes.Count - start));
        pageText.SetText(filteredDyes.Count == 0 ? "0-0 (0)" : $"{start + 1}-{start + visibleCount} ({filteredDyes.Count})");
    }

    private void UpdateSearchText(string value)
    {
        searchText = value ?? string.Empty;
        ApplySearchFilter(resetPage: true);
        RefreshCells();
    }

    private void ApplySearchFilter(bool resetPage)
    {
        IEnumerable<PetDyeData> dyes = allDyes;
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string query = searchText.Trim();
            dyes = dyes.Where(dye =>
                dye.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || dye.DyeItemKey.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        filteredDyes = dyes.ToList();
        if (resetPage)
        {
            page = 0;
        }
        else
        {
            page = Math.Clamp(page, 0, TotalPages() - 1);
        }
    }

    private void SelectDye(PetDyeData dyeData)
    {
        if (dyeData == null)
        {
            return;
        }

        selectDye(dyeData);
        SoundEngine.PlaySound(SoundID.MenuTick);
    }

    private void SelectRandomDye()
    {
        if (filteredDyes.Count <= 0)
        {
            Main.NewText("No unlocked dyes are available for random selection.", 255, 230, 130);
            return;
        }

        SelectDye(filteredDyes[Main.rand.Next(filteredDyes.Count)]);
    }
}

internal sealed class DyePaletteCell : UIElement
{
    private readonly Action<PetDyeData> selectDye;
    private PetDyeData dyeData;
    private bool selected;

    public DyePaletteCell(Action<PetDyeData> selectDye)
    {
        this.selectDye = selectDye;
    }

    public void SetDye(PetDyeData value, bool isSelected)
    {
        dyeData = value;
        selected = isSelected;
    }

    public override void LeftClick(UIMouseEvent evt)
    {
        base.LeftClick(evt);
        selectDye(dyeData);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        CalculatedStyle dimensions = GetDimensions();
        Rectangle bounds = new((int)dimensions.X, (int)dimensions.Y, (int)dimensions.Width, (int)dimensions.Height);
        Color border = selected ? Color.Gold : IsMouseHovering ? Color.LightSkyBlue : new Color(91, 124, 199);
        Color fill = dyeData == null ? new Color(22, 30, 70, 180) : new Color(34, 49, 111, 235);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, border);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4), fill);

        if (dyeData == null)
        {
            return;
        }

        if (PetDyeManager.TryResolveItemType(dyeData, out int itemType))
        {
            DyeSwatchButton.DrawItemIcon(spriteBatch, itemType, bounds);
        }
        else
        {
            Utils.DrawBorderString(spriteBatch, "?", new Vector2(bounds.X + 13f, bounds.Y + 9f), Color.Gray, 0.7f);
        }

        if (IsMouseHovering)
        {
            Main.LocalPlayer.mouseInterface = true;
            Main.hoverItemName = dyeData.DisplayName;
        }
    }
}

internal sealed class PresetPanel : UIElement
{
    private readonly Action<int> selectPreset;
    private readonly List<PresetListEntry> entries = new();
    private PetBestiaryPlayer petPlayer;
    private int selectedIndex;

    public PresetPanel(Action<int> selectPreset)
    {
        this.selectPreset = selectPreset;
        for (int i = 0; i < 8; i++)
        {
            PresetListEntry entry = new(SelectEntry);
            entry.Left.Set(12f, 0f);
            entry.Top.Set(12f + i * 40f, 0f);
            entry.Width.Set(520f, 0f);
            entry.Height.Set(34f, 0f);
            Append(entry);
            entries.Add(entry);
        }
    }

    public void SetPresets(PetBestiaryPlayer player, int startIndex, int pageSize, int selectedPresetIndex)
    {
        petPlayer = player;
        selectedIndex = selectedPresetIndex;
        for (int i = 0; i < entries.Count; i++)
        {
            int presetIndex = startIndex + i;
            bool visible = i < pageSize && presetIndex < player.Presets.Count;
            entries[i].SetPreset(visible ? player.Presets[presetIndex] : null, visible ? presetIndex : -1, player, presetIndex == selectedPresetIndex);
        }
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        CalculatedStyle dimensions = GetDimensions();
        Rectangle bounds = new((int)dimensions.X, (int)dimensions.Y, (int)dimensions.Width, (int)dimensions.Height);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, new Color(23, 33, 78, 190));

        Rectangle detail = new(bounds.X + 560, bounds.Y + 12, bounds.Width - 580, bounds.Height - 24);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, detail, new Color(72, 94, 174, 235));
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(detail.X, detail.Y, detail.Width, 2), new Color(130, 162, 235));

        if (petPlayer == null || selectedIndex < 0 || selectedIndex >= petPlayer.Presets.Count)
        {
            Utils.DrawBorderString(spriteBatch, "Select a preset", new Vector2(detail.X + 20f, detail.Y + 18f), Color.White, 0.82f);
            Utils.DrawBorderString(spriteBatch, "Use the buttons above to save, load, or delete.", new Vector2(detail.X + 20f, detail.Y + 48f), Color.LightGray, 0.66f);
            return;
        }

        PetPreset preset = petPlayer.Presets[selectedIndex];
        int missing = CountMissingPets(preset);
        Utils.DrawBorderString(spriteBatch, preset.Name, new Vector2(detail.X + 20f, detail.Y + 18f), Color.White, 0.86f);
        Utils.DrawBorderString(spriteBatch, $"Normal pets: {preset.NormalPets.Count}", new Vector2(detail.X + 20f, detail.Y + 58f), Color.White, 0.72f);
        Utils.DrawBorderString(spriteBatch, $"Light pets: {preset.LightPets.Count}", new Vector2(detail.X + 20f, detail.Y + 84f), Color.White, 0.72f);
        Utils.DrawBorderString(spriteBatch, $"Locked pets: {preset.LockedPets.Count}", new Vector2(detail.X + 20f, detail.Y + 110f), Color.White, 0.72f);
        Utils.DrawBorderString(spriteBatch, $"Dyes: {preset.PetDyes.Count}", new Vector2(detail.X + 20f, detail.Y + 136f), Color.White, 0.72f);
        Utils.DrawBorderString(spriteBatch, missing > 0 ? $"Missing skipped on load: {missing}" : "All saved pets available", new Vector2(detail.X + 20f, detail.Y + 172f), missing > 0 ? Color.Orange : Color.LightGreen, 0.68f);
    }

    private void SelectEntry(int index)
    {
        selectPreset(index);
    }

    private int CountMissingPets(PetPreset preset)
    {
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null || petPlayer == null)
        {
            return preset.NormalPets.Count + preset.LightPets.Count;
        }

        int missing = 0;
        missing += preset.NormalPets.Count(key => !petPlayer.IsUnlocked(key) || !registry.TryResolve(key, PetCategory.Normal, out _));
        missing += preset.LightPets.Count(key => !petPlayer.IsUnlocked(key) || !registry.TryResolve(key, PetCategory.Light, out _));
        return missing;
    }
}

internal sealed class PresetListEntry : UIElement
{
    private readonly Action<int> select;
    private PetPreset preset;
    private PetBestiaryPlayer petPlayer;
    private int index;
    private bool selected;

    public PresetListEntry(Action<int> select)
    {
        this.select = select;
    }

    public void SetPreset(PetPreset value, int presetIndex, PetBestiaryPlayer player, bool isSelected)
    {
        preset = value;
        index = presetIndex;
        petPlayer = player;
        selected = isSelected;
    }

    public override void LeftClick(UIMouseEvent evt)
    {
        base.LeftClick(evt);
        if (preset != null)
        {
            select(index);
            SoundEngine.PlaySound(SoundID.MenuTick);
        }
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        CalculatedStyle dimensions = GetDimensions();
        Rectangle bounds = new((int)dimensions.X, (int)dimensions.Y, (int)dimensions.Width, (int)dimensions.Height);
        if (preset == null)
        {
            return;
        }

        int missing = CountMissingPets();
        Color border = selected ? Color.Gold : new Color(91, 124, 199);
        Color fill = missing > 0 ? new Color(82, 64, 92, 230) : new Color(42, 58, 122, 230);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, border);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4), fill);
        Utils.DrawBorderString(spriteBatch, preset.Name, new Vector2(bounds.X + 10f, bounds.Y + 7f), Color.White, 0.72f);
        Utils.DrawBorderString(spriteBatch, $"{preset.NormalPets.Count} normal, {preset.LightPets.Count} light", new Vector2(bounds.Right - 190f, bounds.Y + 7f), Color.Khaki, 0.64f);

        if (missing > 0)
        {
            Utils.DrawBorderString(spriteBatch, $"Missing {missing}", new Vector2(bounds.Right - 88f, bounds.Y + 7f), Color.Orange, 0.62f);
        }
    }

    private int CountMissingPets()
    {
        PetRegistry registry = PetRegistry.Instance;
        if (registry == null || petPlayer == null || preset == null)
        {
            return 0;
        }

        int missing = 0;
        missing += preset.NormalPets.Count(key => !petPlayer.IsUnlocked(key) || !registry.TryResolve(key, PetCategory.Normal, out _));
        missing += preset.LightPets.Count(key => !petPlayer.IsUnlocked(key) || !registry.TryResolve(key, PetCategory.Light, out _));
        return missing;
    }
}

internal sealed class BestiaryProgressBar : UIElement
{
    private int unlocked;
    private int total;
    private bool progressionMode;
    private bool visible;

    public void SetProgress(int unlockedCount, int totalCount, bool progressionEnabled)
    {
        unlocked = Math.Clamp(unlockedCount, 0, Math.Max(0, totalCount));
        total = Math.Max(0, totalCount);
        progressionMode = progressionEnabled;
        visible = total > 0;
    }

    public void SetHidden()
    {
        visible = false;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        if (!visible)
        {
            return;
        }

        CalculatedStyle dimensions = GetDimensions();
        Rectangle bounds = new((int)dimensions.X, (int)dimensions.Y, (int)dimensions.Width, (int)dimensions.Height);
        Rectangle inner = new(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4);
        int fillWidth = total > 0 ? (int)(inner.Width * (unlocked / (float)total)) : 0;

        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, new Color(91, 124, 199));
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, inner, new Color(72, 42, 76, 220));
        if (fillWidth > 0)
        {
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(inner.X, inner.Y, fillWidth, inner.Height), new Color(255, 206, 76, 235));
        }

        if (IsMouseHovering)
        {
            Main.LocalPlayer.mouseInterface = true;
            Main.hoverItemName = progressionMode
                ? $"Bestiary completion: {unlocked} / {total}\nProgression Mode is enabled"
                : $"Bestiary completion: {unlocked} / {total}";
        }
    }
}

internal sealed class DebugPanel : UIElement
{
    private bool visible;

    public DebugPanel(Action unlockAll, Action relockAll, Action clearActive, Action resyncNative, Action clearDyes, Action unlockAllDyes, Action relockAllDyes)
    {
        AddButton("Unlock Pets", 24f, 24f, unlockAll);
        AddButton("Relock Pets", 140f, 24f, relockAll);
        AddButton("Clear Active", 256f, 24f, clearActive);
        AddButton("Resync Native", 386f, 24f, resyncNative);
        AddButton("Clear Dyes", 516f, 24f, clearDyes);
        AddButton("Unlock Dyes", 24f, 66f, unlockAllDyes);
        AddButton("Relock Dyes", 140f, 66f, relockAllDyes);
    }

    public void SetVisible(bool value)
    {
        visible = value;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        CalculatedStyle dimensions = GetDimensions();
        Rectangle bounds = new((int)dimensions.X, (int)dimensions.Y, (int)dimensions.Width, (int)dimensions.Height);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, new Color(23, 33, 78, 190));
        string text = visible
            ? "Debug tools are enabled in config."
            : "Enable DebugMode in config to show debug tools.";
        Utils.DrawBorderString(spriteBatch, text, new Vector2(bounds.X + 24f, bounds.Y + 118f), Color.White, 0.76f);
    }

    private void AddButton(string label, float left, float top, Action action)
    {
        UITextPanel<string> button = new(label, 0.68f);
        button.Left.Set(left, 0f);
        button.Top.Set(top, 0f);
        button.Width.Set(110f, 0f);
        button.Height.Set(30f, 0f);
        button.OnLeftClick += (_, _) =>
        {
            action();
            SoundEngine.PlaySound(SoundID.MenuTick);
        };
        Append(button);
    }
}
