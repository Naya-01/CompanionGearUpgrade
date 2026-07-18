using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Domain;
using CompanionGearUpgrades.Services;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace CompanionGearUpgrades.UI
{
    public enum GearPresetCategory
    {
        Weapons,
        Armors,
        Horse
    }

    /// <summary>
    /// Gauntlet state for Clan > Equipment. The working snapshot is created
    /// when a role/tier is selected and is committed only by ExecuteSave.
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

        private GearRole _role;
        private int _tier;
        private GearPresetCategory _category;
        private EquipmentIndex _slot;
        private GearPresetSnapshot _working;
        private string _selectedCandidateId;
        private Page _page;
        private bool _isWindowOpen;
        private bool _isClanScreenVisible;
        private string _statusText;

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
            _statusText = "Select a role and tier to edit a preset.";
            _page = Page.Roles;

            _roles.Add(new GearRoleOptionViewModel(GearRole.Infantry, "Infantry", SelectRole));
            _roles.Add(new GearRoleOptionViewModel(GearRole.Archer, "Archer", SelectRole));
            _roles.Add(new GearRoleOptionViewModel(GearRole.Lancer, "Lancer", SelectRole));
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
        public bool IsClanScreenVisible
        {
            get { return _isClanScreenVisible; }
            private set
            {
                if (_isClanScreenVisible == value)
                    return;

                _isClanScreenVisible = value;
                OnPropertyChanged(nameof(IsClanScreenVisible));
                if (!value)
                    CloseWithoutSaving();
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

        public void SetClanScreenVisible(bool visible)
        {
            IsClanScreenVisible = visible;
        }

        public void ExecuteOpenConfiguration()
        {
            _working = null;
            _selectedCandidateId = null;
            _page = Page.Roles;
            StatusText = "Select a role and tier to edit a preset.";
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
                    CloseWithoutSaving();
                    break;
            }
        }

        public void ExecuteSave()
        {
            if (_working == null)
            {
                CloseWithoutSaving();
                return;
            }

            GearPreset defaultPreset = _service.GetDefaultPresetOrNull(_role, _tier);
            if (defaultPreset == null)
            {
                StatusText = "The selected preset is not available.";
                return;
            }

            _overrides.CommitSnapshot(_role, _tier, defaultPreset, _working);
            StatusText = "Saved to the campaign overrides.";
            _working = null;
            IsWindowOpen = false;
        }

        public void ExecuteCancel()
        {
            CloseWithoutSaving();
        }

        public void ExecuteSelectItem()
        {
            if (_working == null || string.IsNullOrEmpty(_selectedCandidateId))
            {
                StatusText = "Select an item in the left panel first.";
                return;
            }

            _working.Slots[_slot] = _selectedCandidateId;
            _selectedCandidateId = null;
            RefreshSlotLabels();
            NotifyCurrentItemChanged();
            StatusText = $"{GetSlotName(_slot)} changed in the temporary snapshot.";
        }

        public void ExecuteRemoveItem()
        {
            if (_working == null)
                return;

            // Null is intentional here. CommitSnapshot persists it as an
            // explicit empty marker instead of falling back to the default.
            _working.Slots[_slot] = null;
            _selectedCandidateId = null;
            RefreshSlotLabels();
            NotifyCurrentItemChanged();
            StatusText = $"{GetSlotName(_slot)} will be empty after Save.";
        }

        private void SelectRole(GearRoleOptionViewModel option)
        {
            _role = option.Role;
            _tiers.Clear();
            for (int tier = 1; tier <= 3; tier++)
                _tiers.Add(new GearTierOptionViewModel(tier, _service.GetEffectiveCost(_role, tier), SelectTier));

            SetPage(Page.Tiers);
        }

        private void SelectTier(GearTierOptionViewModel option)
        {
            GearPreset preset = _service.GetDefaultPresetOrNull(_role, option.Tier);
            if (preset == null)
            {
                StatusText = "The selected preset is not available.";
                return;
            }

            _tier = option.Tier;
            _working = _service.BuildEffectiveSnapshot(_role, _tier, preset);
            _categories.Clear();
            _categories.Add(new GearCategoryOptionViewModel(GearPresetCategory.Weapons, "Weapons", SelectCategory));
            _categories.Add(new GearCategoryOptionViewModel(GearPresetCategory.Armors, "Armors", SelectCategory));
            _categories.Add(new GearCategoryOptionViewModel(GearPresetCategory.Horse, "Horse", SelectCategory));
            SetPage(Page.Categories);
        }

        private void SelectCategory(GearCategoryOptionViewModel option)
        {
            _category = option.Category;
            _slots.Clear();

            foreach (EquipmentIndex slot in GetSlotsForCategory(_category))
                _slots.Add(new GearSlotOptionViewModel(slot, GetSlotLabel(slot), SelectSlot));

            SetPage(Page.Slots);
        }

        private void SelectSlot(GearSlotOptionViewModel option)
        {
            _slot = option.Slot;
            _selectedCandidateId = null;
            _items.Clear();

            foreach (ItemObject item in _service.GetCompatibleItems(_slot))
                _items.Add(new GearItemOptionViewModel(item, HighlightCandidate));

            NotifyCurrentItemChanged();
            NotifyCandidateChanged();
            SetPage(Page.Items);
        }

        private void HighlightCandidate(GearItemOptionViewModel option)
        {
            _selectedCandidateId = option.ItemId;
            foreach (GearItemOptionViewModel item in _items)
                item.SetSelected(string.Equals(item.ItemId, _selectedCandidateId, StringComparison.Ordinal));

            NotifyCandidateChanged();
        }

        private void SetPage(Page page)
        {
            _page = page;
            NotifyPageChanged();
        }

        private void NotifyPageChanged()
        {
            OnPropertyChanged(nameof(IsRoleSelectionVisible));
            OnPropertyChanged(nameof(IsTierSelectionVisible));
            OnPropertyChanged(nameof(IsCategorySelectionVisible));
            OnPropertyChanged(nameof(IsSlotSelectionVisible));
            OnPropertyChanged(nameof(IsItemSelectionVisible));
            OnPropertyChanged(nameof(Breadcrumb));
        }

        private void NotifyCurrentItemChanged()
        {
            OnPropertyChanged(nameof(CurrentItemName));
            OnPropertyChanged(nameof(CurrentItemStringId));
            OnPropertyChanged(nameof(CurrentItemStringIdLabel));
        }

        private void NotifyCandidateChanged()
        {
            OnPropertyChanged(nameof(SelectedCandidateName));
            OnPropertyChanged(nameof(SelectedCandidateStringId));
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

            foreach (GearItemOptionViewModel item in _items)
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

        private void CloseWithoutSaving()
        {
            _working = null;
            _selectedCandidateId = null;
            IsWindowOpen = false;
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

        public GearRoleOptionViewModel(GearRole role, string name, Action<GearRoleOptionViewModel> onSelected)
        {
            Role = role;
            Name = name;
            _onSelected = onSelected;
        }

        public GearRole Role { get; private set; }

        [DataSourceProperty]
        public string Name { get; private set; }

        public void ExecuteSelect()
        {
            _onSelected?.Invoke(this);
        }
    }

    public sealed class GearTierOptionViewModel : ViewModel
    {
        private readonly Action<GearTierOptionViewModel> _onSelected;

        public GearTierOptionViewModel(int tier, int cost, Action<GearTierOptionViewModel> onSelected)
        {
            Tier = tier;
            Cost = cost;
            _onSelected = onSelected;
        }

        public int Tier { get; private set; }

        [DataSourceProperty]
        public string Name => $"Tier {Tier} ({Cost} gold)";

        private int Cost { get; set; }

        public void ExecuteSelect()
        {
            _onSelected?.Invoke(this);
        }
    }

    public sealed class GearCategoryOptionViewModel : ViewModel
    {
        private readonly Action<GearCategoryOptionViewModel> _onSelected;

        public GearCategoryOptionViewModel(GearPresetCategory category, string name, Action<GearCategoryOptionViewModel> onSelected)
        {
            Category = category;
            Name = name;
            _onSelected = onSelected;
        }

        public GearPresetCategory Category { get; private set; }

        [DataSourceProperty]
        public string Name { get; private set; }

        public void ExecuteSelect()
        {
            _onSelected?.Invoke(this);
        }
    }

    public sealed class GearSlotOptionViewModel : ViewModel
    {
        private readonly Action<GearSlotOptionViewModel> _onSelected;
        private string _label;

        public GearSlotOptionViewModel(EquipmentIndex slot, string label, Action<GearSlotOptionViewModel> onSelected)
        {
            Slot = slot;
            _label = label;
            _onSelected = onSelected;
        }

        public EquipmentIndex Slot { get; private set; }

        [DataSourceProperty]
        public string Name => _label;

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
    }

    public sealed class GearItemOptionViewModel : ViewModel
    {
        private readonly Action<GearItemOptionViewModel> _onSelected;
        private bool _isSelected;

        public GearItemOptionViewModel(ItemObject item, Action<GearItemOptionViewModel> onSelected)
        {
            ItemId = item.StringId;
            ItemName = item.Name.ToString();
            _onSelected = onSelected;
        }

        [DataSourceProperty]
        public string ItemId { get; private set; }

        [DataSourceProperty]
        public string ItemName { get; private set; }

        [DataSourceProperty]
        public string DisplayText => $"{ItemName}  [{ItemId}]";

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

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
        }
    }
}
