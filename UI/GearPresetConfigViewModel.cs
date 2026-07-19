using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Domain;
using CompanionGearUpgrades.Services;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace CompanionGearUpgrades.UI
{
    public enum GearPresetCategory
    {
        Weapons,
        Armors,
        Horse
    }

    public enum GearItemSortOrder
    {
        ValueAscending,
        ValueDescending
    }

    /// <summary>
    /// Shared Gauntlet state for preset configuration. The working snapshot
    /// is created when a role/tier is selected and committed only by Save.
    /// </summary>
    public sealed class GearPresetConfigViewModel : ViewModel
    {
        private enum Page
        {
            Roles,
            Tiers,
            Categories,
            Slots,
            Items
        }

        private readonly CompanionGearUpgradeService _service;
        private readonly GearPresetOverrides _overrides;
        private readonly Action<bool> _windowStateChanged;

        private readonly MBBindingList<GearRoleOptionViewModel> _roles;
        private readonly MBBindingList<GearTierOptionViewModel> _tiers;
        private readonly MBBindingList<GearCategoryOptionViewModel> _categories;
        private readonly MBBindingList<GearSlotOptionViewModel> _slots;
        private readonly MBBindingList<GearItemOptionViewModel> _items;
        private readonly List<GearItemOptionViewModel> _allItems;
        private readonly MBBindingList<GearItemFilterOptionViewModel> _filters;
        private readonly MBBindingList<GearItemSortOptionViewModel> _sortOptions;
        private readonly MBBindingList<GearItemComparisonViewModel> _comparisonStats;
        private ItemPreviewVM _itemPreview;
        private readonly GearItemTooltipViewModel _inspectionTooltip;
        private readonly GearItemTooltipViewModel _configuredTooltip;
        private ItemObject _inspectedItem;

        private GearRole _role;
        private int _tier;
        private GearPresetCategory _category;
        private EquipmentIndex _slot;
        private GearPresetSnapshot _working;
        private GearPresetSnapshot _savedSnapshot;
        private GearRole _workingRole;
        private int _workingTier;
        private bool _hasWorkingPreset;
        private string _selectedCandidateId;
        private string _selectedItemTypeFilter;
        private string _itemSearchText;
        private GearItemSortOrder _itemSortOrder;
        private string _requestedPreviewItemId;
        private string _openedPreviewItemId;
        private string _readyPreviewItemId;
        private int _previewOpenDelayTicks;
        private int _previewOpenAttempt;
        private int _previewTextureSettleTicks;
        private int _previewTextureWaitTicks;
        private string _previewStateText;
        private bool _isReleasingPreview;
        private GearItemOptionViewModel _hoveredCandidate;
        private Page _page;
        private bool _isWindowOpen;
        private bool _isHostScreenVisible;
        private string _statusText;

        // Open only after the direct ItemTableauWidget has materialized its
        // native texture provider in the visible Gauntlet context.
        private const int PreviewOpenDelayTicks = 1;
        private const int PreviewTextureSettleTicks = 2;
        private const int PreviewTextureTimeoutTicks = 30;
        private const int MaxPreviewOpenAttempts = 3;

        public GearPresetConfigViewModel(
            CompanionGearUpgradeService service,
            GearPresetOverrides overrides,
            Action<bool> windowStateChanged = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _overrides = overrides ?? throw new ArgumentNullException(nameof(overrides));
            _windowStateChanged = windowStateChanged;

            _roles = new MBBindingList<GearRoleOptionViewModel>();
            _tiers = new MBBindingList<GearTierOptionViewModel>();
            _categories = new MBBindingList<GearCategoryOptionViewModel>();
            _slots = new MBBindingList<GearSlotOptionViewModel>();
            _items = new MBBindingList<GearItemOptionViewModel>();
            _allItems = new List<GearItemOptionViewModel>();
            _filters = new MBBindingList<GearItemFilterOptionViewModel>();
            _sortOptions = new MBBindingList<GearItemSortOptionViewModel>();
            _comparisonStats = new MBBindingList<GearItemComparisonViewModel>();
            _itemPreview = new ItemPreviewVM(OnItemPreviewClosed);
            _inspectionTooltip = new GearItemTooltipViewModel();
            _configuredTooltip = new GearItemTooltipViewModel();
            _itemSortOrder = GearItemSortOrder.ValueAscending;
            _previewStateText = "Preview will load when an item is selected.";
            _statusText = "Select a role and tier to edit a preset.";
            _page = Page.Roles;

            _roles.Add(new GearRoleOptionViewModel(GearRole.Infantry, "Infantry", SelectRole));
            _roles.Add(new GearRoleOptionViewModel(GearRole.Archer, "Archer", SelectRole));
            _roles.Add(new GearRoleOptionViewModel(GearRole.Lancer, "Lancer", SelectRole));
            _sortOptions.Add(new GearItemSortOptionViewModel(GearItemSortOrder.ValueAscending, "Price: low to high", SelectSort));
            _sortOptions.Add(new GearItemSortOptionViewModel(GearItemSortOrder.ValueDescending, "Price: high to low", SelectSort));
            SetSelectedSortOption();
        }

        [DataSourceProperty]
        public MBBindingList<GearRoleOptionViewModel> RoleOptions => _roles;

        [DataSourceProperty]
        public MBBindingList<GearTierOptionViewModel> TierOptions => _tiers;

        [DataSourceProperty]
        public MBBindingList<GearCategoryOptionViewModel> CategoryOptions => _categories;

        [DataSourceProperty]
        public MBBindingList<GearSlotOptionViewModel> SlotOptions => _slots;

        [DataSourceProperty]
        public MBBindingList<GearItemOptionViewModel> ItemOptions => _items;

        [DataSourceProperty]
        public MBBindingList<GearItemFilterOptionViewModel> FilterOptions => _filters;

        [DataSourceProperty]
        public MBBindingList<GearItemSortOptionViewModel> SortOptions => _sortOptions;

        [DataSourceProperty]
        public ItemPreviewVM ItemPreview => _itemPreview;

        [DataSourceProperty]
        public ItemCollectionElementViewModel PreviewTableau => _itemPreview?.ItemTableau;

        [DataSourceProperty]
        public GearItemTooltipViewModel InspectionTooltip => _inspectionTooltip;

        [DataSourceProperty]
        public GearItemTooltipViewModel ConfiguredTooltip => _configuredTooltip;

        [DataSourceProperty]
        public MBBindingList<GearItemComparisonViewModel> ComparisonStats => _comparisonStats;

        [DataSourceProperty]
        public bool HasPreviewItem => !string.IsNullOrEmpty(_readyPreviewItemId) &&
            string.Equals(_readyPreviewItemId, _requestedPreviewItemId, StringComparison.Ordinal);

        [DataSourceProperty]
        public string PreviewStateText => _previewStateText;

        [DataSourceProperty]
        public bool HasInspectionItem => _inspectedItem != null;

        [DataSourceProperty]
        public bool HasComparison => _comparisonStats.Count > 0;

        [DataSourceProperty]
        public bool HasSingleInspection => HasInspectionItem && !HasComparison;

        [DataSourceProperty]
        public bool HasHoveredComparison => _hoveredCandidate != null && HasComparison;

        [DataSourceProperty]
        public string ItemSearchText
        {
            get { return _itemSearchText; }
            set
            {
                string searchText = value ?? string.Empty;
                if (string.Equals(_itemSearchText, searchText, StringComparison.Ordinal))
                    return;

                _itemSearchText = searchText;
                OnPropertyChanged(nameof(ItemSearchText));
                ClearHoveredCandidate();
                RebuildVisibleItems();
                RefreshItemInspection();
            }
        }

        [DataSourceProperty]
        public string SearchPlaceholderText => "Search by name or StringId";

        [DataSourceProperty]
        public string ItemCountText => $"{_items.Count} / {_allItems.Count} items";

        [DataSourceProperty]
        public bool HasVisibleItems => _items.Count > 0;

        [DataSourceProperty]
        public bool IsWindowOpen
        {
            get { return _isWindowOpen; }
            private set
            {
                if (_isWindowOpen == value)
                    return;

                _isWindowOpen = value;
                OnPropertyChanged(nameof(IsWindowOpen));
                OnPropertyChanged(nameof(IsRoleSelectionVisible));
                OnPropertyChanged(nameof(IsTierSelectionVisible));
                OnPropertyChanged(nameof(IsCategorySelectionVisible));
                OnPropertyChanged(nameof(IsSlotSelectionVisible));
                OnPropertyChanged(nameof(IsItemSelectionVisible));
                OnPropertyChanged(nameof(IsTierEditorMainVisible));
                _windowStateChanged?.Invoke(value);
            }
        }

        [DataSourceProperty]
        public bool IsRoleSelectionVisible => IsWindowOpen && _page == Page.Roles;

        [DataSourceProperty]
        public bool IsTierSelectionVisible => IsWindowOpen && _page == Page.Tiers;

        [DataSourceProperty]
        public bool IsCategorySelectionVisible => IsWindowOpen && _page == Page.Categories;

        [DataSourceProperty]
        public bool IsSlotSelectionVisible => IsWindowOpen && _page == Page.Slots;

        [DataSourceProperty]
        public bool IsItemSelectionVisible => IsWindowOpen && _page == Page.Items;

        [DataSourceProperty]
        public bool IsTierEditorMainVisible => IsWindowOpen && _page == Page.Categories;

        [DataSourceProperty]
        public bool HasUnsavedChanges => !SnapshotsEqual(_working, _savedSnapshot);

        public void SetHostScreenVisible(bool visible)
        {
            if (_isHostScreenVisible == visible)
                return;

            _isHostScreenVisible = visible;
            if (!visible && IsWindowOpen)
            {
                bool discardedChanges = HasUnsavedChanges;
                CloseWithoutSaving();
                if (discardedChanges)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[CGU] The configuration was closed and unsaved changes were discarded."));
                }
            }
        }

        [DataSourceProperty]
        public string WindowTitle => "Companion Gear Upgrade - Preset Configuration";

        [DataSourceProperty]
        public string Breadcrumb
        {
            get
            {
                if (_page == Page.Roles)
                    return "Choose a role";
                if (_page == Page.Tiers)
                    return $"{_role} > Choose a tier";
                if (_page == Page.Categories)
                    return $"{_role} > Tier {_tier} > Choose a category";
                if (_page == Page.Slots)
                    return $"{_role} > Tier {_tier} > {GetCategoryName(_category)} > Choose a slot";
                return $"{_role} > Tier {_tier} > {GetCategoryName(_category)} > {GetSlotName(_slot)}";
            }
        }

        [DataSourceProperty]
        public string StatusText
        {
            get { return _statusText; }
            private set
            {
                if (string.Equals(_statusText, value, StringComparison.Ordinal))
                    return;

                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        [DataSourceProperty]
        public string CurrentItemName
        {
            get
            {
                string id = GetWorkingSlotId(_slot);
                if (string.IsNullOrEmpty(id))
                    return "(empty slot)";

                ItemObject item = FindItem(id);
                return item != null ? item.Name.ToString() : "Missing item";
            }
        }

        [DataSourceProperty]
        public string CurrentItemStringId
        {
            get
            {
                string id = GetWorkingSlotId(_slot);
                return string.IsNullOrEmpty(id) ? "(empty)" : id;
            }
        }

        [DataSourceProperty]
        public string CurrentItemStringIdLabel => $"StringId: {CurrentItemStringId}";

        [DataSourceProperty]
        public string CurrentTierCostText => _working == null ? "" : $"{_working.Cost} gold";

        [DataSourceProperty]
        public string CurrentItemDisplayName => TruncatePreviewName(CurrentItemName);

        [DataSourceProperty]
        public HintViewModel CurrentItemNameHint => CreateNameHint(CurrentItemName);

        [DataSourceProperty]
        public string SelectedCandidateName
        {
            get
            {
                GearItemOptionViewModel item = FindCandidate(_selectedCandidateId);
                return item != null ? item.ItemName : "No item selected";
            }
        }

        [DataSourceProperty]
        public string SelectedCandidateStringId => string.IsNullOrEmpty(_selectedCandidateId) ? "" : _selectedCandidateId;

        [DataSourceProperty]
        public string SelectedCandidateDisplayName => TruncatePreviewName(SelectedCandidateName);

        [DataSourceProperty]
        public HintViewModel SelectedCandidateNameHint => CreateNameHint(SelectedCandidateName);

        [DataSourceProperty]
        public HintViewModel SelectItemHint => CreateNameHint("The selected item is applied to the temporary tier snapshot.");

        [DataSourceProperty]
        public HintViewModel RemoveItemHint => CreateNameHint("The configured item is removed from the temporary tier snapshot.");

        [DataSourceProperty]
        public HintViewModel ResetTierHint => CreateNameHint("Restore this tier's default equipment and price in the temporary snapshot.");

        [DataSourceProperty]
        public HintViewModel SetTierPriceHint => CreateNameHint("Set a custom gold price for this tier in the temporary snapshot.");

        [DataSourceProperty]
        public HintViewModel CalculateTierPriceHint => CreateNameHint("Calculate a price from the equipment currently configured for this tier.");

        [DataSourceProperty]
        public HintViewModel SaveHint => CreateNameHint("Save all temporary changes to the campaign without closing the configuration.");

        [DataSourceProperty]
        public HintViewModel ExitHint => CreateNameHint("Close the configuration. You will be warned before unsaved changes are discarded.");

        public void ExecuteOpenConfiguration()
        {
            PreparePreviewSession();
            _working = null;
            _savedSnapshot = null;
            _hasWorkingPreset = false;
            _selectedCandidateId = null;
            ClearItemInspection();
            foreach (GearRoleOptionViewModel roleOption in _roles)
                roleOption.SetSelected(false);
            _page = Page.Roles;
            StatusText = "Select a role and tier to edit a preset.";
            NotifyUnsavedChangesChanged();
            NotifyPageChanged();
            IsWindowOpen = true;
        }

        public void ExecuteBack()
        {
            switch (_page)
            {
                case Page.Tiers:
                    SetPage(Page.Roles);
                    break;
                case Page.Categories:
                    SetPage(Page.Tiers);
                    break;
                case Page.Slots:
                    SetPage(Page.Categories);
                    break;
                case Page.Items:
                    SetPage(Page.Slots);
                    break;
                default:
                    ExecuteExit();
                    break;
            }
        }

        public void ExecuteSave()
        {
            if (_working == null)
            {
                StatusText = "Select a role and tier before saving.";
                return;
            }

            GearRole role = _hasWorkingPreset ? _workingRole : _role;
            int tier = _hasWorkingPreset ? _workingTier : _tier;
            GearPreset defaultPreset = _service.GetDefaultPresetOrNull(role, tier);
            if (defaultPreset == null)
            {
                StatusText = "The selected preset is not available.";
                return;
            }

            _overrides.CommitSnapshot(role, tier, defaultPreset, _working);
            _savedSnapshot = _working.Clone();
            foreach (GearTierOptionViewModel tierOption in _tiers)
            {
                if (tierOption.Tier == tier)
                    tierOption.SetCost(_working.Cost);
            }

            NotifyUnsavedChangesChanged();
            StatusText = "Changes saved to the campaign overrides.";
        }

        public void ExecuteExit()
        {
            if (!HasUnsavedChanges)
            {
                CloseWithoutSaving();
                return;
            }

            StatusText = "Unsaved changes are still pending.";
            InformationManager.ShowInquiry(new InquiryData(
                "CGU - Unsaved changes",
                "You have unsaved changes. Exit and discard them?",
                true,
                true,
                "Exit without saving",
                "Keep editing",
                () =>
                {
                    CloseWithoutSaving();
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[CGU] Unsaved changes were discarded."));
                },
                () => StatusText = "Exit cancelled. Your unsaved changes are still available."
            ));
        }

        public void ExecuteSelectItem()
        {
            if (_working == null || string.IsNullOrEmpty(_selectedCandidateId))
            {
                StatusText = "Select an item in the left panel first.";
                return;
            }

            string selectedItemId = _selectedCandidateId;
            _working.Slots[_slot] = selectedItemId;
            SetSelectedCandidate(null);
            RefreshSlotLabels();
            NotifyCurrentItemChanged();
            RefreshItemInspection();
            NotifyUnsavedChangesChanged();
            StatusText = $"{GetSlotName(_slot)} changed in the temporary snapshot.";
        }

        public void ExecuteRemoveItem()
        {
            if (_working == null)
                return;

            // Null is intentional here. CommitSnapshot persists it as an
            // explicit empty marker instead of falling back to the default.
            _working.Slots[_slot] = null;
            SetSelectedCandidate(null);
            RefreshSlotLabels();
            NotifyCurrentItemChanged();
            RefreshItemInspection();
            NotifyUnsavedChangesChanged();
            StatusText = $"{GetSlotName(_slot)} was removed from the temporary snapshot.";
        }

        public void ExecuteResetTierToDefault()
        {
            GearPresetSnapshot defaultSnapshot = CreateDefaultTierSnapshot(_service, _role, _tier);
            if (defaultSnapshot == null)
            {
                StatusText = "The selected preset is not available.";
                return;
            }

            _working = defaultSnapshot;
            RefreshSlotLabels();
            NotifyCurrentItemChanged();
            OnPropertyChanged(nameof(CurrentTierCostText));
            NotifyUnsavedChangesChanged();
            StatusText = "The current tier was reset to its default items and price in the temporary snapshot.";
        }

        public void ExecuteSetTierPrice()
        {
            if (_working == null)
                return;

            InformationManager.ShowTextInquiry(new TextInquiryData(
                "CGU - Set price",
                "Enter the price in gold (number):",
                true,
                true,
                "OK",
                "Cancel",
                text =>
                {
                    GearPresetSnapshot updatedSnapshot;
                    if (!TrySetSnapshotPrice(_working, text, out updatedSnapshot))
                    {
                        StatusText = "Invalid price. Enter a non-negative whole number.";
                        return;
                    }

                    _working = updatedSnapshot;
                    OnPropertyChanged(nameof(CurrentTierCostText));
                    NotifyUnsavedChangesChanged();
                    StatusText = $"Temporary tier price changed to {_working.Cost} gold.";
                },
                () => StatusText = "Price unchanged."
            ));
        }

        public void ExecuteCalculateTierPriceFromEquipment()
        {
            if (_working == null)
                return;

            int calculatedPrice;
            string error;
            if (!TryCalculateTierPriceFromEquipment(_working, out calculatedPrice, out error))
            {
                StatusText = error;
                return;
            }

            InformationManager.ShowInquiry(new InquiryData(
                "CGU - Calculate price",
                $"Configured equipment is worth {calculatedPrice} gold. Use this as the tier price?",
                true,
                true,
                "Apply",
                "Cancel",
                () => ApplyCalculatedTierPrice(calculatedPrice),
                () => StatusText = "Calculated price was not applied."
            ));
        }

        private void ApplyCalculatedTierPrice(int calculatedPrice)
        {
            GearPresetSnapshot updatedSnapshot;
            if (!TrySetSnapshotPrice(_working, calculatedPrice.ToString(), out updatedSnapshot))
            {
                StatusText = "Calculated price is invalid.";
                return;
            }

            _working = updatedSnapshot;
            OnPropertyChanged(nameof(CurrentTierCostText));
            NotifyUnsavedChangesChanged();
            StatusText = $"Temporary tier price calculated from equipment: {_working.Cost} gold.";
        }

        private static bool TryCalculateTierPriceFromEquipment(
            GearPresetSnapshot snapshot,
            out int calculatedPrice,
            out string error)
        {
            calculatedPrice = 0;
            error = null;

            if (snapshot == null || snapshot.Slots == null)
            {
                error = "The current tier is not available.";
                return false;
            }

            long total = 0;
            foreach (EquipmentIndex slot in GearPresetOverrides.EditableSlots)
            {
                string itemId;
                if (!snapshot.Slots.TryGetValue(slot, out itemId) || string.IsNullOrEmpty(itemId))
                    continue;

                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                if (item == null)
                {
                    error = $"Cannot calculate price: configured item '{itemId}' is unavailable.";
                    return false;
                }

                total += item.Value;
                if (total > int.MaxValue)
                {
                    error = "Calculated equipment value is too high.";
                    return false;
                }
            }

            calculatedPrice = (int)total;
            return true;
        }

        private static GearPresetSnapshot CreateDefaultTierSnapshot(
            CompanionGearUpgradeService service,
            GearRole role,
            int tier)
        {
            GearPreset defaultPreset = service != null ? service.GetDefaultPresetOrNull(role, tier) : null;
            return defaultPreset == null
                ? null
                : new GearPresetSnapshot(
                    defaultPreset.Cost,
                    new Dictionary<EquipmentIndex, string>(defaultPreset.Slots));
        }

        private static bool TrySetSnapshotPrice(
            GearPresetSnapshot snapshot,
            string text,
            out GearPresetSnapshot updatedSnapshot)
        {
            updatedSnapshot = snapshot;
            int value;
            if (snapshot == null || !int.TryParse(text, out value) || value < 0)
                return false;

            updatedSnapshot = new GearPresetSnapshot(value, snapshot.Slots);
            return true;
        }

        private void SelectRole(GearRoleOptionViewModel option)
        {
            _role = option.Role;
            foreach (GearRoleOptionViewModel roleOption in _roles)
                roleOption.SetSelected(roleOption.Role == _role);

            _tiers.Clear();
            for (int tier = 1; tier <= 3; tier++)
                _tiers.Add(new GearTierOptionViewModel(tier, _service.GetEffectiveCost(_role, tier), SelectTier));

            SetPage(Page.Tiers);
        }

        private void SelectTier(GearTierOptionViewModel option)
        {
            if (_working != null && _hasWorkingPreset &&
                _workingRole == _role && _workingTier == option.Tier)
            {
                _tier = option.Tier;
                foreach (GearTierOptionViewModel tierOption in _tiers)
                    tierOption.SetSelected(tierOption.Tier == _tier);

                SetPage(Page.Categories);
                return;
            }

            if (HasUnsavedChanges)
            {
                GearRole targetRole = _role;
                InformationManager.ShowInquiry(new InquiryData(
                    "CGU - Unsaved changes",
                    "Switch presets and discard the unsaved changes to the current preset?",
                    true,
                    true,
                    "Discard and switch",
                    "Keep editing",
                    () => LoadTier(targetRole, option),
                    () => StatusText = "Preset switch cancelled. Your unsaved changes are still available."
                ));
                return;
            }

            LoadTier(_role, option);
        }

        private void LoadTier(GearRole role, GearTierOptionViewModel option)
        {
            _role = role;
            GearPreset preset = _service.GetDefaultPresetOrNull(_role, option.Tier);
            if (preset == null)
            {
                StatusText = "The selected preset is not available.";
                return;
            }

            _tier = option.Tier;
            foreach (GearTierOptionViewModel tierOption in _tiers)
                tierOption.SetSelected(tierOption.Tier == _tier);

            _working = _service.BuildEffectiveSnapshot(_role, _tier, preset);
            _savedSnapshot = _working?.Clone();
            _workingRole = _role;
            _workingTier = _tier;
            _hasWorkingPreset = _working != null;
            _categories.Clear();
            _categories.Add(new GearCategoryOptionViewModel(GearPresetCategory.Weapons, "Weapons", SelectCategory));
            _categories.Add(new GearCategoryOptionViewModel(GearPresetCategory.Armors, "Armors", SelectCategory));
            _categories.Add(new GearCategoryOptionViewModel(GearPresetCategory.Horse, "Horse", SelectCategory));
            NotifyUnsavedChangesChanged();
            SetPage(Page.Categories);
        }

        private void SelectCategory(GearCategoryOptionViewModel option)
        {
            _category = option.Category;
            foreach (GearCategoryOptionViewModel categoryOption in _categories)
                categoryOption.SetSelected(categoryOption.Category == _category);

            _slots.Clear();

            foreach (EquipmentIndex slot in GetSlotsForCategory(_category))
                _slots.Add(new GearSlotOptionViewModel(slot, GetSlotLabel(slot), SelectSlot));

            SetPage(Page.Slots);
        }

        private void SelectSlot(GearSlotOptionViewModel option)
        {
            _slot = option.Slot;
            foreach (GearSlotOptionViewModel slotOption in _slots)
                slotOption.SetSelected(slotOption.Slot == _slot);

            _selectedCandidateId = null;
            ClearHoveredCandidate();
            _allItems.Clear();

            foreach (ItemObject item in _service.GetCompatibleItems(_slot))
                _allItems.Add(new GearItemOptionViewModel(item, HighlightCandidate, BeginCandidateInspection, EndCandidateInspection));

            BuildFilters();
            RebuildVisibleItems();
            NotifyCurrentItemChanged();
            NotifyCandidateChanged();
            SetPage(Page.Items);
            RefreshItemInspection();
        }

        private void HighlightCandidate(GearItemOptionViewModel option)
        {
            SetSelectedCandidate(option.ItemId);
            RefreshItemInspection();
        }

        private void SetSelectedCandidate(string itemId)
        {
            _selectedCandidateId = itemId;
            foreach (GearItemOptionViewModel item in _allItems)
                item.SetSelected(string.Equals(item.ItemId, _selectedCandidateId, StringComparison.Ordinal));

            NotifyCandidateChanged();
        }

        private void SelectFilter(GearItemFilterOptionViewModel option)
        {
            _selectedItemTypeFilter = option.ItemTypeName;
            ClearHoveredCandidate();

            foreach (GearItemFilterOptionViewModel filter in _filters)
                filter.SetSelected(string.Equals(filter.ItemTypeName, _selectedItemTypeFilter, StringComparison.Ordinal));

            RebuildVisibleItems();
            RefreshItemInspection();
        }

        private void SelectSort(GearItemSortOptionViewModel option)
        {
            _itemSortOrder = option.SortOrder;
            ClearHoveredCandidate();
            SetSelectedSortOption();
            RebuildVisibleItems();
            RefreshItemInspection();
        }

        private void SetSelectedSortOption()
        {
            foreach (GearItemSortOptionViewModel sortOption in _sortOptions)
                sortOption.SetSelected(sortOption.SortOrder == _itemSortOrder);
        }

        private void BuildFilters()
        {
            _filters.Clear();
            _selectedItemTypeFilter = null;
            ItemSearchText = string.Empty;
            _filters.Add(new GearItemFilterOptionViewModel(null, "All", SelectFilter));

            HashSet<string> availableTypes = new HashSet<string>(StringComparer.Ordinal);
            foreach (GearItemOptionViewModel item in _allItems)
                availableTypes.Add(item.ItemTypeName);

            foreach (string itemTypeName in GetOrderedItemTypeNames())
            {
                if (availableTypes.Remove(itemTypeName))
                    _filters.Add(new GearItemFilterOptionViewModel(itemTypeName, GetItemTypeDisplayName(itemTypeName), SelectFilter));
            }

            List<string> remainingTypes = new List<string>(availableTypes);
            remainingTypes.Sort(StringComparer.Ordinal);
            foreach (string itemTypeName in remainingTypes)
                _filters.Add(new GearItemFilterOptionViewModel(itemTypeName, GetItemTypeDisplayName(itemTypeName), SelectFilter));

            foreach (GearItemFilterOptionViewModel filter in _filters)
                filter.SetSelected(filter.ItemTypeName == null);
        }

        private void RebuildVisibleItems()
        {
            List<GearItemOptionViewModel> visibleItems = new List<GearItemOptionViewModel>();
            foreach (GearItemOptionViewModel item in _allItems)
            {
                if (!string.IsNullOrEmpty(_selectedItemTypeFilter) &&
                    !string.Equals(item.ItemTypeName, _selectedItemTypeFilter, StringComparison.Ordinal))
                    continue;

                if (!string.IsNullOrWhiteSpace(_itemSearchText) &&
                    item.ItemName.IndexOf(_itemSearchText, StringComparison.OrdinalIgnoreCase) < 0 &&
                    item.ItemId.IndexOf(_itemSearchText, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                visibleItems.Add(item);
            }

            visibleItems.Sort(CompareVisibleItems);
            _items.Clear();
            foreach (GearItemOptionViewModel item in visibleItems)
                _items.Add(item);

            OnPropertyChanged(nameof(ItemCountText));
            OnPropertyChanged(nameof(HasVisibleItems));
        }

        private int CompareVisibleItems(GearItemOptionViewModel left, GearItemOptionViewModel right)
        {
            int comparison = left.ItemValue.CompareTo(right.ItemValue);
            if (_itemSortOrder == GearItemSortOrder.ValueDescending)
                comparison = -comparison;

            if (comparison != 0)
                return comparison;

            comparison = string.Compare(left.ItemName, right.ItemName, StringComparison.OrdinalIgnoreCase);
            return comparison != 0
                ? comparison
                : string.Compare(left.ItemId, right.ItemId, StringComparison.Ordinal);
        }

        private void SetPage(Page page)
        {
            if (_page == Page.Items && page != Page.Items)
                ClearItemInspection();

            _page = page;
            if (page == Page.Items)
                ArmPreviewHostInitialization();

            NotifyPageChanged();
        }

        private void NotifyPageChanged()
        {
            OnPropertyChanged(nameof(IsRoleSelectionVisible));
            OnPropertyChanged(nameof(IsTierSelectionVisible));
            OnPropertyChanged(nameof(IsCategorySelectionVisible));
            OnPropertyChanged(nameof(IsSlotSelectionVisible));
            OnPropertyChanged(nameof(IsItemSelectionVisible));
            OnPropertyChanged(nameof(IsTierEditorMainVisible));
            OnPropertyChanged(nameof(Breadcrumb));
            OnPropertyChanged(nameof(CurrentTierCostText));
        }

        private void NotifyCurrentItemChanged()
        {
            OnPropertyChanged(nameof(CurrentItemName));
            OnPropertyChanged(nameof(CurrentItemStringId));
            OnPropertyChanged(nameof(CurrentItemStringIdLabel));
            OnPropertyChanged(nameof(CurrentItemDisplayName));
            OnPropertyChanged(nameof(CurrentItemNameHint));
        }

        private void NotifyCandidateChanged()
        {
            OnPropertyChanged(nameof(SelectedCandidateName));
            OnPropertyChanged(nameof(SelectedCandidateStringId));
            OnPropertyChanged(nameof(SelectedCandidateDisplayName));
            OnPropertyChanged(nameof(SelectedCandidateNameHint));
        }

        private void RefreshSlotLabels()
        {
            foreach (GearSlotOptionViewModel slot in _slots)
                slot.SetLabel(GetSlotLabel(slot.Slot));
        }

        private string GetSlotLabel(EquipmentIndex slot)
        {
            string id = GetWorkingSlotId(slot);
            ItemObject item = string.IsNullOrEmpty(id) ? null : FindItem(id);
            string itemName = item != null ? item.Name.ToString() : "(empty)";
            return $"{GetSlotName(slot)}: {itemName}";
        }

        private string GetWorkingSlotId(EquipmentIndex slot)
        {
            if (_working == null)
                return null;

            string id;
            return _working.Slots.TryGetValue(slot, out id) ? id : null;
        }

        private GearItemOptionViewModel FindCandidate(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (GearItemOptionViewModel item in _allItems)
            {
                if (string.Equals(item.ItemId, id, StringComparison.Ordinal))
                    return item;
            }

            return null;
        }

        private static ItemObject FindItem(string id)
        {
            return string.IsNullOrEmpty(id)
                ? null
                : MBObjectManager.Instance.GetObject<ItemObject>(id);
        }

        private static bool SnapshotsEqual(GearPresetSnapshot left, GearPresetSnapshot right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Cost != right.Cost)
                return false;

            foreach (EquipmentIndex slot in GearPresetOverrides.EditableSlots)
            {
                string leftId;
                string rightId;
                bool hasLeft = left.Slots.TryGetValue(slot, out leftId);
                bool hasRight = right.Slots.TryGetValue(slot, out rightId);
                if (hasLeft != hasRight || !string.Equals(leftId, rightId, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private void NotifyUnsavedChangesChanged()
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void CloseWithoutSaving()
        {
            _working = null;
            _savedSnapshot = null;
            _hasWorkingPreset = false;
            _selectedCandidateId = null;
            ClearItemInspection();
            NotifyUnsavedChangesChanged();
            IsWindowOpen = false;
            ReleasePreviewSession();
        }

        /// <summary>
        /// The preview follows the hovered row first, then the selected row,
        /// and finally the item already configured in the temporary preset.
        /// The native ItemPreviewVM is scoped to one lazily-created movie.
        /// </summary>
        private void BeginCandidateInspection(GearItemOptionViewModel option)
        {
            _hoveredCandidate = option;
            RefreshItemInspection();
        }

        private void EndCandidateInspection(GearItemOptionViewModel option)
        {
            if (!ReferenceEquals(_hoveredCandidate, option))
                return;

            _hoveredCandidate = null;
            RefreshItemInspection();
        }

        private void ClearHoveredCandidate()
        {
            if (_hoveredCandidate != null)
                _hoveredCandidate.SetHovered(false);

            _hoveredCandidate = null;
        }

        private void RefreshItemInspection()
        {
            ItemObject configuredItem = FindItem(GetWorkingSlotId(_slot));
            GearItemOptionViewModel selectedCandidate = FindCandidate(_selectedCandidateId);
            ItemObject selectedItem = selectedCandidate != null ? selectedCandidate.Item : null;
            _inspectedItem = GetInspectedItem(configuredItem, selectedItem);
            bool hasComparison = _hoveredCandidate != null &&
                configuredItem != null &&
                !string.Equals(_hoveredCandidate.ItemId, configuredItem.StringId, StringComparison.Ordinal) &&
                (selectedItem == null ||
                    !string.Equals(_hoveredCandidate.ItemId, selectedItem.StringId, StringComparison.Ordinal));

            _inspectionTooltip.SetItem(_inspectedItem);
            _configuredTooltip.SetItem(hasComparison ? configuredItem : null);
            RebuildComparison(
                hasComparison ? _inspectedItem : null,
                hasComparison ? configuredItem : null);
            SetPreviewItem(_inspectedItem);

            OnPropertyChanged(nameof(HasInspectionItem));
            OnPropertyChanged(nameof(HasComparison));
            OnPropertyChanged(nameof(HasSingleInspection));
            OnPropertyChanged(nameof(HasHoveredComparison));
        }

        private ItemObject GetInspectedItem(ItemObject configuredItem, ItemObject selectedItem)
        {
            if (_hoveredCandidate != null)
                return _hoveredCandidate.Item;

            return selectedItem ?? configuredItem;
        }

        private void RebuildComparison(ItemObject inspectedItem, ItemObject configuredItem)
        {
            _comparisonStats.Clear();

            if (inspectedItem == null || configuredItem == null ||
                string.Equals(inspectedItem.StringId, configuredItem.StringId, StringComparison.Ordinal))
                return;

            Dictionary<string, GearItemStatValue> configuredStats = new Dictionary<string, GearItemStatValue>(StringComparer.Ordinal);
            foreach (GearItemStatValue stat in GearItemTooltipViewModel.GetStats(configuredItem))
                configuredStats[stat.Label] = stat;

            HashSet<string> addedLabels = new HashSet<string>(StringComparer.Ordinal);
            foreach (GearItemStatValue inspectedStat in GearItemTooltipViewModel.GetStats(inspectedItem))
            {
                GearItemStatValue configuredStat;
                configuredStats.TryGetValue(inspectedStat.Label, out configuredStat);
                _comparisonStats.Add(new GearItemComparisonViewModel(inspectedStat, configuredStat));
                addedLabels.Add(inspectedStat.Label);
            }

            foreach (GearItemStatValue configuredStat in GearItemTooltipViewModel.GetStats(configuredItem))
            {
                if (!addedLabels.Contains(configuredStat.Label))
                    _comparisonStats.Add(new GearItemComparisonViewModel(null, configuredStat));
            }
        }

        /// <summary>
        /// Called by the owning Gauntlet layer. ItemPreviewVM.Open is allowed
        /// only after the visible ItemTableauWidget has a native texture
        /// provider; this is the actual readiness boundary for the 3D host.
        /// </summary>
        public void OnGauntletTick(bool isPreviewHostReady, bool isPreviewTextureReady)
        {
            if (!IsWindowOpen || _page != Page.Items || _itemPreview == null ||
                string.IsNullOrEmpty(_requestedPreviewItemId))
                return;

            if (!isPreviewHostReady)
            {
                SetPreviewState("Preparing the 3D preview context...");
                return;
            }

            if (string.Equals(_openedPreviewItemId, _requestedPreviewItemId, StringComparison.Ordinal))
            {
                if (string.Equals(_readyPreviewItemId, _requestedPreviewItemId, StringComparison.Ordinal))
                    return;

                if (_previewTextureSettleTicks > 0)
                {
                    _previewTextureSettleTicks--;
                    return;
                }

                if (isPreviewTextureReady)
                {
                    _readyPreviewItemId = _requestedPreviewItemId;
                    SetPreviewState("3D preview ready.");
                    NotifyPreviewChanged();
                    return;
                }

                _previewTextureWaitTicks++;
                if (_previewTextureWaitTicks >= PreviewTextureTimeoutTicks)
                    SchedulePreviewRetryOrReportFailure();
                return;
            }

            if (_previewOpenDelayTicks > 0)
            {
                _previewOpenDelayTicks--;
                return;
            }

            if (!string.Equals(_openedPreviewItemId, _requestedPreviewItemId, StringComparison.Ordinal) &&
                _previewOpenAttempt < MaxPreviewOpenAttempts)
                OpenRequestedPreview();
        }

        private void SetPreviewItem(ItemObject item)
        {
            string itemId = item != null ? item.StringId : null;
            if (string.Equals(_requestedPreviewItemId, itemId, StringComparison.Ordinal) &&
                (!string.IsNullOrEmpty(_openedPreviewItemId) || _previewOpenDelayTicks > 0))
                return;

            _requestedPreviewItemId = itemId;
            _openedPreviewItemId = null;
            _readyPreviewItemId = null;
            _previewOpenAttempt = 0;
            _previewTextureSettleTicks = 0;
            _previewTextureWaitTicks = 0;

            if (item == null)
            {
                _previewOpenDelayTicks = 0;
                CloseAndClearNativePreview();

                SetPreviewState("Hover or select an item to preview it.");
                NotifyPreviewChanged();
                return;
            }

            _previewOpenDelayTicks = PreviewOpenDelayTicks;
            SetPreviewState("Loading 3D preview...");
            NotifyPreviewChanged();
        }

        private void ArmPreviewHostInitialization()
        {
            if (_itemPreview == null || string.IsNullOrEmpty(_requestedPreviewItemId))
                return;

            _openedPreviewItemId = null;
            _readyPreviewItemId = null;
            _previewOpenAttempt = 0;
            _previewTextureSettleTicks = 0;
            _previewTextureWaitTicks = 0;
            _previewOpenDelayTicks = PreviewOpenDelayTicks;
            SetPreviewState("Loading 3D preview...");
            NotifyPreviewChanged();
        }

        private void OpenRequestedPreview()
        {
            ItemObject item = FindItem(_requestedPreviewItemId);
            if (item == null)
            {
                SetPreviewState("The selected item is no longer available for preview.");
                NotifyPreviewChanged();
                return;
            }

            try
            {
                _previewOpenAttempt++;
                ClearNativePreviewTableau();
                _itemPreview.Open(new EquipmentElement(item));
                _openedPreviewItemId = _requestedPreviewItemId;
                _readyPreviewItemId = null;
                _previewTextureSettleTicks = PreviewTextureSettleTicks;
                _previewTextureWaitTicks = 0;
                SetPreviewState("Rendering 3D preview...");
                NotifyPreviewChanged();
            }
            catch (Exception)
            {
                SchedulePreviewRetryOrReportFailure();
            }
        }

        private void SchedulePreviewRetryOrReportFailure()
        {
            _openedPreviewItemId = null;
            _readyPreviewItemId = null;
            _previewTextureSettleTicks = 0;
            _previewTextureWaitTicks = 0;
            ClearNativePreviewTableau();

            if (_previewOpenAttempt < MaxPreviewOpenAttempts)
            {
                _previewOpenDelayTicks = PreviewOpenDelayTicks;
                SetPreviewState("Retrying 3D preview...");
                NotifyPreviewChanged();
                return;
            }

            SetPreviewState("3D preview is temporarily unavailable. Hover the item again to retry.");
            NotifyPreviewChanged();
        }

        private void PreparePreviewSession()
        {
            ResetPreviewTracking();
            CloseAndClearNativePreview();

            SetPreviewState("Preview will load when an item is selected.");
            NotifyPreviewChanged();
        }

        private void ReleasePreviewSession()
        {
            ResetPreviewTracking();
            CloseAndClearNativePreview();

            SetPreviewState("Preview is closed.");
            NotifyPreviewChanged();
        }

        private void ResetPreviewTracking()
        {
            _requestedPreviewItemId = null;
            _openedPreviewItemId = null;
            _readyPreviewItemId = null;
            _previewOpenDelayTicks = 0;
            _previewOpenAttempt = 0;
            _previewTextureSettleTicks = 0;
            _previewTextureWaitTicks = 0;
        }

        private void CloseAndClearNativePreview()
        {
            if (_itemPreview == null)
                return;

            _isReleasingPreview = true;
            try
            {
                if (_itemPreview.IsSelected)
                    _itemPreview.Close();
                ClearNativePreviewTableau();
            }
            finally
            {
                _isReleasingPreview = false;
            }
        }

        private void ClearNativePreviewTableau()
        {
            if (_itemPreview?.ItemTableau != null)
                _itemPreview.ItemTableau.StringId = string.Empty;
        }

        private void SetPreviewState(string state)
        {
            if (string.Equals(_previewStateText, state, StringComparison.Ordinal))
                return;

            _previewStateText = state;
            OnPropertyChanged(nameof(PreviewStateText));
        }

        private void NotifyPreviewChanged()
        {
            OnPropertyChanged(nameof(HasPreviewItem));
        }

        private void ClearItemInspection()
        {
            ClearHoveredCandidate();
            _inspectedItem = null;
            _inspectionTooltip.SetItem(null);
            _configuredTooltip.SetItem(null);
            _comparisonStats.Clear();
            SetPreviewItem(null);

            OnPropertyChanged(nameof(HasInspectionItem));
            OnPropertyChanged(nameof(HasComparison));
            OnPropertyChanged(nameof(HasSingleInspection));
            OnPropertyChanged(nameof(HasHoveredComparison));
        }

        private void OnItemPreviewClosed()
        {
            _openedPreviewItemId = null;
            _readyPreviewItemId = null;
            _previewTextureSettleTicks = 0;
            _previewTextureWaitTicks = 0;
            NotifyPreviewChanged();

            if (_isReleasingPreview || !IsWindowOpen || _page != Page.Items ||
                string.IsNullOrEmpty(_requestedPreviewItemId))
                return;

            _previewOpenAttempt = 0;
            _previewOpenDelayTicks = PreviewOpenDelayTicks;
            SetPreviewState("Reinitializing 3D preview...");
        }

        public override void OnFinalize()
        {
            ClearItemInspection();
            ReleasePreviewSession();

            if (_itemPreview != null)
            {
                _isReleasingPreview = true;
                try
                {
                    _itemPreview.OnFinalize();
                }
                finally
                {
                    _isReleasingPreview = false;
                    _itemPreview = null;
                }
            }

            base.OnFinalize();
        }

        private static IEnumerable<string> GetOrderedItemTypeNames()
        {
            return new[]
            {
                "OneHandedWeapon",
                "TwoHandedWeapon",
                "Polearm",
                "Bow",
                "Crossbow",
                "Thrown",
                "Shield",
                "Arrows",
                "Bolts",
                "Banner",
                "HeadArmor",
                "BodyArmor",
                "Cape",
                "HandArmor",
                "LegArmor",
                "Horse",
                "HorseHarness"
            };
        }

        private static string GetItemTypeDisplayName(string itemTypeName)
        {
            switch (itemTypeName)
            {
                case "OneHandedWeapon": return "One-handed";
                case "TwoHandedWeapon": return "Two-handed";
                case "Polearm": return "Polearms";
                case "Bow": return "Bows";
                case "Crossbow": return "Crossbows";
                case "Thrown": return "Thrown";
                case "Shield": return "Shields";
                case "Arrows": return "Arrows";
                case "Bolts": return "Bolts";
                case "HeadArmor": return "Head";
                case "BodyArmor": return "Body";
                case "Cape": return "Capes";
                case "HandArmor": return "Gloves";
                case "LegArmor": return "Legs";
                case "Horse": return "Horses";
                case "HorseHarness": return "Harnesses";
                default: return itemTypeName;
            }
        }

        private static string TruncatePreviewName(string name)
        {
            const int maximumLength = 28;
            if (string.IsNullOrEmpty(name) || name.Length <= maximumLength)
                return name;

            return name.Substring(0, maximumLength - 3) + "...";
        }

        private static HintViewModel CreateNameHint(string name)
        {
            return new HintViewModel(new TextObject(name ?? string.Empty), null);
        }

        private static IEnumerable<EquipmentIndex> GetSlotsForCategory(GearPresetCategory category)
        {
            switch (category)
            {
                case GearPresetCategory.Weapons:
                    return new[]
                    {
                        EquipmentIndex.Weapon0,
                        EquipmentIndex.Weapon1,
                        EquipmentIndex.Weapon2,
                        EquipmentIndex.Weapon3
                    };
                case GearPresetCategory.Armors:
                    return new[]
                    {
                        EquipmentIndex.Head,
                        EquipmentIndex.Body,
                        EquipmentIndex.Cape,
                        EquipmentIndex.Gloves,
                        EquipmentIndex.Leg
                    };
                default:
                    return new[] { EquipmentIndex.Horse, EquipmentIndex.HorseHarness };
            }
        }

        private static string GetCategoryName(GearPresetCategory category)
        {
            return category.ToString();
        }

        private static string GetSlotName(EquipmentIndex slot)
        {
            switch (slot)
            {
                case EquipmentIndex.Weapon0: return "Weapon0";
                case EquipmentIndex.Weapon1: return "Weapon1";
                case EquipmentIndex.Weapon2: return "Weapon2";
                case EquipmentIndex.Weapon3: return "Weapon3";
                case EquipmentIndex.Head: return "Head";
                case EquipmentIndex.Body: return "Body";
                case EquipmentIndex.Cape: return "Cape";
                case EquipmentIndex.Gloves: return "Gloves";
                case EquipmentIndex.Leg: return "Leg";
                case EquipmentIndex.Horse: return "Horse";
                case EquipmentIndex.HorseHarness: return "HorseHarness";
                default: return slot.ToString();
            }
        }
    }

    public sealed class GearRoleOptionViewModel : ViewModel
    {
        private readonly Action<GearRoleOptionViewModel> _onSelected;
        private bool _isSelected;

        public GearRoleOptionViewModel(GearRole role, string name, Action<GearRoleOptionViewModel> onSelected)
        {
            Role = role;
            Name = name;
            _onSelected = onSelected;
        }

        public GearRole Role { get; private set; }

        [DataSourceProperty]
        public string Name { get; private set; }

        [DataSourceProperty]
        public bool IsSelected
        {
            get { return _isSelected; }
            private set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public void ExecuteSelect()
        {
            _onSelected?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
        }
    }

    public sealed class GearTierOptionViewModel : ViewModel
    {
        private readonly Action<GearTierOptionViewModel> _onSelected;
        private bool _isSelected;

        public GearTierOptionViewModel(int tier, int cost, Action<GearTierOptionViewModel> onSelected)
        {
            Tier = tier;
            Cost = cost;
            _onSelected = onSelected;
        }

        public int Tier { get; private set; }

        [DataSourceProperty]
        public string Name => $"Tier {Tier} ({Cost} gold)";

        [DataSourceProperty]
        public string TierName => $"Tier {Tier}";

        [DataSourceProperty]
        public string TierCostText => $"{Cost} gold";

        [DataSourceProperty]
        public bool IsSelected
        {
            get { return _isSelected; }
            private set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        private int Cost { get; set; }

        public void ExecuteSelect()
        {
            _onSelected?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
        }

        public void SetCost(int cost)
        {
            if (Cost == cost)
                return;

            Cost = cost;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(TierCostText));
        }
    }

    public sealed class GearCategoryOptionViewModel : ViewModel
    {
        private readonly Action<GearCategoryOptionViewModel> _onSelected;
        private bool _isSelected;

        public GearCategoryOptionViewModel(GearPresetCategory category, string name, Action<GearCategoryOptionViewModel> onSelected)
        {
            Category = category;
            Name = name;
            IconBrush = GetIconBrush(category);
            _onSelected = onSelected;
        }

        public GearPresetCategory Category { get; private set; }

        [DataSourceProperty]
        public string Name { get; private set; }

        [DataSourceProperty]
        public string IconBrush { get; private set; }

        [DataSourceProperty]
        public bool IsSelected
        {
            get { return _isSelected; }
            private set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public void ExecuteSelect()
        {
            _onSelected?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
        }

        private static string GetIconBrush(GearPresetCategory category)
        {
            switch (category)
            {
                case GearPresetCategory.Weapons:
                    return "InventoryFilterWeaponsButton";
                case GearPresetCategory.Armors:
                    return "InventoryFilterArmorsButton";
                default:
                    return "InventoryFilterMountsButton";
            }
        }
    }

    public sealed class GearSlotOptionViewModel : ViewModel
    {
        private readonly Action<GearSlotOptionViewModel> _onSelected;
        private string _label;
        private bool _isSelected;

        public GearSlotOptionViewModel(EquipmentIndex slot, string label, Action<GearSlotOptionViewModel> onSelected)
        {
            Slot = slot;
            _label = label;
            _onSelected = onSelected;
        }

        public EquipmentIndex Slot { get; private set; }

        [DataSourceProperty]
        public string Name => _label;

        [DataSourceProperty]
        public bool IsSelected
        {
            get { return _isSelected; }
            private set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public void SetLabel(string label)
        {
            if (string.Equals(_label, label, StringComparison.Ordinal))
                return;

            _label = label;
            OnPropertyChanged(nameof(Name));
        }

        public void ExecuteSelect()
        {
            _onSelected?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
        }
    }

    public sealed class GearItemFilterOptionViewModel : ViewModel
    {
        private readonly Action<GearItemFilterOptionViewModel> _onSelected;
        private bool _isSelected;

        public GearItemFilterOptionViewModel(string itemTypeName, string name, Action<GearItemFilterOptionViewModel> onSelected)
        {
            ItemTypeName = itemTypeName;
            Name = name;
            _onSelected = onSelected;
        }

        public string ItemTypeName { get; private set; }

        [DataSourceProperty]
        public string Name { get; private set; }

        [DataSourceProperty]
        public bool IsSelected
        {
            get { return _isSelected; }
            private set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public void ExecuteSelect()
        {
            _onSelected?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
        }
    }

    public sealed class GearItemSortOptionViewModel : ViewModel
    {
        private readonly Action<GearItemSortOptionViewModel> _onSelected;
        private bool _isSelected;

        public GearItemSortOptionViewModel(GearItemSortOrder sortOrder, string name, Action<GearItemSortOptionViewModel> onSelected)
        {
            SortOrder = sortOrder;
            Name = name;
            _onSelected = onSelected;
        }

        public GearItemSortOrder SortOrder { get; private set; }

        [DataSourceProperty]
        public string Name { get; private set; }

        [DataSourceProperty]
        public bool IsSelected
        {
            get { return _isSelected; }
            private set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public void ExecuteSelect()
        {
            _onSelected?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
        }
    }

    public sealed class GearItemOptionViewModel : ViewModel
    {
        private readonly Action<GearItemOptionViewModel> _onSelected;
        private readonly Action<GearItemOptionViewModel> _onHoverBegin;
        private readonly Action<GearItemOptionViewModel> _onHoverEnd;
        private bool _isHovered;
        private bool _isSelected;

        public GearItemOptionViewModel(
            ItemObject item,
            Action<GearItemOptionViewModel> onSelected,
            Action<GearItemOptionViewModel> onHoverBegin,
            Action<GearItemOptionViewModel> onHoverEnd)
        {
            Item = item;
            ItemId = item.StringId;
            ItemName = item.Name.ToString();
            ItemTypeName = item.ItemType.ToString();
            ItemValue = item.Value;
            _onSelected = onSelected;
            _onHoverBegin = onHoverBegin;
            _onHoverEnd = onHoverEnd;
            ImageIdentifier = new ItemImageIdentifierVM(item, string.Empty);
        }

        public ItemObject Item { get; private set; }

        [DataSourceProperty]
        public string ItemTypeName { get; private set; }

        [DataSourceProperty]
        public int ItemValue { get; private set; }

        [DataSourceProperty]
        public ItemImageIdentifierVM ImageIdentifier { get; private set; }

        [DataSourceProperty]
        public string ItemId { get; private set; }

        [DataSourceProperty]
        public string ItemName { get; private set; }

        [DataSourceProperty]
        public string ItemValueText => ItemValue.ToString() + " gold";

        [DataSourceProperty]
        public string DisplayText => $"{ItemName}  [{ItemId}]";

        [DataSourceProperty]
        public bool IsHovered
        {
            get { return _isHovered; }
            private set
            {
                if (_isHovered == value)
                    return;

                _isHovered = value;
                OnPropertyChanged(nameof(IsHovered));
            }
        }

        [DataSourceProperty]
        public bool IsSelected
        {
            get { return _isSelected; }
            private set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public void ExecuteHighlight()
        {
            _onSelected?.Invoke(this);
        }

        public void ExecuteHoverBegin()
        {
            IsHovered = true;
            _onHoverBegin?.Invoke(this);
        }

        public void ExecuteHoverEnd()
        {
            IsHovered = false;
            _onHoverEnd?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
        }

        public void SetHovered(bool hovered)
        {
            IsHovered = hovered;
        }
    }

    /// <summary>
    /// Lightweight data backing the custom tooltip. It deliberately reuses
    /// ItemMenuTooltipPropertyVM so its rows follow native inventory tooltip
    /// conventions without creating an InventoryLogic or SPInventoryVM.
    /// </summary>
    public sealed class GearItemTooltipViewModel : ViewModel
    {
        private readonly MBBindingList<ItemMenuTooltipPropertyVM> _properties;
        private ItemImageIdentifierVM _imageIdentifier;
        private string _itemName;
        private string _itemStringId;
        private bool _hasItem;

        public GearItemTooltipViewModel()
        {
            _properties = new MBBindingList<ItemMenuTooltipPropertyVM>();
        }

        [DataSourceProperty]
        public MBBindingList<ItemMenuTooltipPropertyVM> Properties => _properties;

        [DataSourceProperty]
        public ItemImageIdentifierVM ImageIdentifier
        {
            get { return _imageIdentifier; }
            private set
            {
                if (ReferenceEquals(_imageIdentifier, value))
                    return;

                _imageIdentifier = value;
                OnPropertyChanged(nameof(ImageIdentifier));
            }
        }

        [DataSourceProperty]
        public string ItemName
        {
            get { return _itemName; }
            private set
            {
                if (string.Equals(_itemName, value, StringComparison.Ordinal))
                    return;

                _itemName = value;
                OnPropertyChanged(nameof(ItemName));
            }
        }

        [DataSourceProperty]
        public string ItemStringId
        {
            get { return _itemStringId; }
            private set
            {
                if (string.Equals(_itemStringId, value, StringComparison.Ordinal))
                    return;

                _itemStringId = value;
                OnPropertyChanged(nameof(ItemStringId));
            }
        }

        [DataSourceProperty]
        public bool HasItem
        {
            get { return _hasItem; }
            private set
            {
                if (_hasItem == value)
                    return;

                _hasItem = value;
                OnPropertyChanged(nameof(HasItem));
            }
        }

        public void SetItem(ItemObject item)
        {
            _properties.Clear();
            HasItem = item != null;
            ItemName = item != null ? item.Name.ToString() : string.Empty;
            ItemStringId = item != null ? item.StringId : string.Empty;
            ImageIdentifier = item != null ? new ItemImageIdentifierVM(item, string.Empty) : null;

            if (item == null)
                return;

            foreach (GearItemStatValue stat in GetStats(item))
            {
                _properties.Add(new ItemMenuTooltipPropertyVM(
                    stat.Label,
                    stat.Value,
                    25,
                    false,
                    null,
                    string.Empty,
                    false));
            }
        }

        public static IEnumerable<GearItemStatValue> GetStats(ItemObject item)
        {
            List<GearItemStatValue> stats = new List<GearItemStatValue>();
            if (item == null)
                return stats;

            stats.Add(new GearItemStatValue("Type", item.ItemType.ToString()));
            stats.Add(new GearItemStatValue("Tier", item.Tier.ToString(), Convert.ToInt32(item.Tier)));
            stats.Add(new GearItemStatValue("Value", item.Value.ToString(), item.Value));
            stats.Add(new GearItemStatValue("Weight", item.Weight.ToString("0.##"), item.Weight));

            if (item.ArmorComponent != null)
            {
                AddPositiveStat(stats, "Head armor", item.ArmorComponent.HeadArmor);
                AddPositiveStat(stats, "Body armor", item.ArmorComponent.BodyArmor);
                AddPositiveStat(stats, "Arm armor", item.ArmorComponent.ArmArmor);
                AddPositiveStat(stats, "Leg armor", item.ArmorComponent.LegArmor);
                AddPositiveStat(stats, "Speed bonus", item.ArmorComponent.SpeedBonus);
                AddPositiveStat(stats, "Maneuver bonus", item.ArmorComponent.ManeuverBonus);
                AddPositiveStat(stats, "Charge bonus", item.ArmorComponent.ChargeBonus);
            }

            if (item.HorseComponent != null)
            {
                AddPositiveStat(stats, "Hit points", item.HorseComponent.HitPoints);
                AddPositiveStat(stats, "Speed", item.HorseComponent.Speed);
                AddPositiveStat(stats, "Maneuver", item.HorseComponent.Maneuver);
                AddPositiveStat(stats, "Charge damage", item.HorseComponent.ChargeDamage);
            }

            if (item.PrimaryWeapon != null)
            {
                AddPositiveStat(stats, "Swing damage", item.PrimaryWeapon.SwingDamage);
                AddPositiveStat(stats, "Swing speed", item.PrimaryWeapon.SwingSpeed);
                AddPositiveStat(stats, "Thrust damage", item.PrimaryWeapon.ThrustDamage);
                AddPositiveStat(stats, "Thrust speed", item.PrimaryWeapon.ThrustSpeed);
                AddPositiveStat(stats, "Missile damage", item.PrimaryWeapon.MissileDamage);
                AddPositiveStat(stats, "Missile speed", item.PrimaryWeapon.MissileSpeed);
                AddPositiveStat(stats, "Accuracy", item.PrimaryWeapon.Accuracy);
                AddPositiveStat(stats, "Handling", item.PrimaryWeapon.Handling);
                AddPositiveStat(stats, "Weapon length", item.PrimaryWeapon.WeaponLength);
                AddPositiveStat(stats, "Ammo", item.PrimaryWeapon.MaxDataValue);
            }

            return stats;
        }

        private static void AddPositiveStat(List<GearItemStatValue> stats, string label, int value)
        {
            if (value > 0)
                stats.Add(new GearItemStatValue(label, value.ToString(), value));
        }
    }

    public sealed class GearItemComparisonViewModel : ViewModel
    {
        public GearItemComparisonViewModel(GearItemStatValue inspected, GearItemStatValue configured)
        {
            Label = inspected != null ? inspected.Label : configured.Label;
            InspectedValue = inspected != null ? inspected.Value : "-";
            ConfiguredValue = configured != null ? configured.Value : "-";

            if (inspected != null && configured != null && inspected.NumericValue.HasValue && configured.NumericValue.HasValue)
            {
                IsBetter = inspected.NumericValue.Value > configured.NumericValue.Value;
                IsWorse = inspected.NumericValue.Value < configured.NumericValue.Value;
            }
        }

        [DataSourceProperty]
        public string Label { get; private set; }

        [DataSourceProperty]
        public string InspectedValue { get; private set; }

        [DataSourceProperty]
        public string ConfiguredValue { get; private set; }

        [DataSourceProperty]
        public bool IsBetter { get; private set; }

        [DataSourceProperty]
        public bool IsWorse { get; private set; }
    }

    public sealed class GearItemStatValue
    {
        public GearItemStatValue(string label, string value, float? numericValue = null)
        {
            Label = label;
            Value = value;
            NumericValue = numericValue;
        }

        public string Label { get; private set; }
        public string Value { get; private set; }
        public float? NumericValue { get; private set; }
    }
}
