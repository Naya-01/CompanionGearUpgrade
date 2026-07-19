using CompanionGearUpgrades.Data;
using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace CompanionGearUpgrades.UI
{
    public sealed partial class GearPresetConfigViewModel
    {
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
            OnPropertyChanged(nameof(Breadcrumb));
            OnPropertyChanged(nameof(CurrentTierCostText));
        }

        private void NotifyCurrentItemChanged()
        {
            OnPropertyChanged(nameof(CurrentItemStringIdLabel));
            OnPropertyChanged(nameof(CurrentItemDisplayName));
            OnPropertyChanged(nameof(CurrentItemNameHint));
        }

        private void NotifyCandidateChanged()
        {
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

        private void CloseWithoutSaving()
        {
            _working = null;
            _savedSnapshot = null;
            _selectedCandidateId = null;
            ClearItemInspection();
            IsWindowOpen = false;
            ReleasePreviewSession();
        }

    }
}

