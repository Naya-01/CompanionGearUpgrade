using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CompanionGearUpgrades.UI
{
    /// <summary>
    /// Represents one resolved equipment slot in the read-only application
    /// preview. A missing or empty item remains visible, but cannot be sent to
    /// the native 3D item preview.
    /// </summary>
    public sealed class ApplyGearPresetEquipmentOptionViewModel : ViewModel
    {
        private readonly Action<ApplyGearPresetEquipmentOptionViewModel> _onSelected;
        private bool _isSelected;

        public ApplyGearPresetEquipmentOptionViewModel(
            EquipmentIndex slot,
            string slotName,
            string itemName,
            string itemId,
            bool isAvailable,
            Action<ApplyGearPresetEquipmentOptionViewModel> onSelected)
        {
            Slot = slot;
            SlotName = slotName;
            ItemName = itemName;
            ItemId = itemId;
            IsAvailable = isAvailable;
            _onSelected = onSelected;
        }

        public EquipmentIndex Slot { get; private set; }
        public string ItemId { get; private set; }

        [DataSourceProperty]
        public string SlotName { get; private set; }

        [DataSourceProperty]
        public string ItemName { get; private set; }

        [DataSourceProperty]
        public bool IsAvailable { get; private set; }

        [DataSourceProperty]
        public bool IsUnavailable => !IsAvailable && !string.IsNullOrEmpty(ItemId);

        [DataSourceProperty]
        public bool IsEmpty => string.IsNullOrEmpty(ItemId);

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
