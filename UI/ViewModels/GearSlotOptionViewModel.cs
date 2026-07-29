using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CompanionGearUpgrades.UI
{
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
}
