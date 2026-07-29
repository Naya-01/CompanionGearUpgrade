using System;
using TaleWorlds.Library;

namespace CompanionGearUpgrades.UI
{
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
}
