using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Domain;
using CompanionGearUpgrades.Services;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace CompanionGearUpgrades.UI
{
    /// <summary>
    /// Shared Gauntlet state for preset configuration. The working snapshot
    /// is created when a role/tier is selected and committed only by Save.
    /// </summary>
    public sealed partial class GearPresetConfigViewModel : ViewModel
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
        private readonly List<GearRoleDefinition> _customRoles;
        private List<GearRoleDefinition> _savedCustomRoles;
        private readonly List<PendingImportRole> _pendingImportRoles;
        private GearPresetTransferDocument _pendingImportDocument;
        private int _pendingImportConflictIndex;
        private bool _isImportInProgress;
        private readonly MBBindingList<GearTierOptionViewModel> _tiers;
        private readonly MBBindingList<GearCategoryOptionViewModel> _categories;
        private readonly MBBindingList<GearSlotOptionViewModel> _slots;
        private readonly MBBindingList<GearItemOptionViewModel> _items;
        private readonly List<GearItemOptionViewModel> _allItems;
        private readonly MBBindingList<GearItemFilterOptionViewModel> _filters;
        private readonly MBBindingList<GearItemSortOptionViewModel> _sortOptions;
        private ItemPreviewVM _itemPreview;
        private readonly GearItemTooltipViewModel _inspectionTooltip;
        private readonly GearItemTooltipViewModel _configuredTooltip;
        private ItemObject _inspectedItem;

        private string _role;
        private int _tier;
        private GearPresetCategory _category;
        private EquipmentIndex _slot;
        private GearPresetSnapshot _working;
        private GearPresetSnapshot _savedSnapshot;
        private string _workingRole;
        private int _workingTier;
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
        private bool _hasComparison;
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
            _customRoles = new List<GearRoleDefinition>();
            _savedCustomRoles = new List<GearRoleDefinition>();
            _pendingImportRoles = new List<PendingImportRole>();
            _tiers = new MBBindingList<GearTierOptionViewModel>();
            _categories = new MBBindingList<GearCategoryOptionViewModel>();
            _slots = new MBBindingList<GearSlotOptionViewModel>();
            _items = new MBBindingList<GearItemOptionViewModel>();
            _allItems = new List<GearItemOptionViewModel>();
            _filters = new MBBindingList<GearItemFilterOptionViewModel>();
            _sortOptions = new MBBindingList<GearItemSortOptionViewModel>();
            _itemPreview = new ItemPreviewVM(OnItemPreviewClosed);
            _inspectionTooltip = new GearItemTooltipViewModel();
            _configuredTooltip = new GearItemTooltipViewModel();
            _itemSortOrder = GearItemSortOrder.ValueAscending;
            _previewStateText = "Preview will load when an item is selected.";
            _statusText = "Select a role and tier to edit a preset.";
            _page = Page.Roles;

            ReloadStagedRoles();
            _sortOptions.Add(new GearItemSortOptionViewModel(GearItemSortOrder.ValueAscending, "Price: low to high", SelectSort));
            _sortOptions.Add(new GearItemSortOptionViewModel(GearItemSortOrder.ValueDescending, "Price: high to low", SelectSort));
            SetSelectedSortOption();
        }

        [DataSourceProperty]
        public MBBindingList<GearRoleOptionViewModel> RoleOptions => _roles;

        [DataSourceProperty]
        public string RoleCountText => $"{_roles.Count} / {GearPresetRepository.MaxRoleCount} roles";

        [DataSourceProperty]
        public bool CanAddRole => _roles.Count < GearPresetRepository.MaxRoleCount;

        [DataSourceProperty]
        public bool IsAddRoleDisabled => !CanAddRole;

        [DataSourceProperty]
        public HintViewModel AddRoleHint => CreateNameHint(
            CanAddRole
                ? "Create a custom role with three upgrade tiers."
                : "You can configure at most 10 roles, including Archer, Infantry, and Lancer.");

        [DataSourceProperty]
        public bool CanExportRole => IsRoleSelectionVisible &&
            !HasUnsavedChanges &&
            !string.IsNullOrEmpty(_role) &&
            ContainsStagedRole(_role);

        [DataSourceProperty]
        public bool CanExportAll => IsRoleSelectionVisible &&
            !HasUnsavedChanges &&
            _roles.Count > 0;

        [DataSourceProperty]
        public bool CanImport => IsRoleSelectionVisible &&
            !HasUnsavedChanges &&
            !_isImportInProgress;

        [DataSourceProperty]
        public HintViewModel ExportRoleHint => CreateNameHint(
            HasUnsavedChanges
                ? "Save pending changes before exporting a role."
                : string.IsNullOrEmpty(_role)
                    ? "Select a role before exporting it."
                    : "Export the selected role and its three tiers to a JSON file.");

        [DataSourceProperty]
        public HintViewModel ExportAllHint => CreateNameHint(
            HasUnsavedChanges
                ? "Save pending changes before exporting roles."
                : "Export all roles and their three tiers to a JSON file.");

        [DataSourceProperty]
        public HintViewModel ImportHint => CreateNameHint(
            HasUnsavedChanges
                ? "Save or discard pending changes before importing a JSON file."
                : _isImportInProgress
                    ? "An import is already waiting for a conflict decision."
                    : "Import roles and presets from a JSON file into this save.");

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
        public ItemCollectionElementViewModel PreviewTableau => _itemPreview?.ItemTableau;

        [DataSourceProperty]
        public GearItemTooltipViewModel InspectionTooltip => _inspectionTooltip;

        [DataSourceProperty]
        public GearItemTooltipViewModel ConfiguredTooltip => _configuredTooltip;

        [DataSourceProperty]
        public bool HasPreviewItem => !string.IsNullOrEmpty(_readyPreviewItemId) &&
            string.Equals(_readyPreviewItemId, _requestedPreviewItemId, StringComparison.Ordinal);

        [DataSourceProperty]
        public string PreviewStateText => _previewStateText;

        [DataSourceProperty]
        public bool HasInspectionItem => _inspectedItem != null;

        private bool HasComparison => _hasComparison;

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
                OnPropertyChanged(nameof(IsSaveExitVisible));
                OnPropertyChanged(nameof(IsSaveCancelVisible));
                OnPropertyChanged(nameof(IsBackVisible));
                NotifyTransferActionState();
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
        public bool IsSaveExitVisible => IsWindowOpen &&
            (_page == Page.Roles || _page == Page.Categories);

        // Kept for compatibility with older prefabs. New UI bindings should
        // use IsSaveExitVisible so the action is named after its behavior.
        [DataSourceProperty]
        public bool IsSaveCancelVisible => IsSaveExitVisible;

        [DataSourceProperty]
        public bool IsBackVisible => IsWindowOpen &&
            (_page == Page.Tiers || _page == Page.Categories || _page == Page.Slots || _page == Page.Items);

        private bool HasUnsavedTierChanges => !SnapshotsEqual(_working, _savedSnapshot);

        private bool HasUnsavedRoleChanges => !RoleDefinitionsEqual(_customRoles, _savedCustomRoles);

        private bool HasUnsavedChanges => HasUnsavedTierChanges || HasUnsavedRoleChanges;

        public void SetHostScreenVisible(bool visible)
        {
            if (_isHostScreenVisible == visible)
                return;

            _isHostScreenVisible = visible;
            if (!visible && IsWindowOpen)
            {
                bool discardedChanges = HasUnsavedChanges;
                CloseConfiguration();
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
                    return $"{GetRoleDisplayName(_role)} > Choose a tier";
                if (_page == Page.Categories)
                    return $"{GetRoleDisplayName(_role)} > Tier {_tier} > Choose a category";
                if (_page == Page.Slots)
                    return $"{GetRoleDisplayName(_role)} > Tier {_tier} > {_category} > Choose a slot";
                return $"{GetRoleDisplayName(_role)} > Tier {_tier} > {_category} > {GetSlotName(_slot)}";
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

        private string CurrentItemName
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

        private string CurrentItemStringId
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

        private string SelectedCandidateName
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
        public HintViewModel SaveHint => CreateNameHint("Save all temporary role and tier changes. The configuration remains open.");

        [DataSourceProperty]
        public HintViewModel ExitHint => CreateNameHint("Exit the configuration. Unsaved changes require confirmation before they are discarded.");

        public void ExecuteOpenConfiguration()
        {
            PreparePreviewSession();
            _role = null;
            _working = null;
            _savedSnapshot = null;
            _workingRole = null;
            _workingTier = 0;
            _tier = 0;
            _selectedCandidateId = null;
            ClearItemInspection();
            ClearPendingImport();
            ReloadStagedRoles();
            _page = Page.Roles;
            StatusText = "Select a role and tier to edit a preset.";
            NotifyPageChanged();
            IsWindowOpen = true;
        }

        public void ExecuteAddRole()
        {
            if (!CanAddRole)
            {
                StatusText = "The 10 role limit has been reached.";
                return;
            }

            InformationManager.ShowTextInquiry(new TextInquiryData(
                "CGU - Add role",
                "Enter a unique name for the new role:",
                true,
                true,
                "Add role",
                "Cancel",
                CreateCustomRoleDraft,
                () => StatusText = "Role creation cancelled."
            ));
        }

        public void ExecuteDeleteRole(GearRoleOptionViewModel option)
        {
            if (option == null || !option.IsCustomRole)
            {
                StatusText = "Archer, Infantry, and Lancer are default roles and cannot be deleted.";
                return;
            }

            if (!ContainsCustomRole(option.RoleId))
            {
                StatusText = "This role is no longer available.";
                return;
            }

            InformationManager.ShowInquiry(new InquiryData(
                "CGU - Delete role",
                $"Delete the custom role '{option.Name}'? Its three tiers and all saved configuration for this role will be removed when you save.",
                true,
                true,
                "Delete",
                "Keep role",
                () => DeleteCustomRoleDraft(option),
                () => StatusText = "Role deletion cancelled."
            ));
        }

        public void ExecuteExportRole()
        {
            if (!CanExportRole)
            {
                StatusText = HasUnsavedChanges
                    ? "Save pending changes before exporting a role."
                    : "Select a role before exporting it.";
                return;
            }

            string roleId = _role;
            InformationManager.ShowTextInquiry(new TextInquiryData(
                "CGU - Export role",
                "Enter the destination file path. The .json extension is added if needed:",
                true,
                true,
                "Export",
                "Cancel",
                path => ExportRolesToFile(path, new[] { roleId }, GetRoleDisplayName(roleId)),
                () => StatusText = "Role export cancelled."
            ));
        }

        private void ExecuteExportRoleFromOption(GearRoleOptionViewModel option)
        {
            if (option == null || string.IsNullOrEmpty(option.RoleId) ||
                !ContainsStagedRole(option.RoleId))
            {
                StatusText = "The selected role is no longer available to export.";
                return;
            }

            if (HasUnsavedChanges)
            {
                StatusText = "Save pending changes before exporting a role.";
                return;
            }

            string roleId = option.RoleId;
            string roleName = option.Name;
            InformationManager.ShowTextInquiry(new TextInquiryData(
                "CGU - Export role",
                "Enter the destination file path. The .json extension is added if needed:",
                true,
                true,
                "Export",
                "Cancel",
                path => ExportRolesToFile(path, new[] { roleId }, roleName),
                () => StatusText = "Role export cancelled."
            ));
        }

        public void ExecuteExportAll()
        {
            if (!CanExportAll)
            {
                StatusText = HasUnsavedChanges
                    ? "Save pending changes before exporting roles."
                    : "No roles are available to export.";
                return;
            }

            InformationManager.ShowTextInquiry(new TextInquiryData(
                "CGU - Export all roles",
                "Enter the destination file path. The .json extension is added if needed:",
                true,
                true,
                "Export",
                "Cancel",
                path => ExportRolesToFile(path, null, "all roles"),
                () => StatusText = "Role export cancelled."
            ));
        }

        public void ExecuteImport()
        {
            if (!CanImport)
            {
                StatusText = HasUnsavedChanges
                    ? "Save or discard pending changes before importing a JSON file."
                    : _isImportInProgress
                        ? "Resolve or cancel the current import before starting another one."
                        : "Import is only available from the role list.";
                return;
            }

            InformationManager.ShowTextInquiry(new TextInquiryData(
                "CGU - Import roles",
                "Enter the full path of the JSON file to import into the current save:",
                true,
                true,
                "Import",
                "Cancel",
                BeginImportFromFile,
                () => StatusText = "Role import cancelled."
            ));
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
            GearPreset defaultPreset = null;
            if (HasUnsavedTierChanges)
            {
                if (string.IsNullOrEmpty(_workingRole) || !ContainsStagedRole(_workingRole))
                {
                    StatusText = "The edited role no longer exists.";
                    return;
                }

                defaultPreset = _service.GetDefaultPresetOrNull(_workingRole, _workingTier);
                if (defaultPreset == null)
                {
                    StatusText = "The selected preset is not available.";
                    return;
                }
            }

            string error;
            if (HasUnsavedRoleChanges && !_service.TryCommitCustomRoles(_customRoles, out error))
            {
                StatusText = string.IsNullOrEmpty(error) ? "The custom roles could not be saved." : error;
                return;
            }

            if (HasUnsavedTierChanges)
            {
                _overrides.CommitSnapshot(_workingRole, _workingTier, defaultPreset, _working);
                _savedSnapshot = _working.Clone();
                foreach (GearTierOptionViewModel tierOption in _tiers)
                {
                    if (tierOption.Tier == _workingTier)
                        tierOption.SetCost(_working.Cost);
                }
            }

            _savedCustomRoles = new List<GearRoleDefinition>(_customRoles);
            NotifyTransferActionState();
            StatusText = "Changes saved. You can continue editing or exit the configuration.";
            InformationManager.DisplayMessage(new InformationMessage("[CGU] Preset configuration saved."));
        }

        public void ExecuteExit()
        {
            if (!HasUnsavedChanges)
            {
                CloseConfiguration();
                return;
            }

            StatusText = "Unsaved changes are pending. Save before exiting, or discard them to exit.";
            InformationManager.ShowInquiry(new InquiryData(
                "CGU - Unsaved changes",
                "You have unsaved role or tier changes. Exit without saving and discard them?",
                true,
                true,
                "Exit without saving",
                "Keep editing",
                () =>
                {
                    CloseConfiguration();
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
            StatusText = $"{GetSlotName(_slot)} was removed from the temporary snapshot.";
        }

        public void ExecuteResetTierToDefault()
        {
            GearPresetSnapshot defaultSnapshot = CreateDefaultTierSnapshot(_role, _tier);
            if (defaultSnapshot == null)
            {
                StatusText = "The selected preset is not available.";
                return;
            }

            _working = defaultSnapshot;
            RefreshSlotLabels();
            NotifyCurrentItemChanged();
            OnPropertyChanged(nameof(CurrentTierCostText));
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
            if (_working == null)
            {
                StatusText = "Calculated price is invalid.";
                return;
            }

            _working = new GearPresetSnapshot(calculatedPrice, _working.Slots);
            OnPropertyChanged(nameof(CurrentTierCostText));
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

        private GearPresetSnapshot CreateDefaultTierSnapshot(string roleId, int tier)
        {
            GearPreset defaultPreset = _service.GetDefaultPresetOrNull(roleId, tier);
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
    }
}
