using System;
using System.Collections.Generic;
using CompanionGearUpgrades.Services;
using TaleWorlds.Core;

namespace CompanionGearUpgrades.UI
{
    public sealed partial class GearPresetConfigViewModel
    {
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
            _hoveredCandidate = null;
        }

        private void RefreshItemInspection()
        {
            ItemObject configuredItem = GearItemCatalog.FindById(GetWorkingSlotId(_slot));
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
            _hasComparison = hasComparison;
            SetPreviewItem(_inspectedItem);

            OnPropertyChanged(nameof(HasInspectionItem));
            OnPropertyChanged(nameof(HasSingleInspection));
            OnPropertyChanged(nameof(HasHoveredComparison));
        }

        private ItemObject GetInspectedItem(ItemObject configuredItem, ItemObject selectedItem)
        {
            if (_hoveredCandidate != null)
                return _hoveredCandidate.Item;

            return selectedItem ?? configuredItem;
        }

    }
}
