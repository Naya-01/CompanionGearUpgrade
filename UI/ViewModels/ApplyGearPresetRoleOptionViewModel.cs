using System;
using TaleWorlds.Library;

namespace CompanionGearUpgrades.UI
{
    /// <summary>
    /// A lightweight role row used exclusively by the preset-application
    /// window. Keeping it separate from the editor role row avoids exposing
    /// editor-only actions such as Delete and Export in the apply flow.
    /// </summary>
    public sealed class ApplyGearPresetRoleOptionViewModel : ViewModel
    {
        private readonly Action<ApplyGearPresetRoleOptionViewModel> _onSelected;
        private bool _isSelected;

        public ApplyGearPresetRoleOptionViewModel(
            string roleId,
            string name,
            Action<ApplyGearPresetRoleOptionViewModel> onSelected)
        {
            RoleId = roleId;
            Name = name;
            _onSelected = onSelected;
        }

        public string RoleId { get; private set; }

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
