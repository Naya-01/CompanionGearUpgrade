using CompanionGearUpgrades.Domain;
using System;
using TaleWorlds.Library;

namespace CompanionGearUpgrades.UI
{
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
}
