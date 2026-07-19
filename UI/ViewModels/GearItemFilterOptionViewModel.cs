using System;
using TaleWorlds.Library;

namespace CompanionGearUpgrades.UI
{
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
}
