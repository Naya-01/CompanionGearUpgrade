using System;
using TaleWorlds.Library;

namespace CompanionGearUpgrades.UI
{
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
}
