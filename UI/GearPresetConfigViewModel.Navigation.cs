using CompanionGearUpgrades.Domain;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CompanionGearUpgrades.UI
{
    public sealed partial class GearPresetConfigViewModel
    {
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
            if (_working != null && _workingRole == _role && _workingTier == option.Tier)
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
            _categories.Clear();
            _categories.Add(new GearCategoryOptionViewModel(GearPresetCategory.Weapons, "Weapons", SelectCategory));
            _categories.Add(new GearCategoryOptionViewModel(GearPresetCategory.Armors, "Armors", SelectCategory));
            _categories.Add(new GearCategoryOptionViewModel(GearPresetCategory.Horse, "Horse", SelectCategory));
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

    }
}
