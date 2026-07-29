using System;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace CompanionGearUpgrades.UI
{
    public sealed class GearRoleOptionViewModel : ViewModel
    {
        private readonly Action<GearRoleOptionViewModel> _onSelected;
        private readonly Action<GearRoleOptionViewModel> _onDelete;
        private readonly Action<GearRoleOptionViewModel> _onExport;
        private bool _isSelected;

        public GearRoleOptionViewModel(
            string roleId,
            string name,
            bool isDefaultRole,
            Action<GearRoleOptionViewModel> onSelected,
            Action<GearRoleOptionViewModel> onDelete,
            Action<GearRoleOptionViewModel> onExport)
        {
            RoleId = roleId;
            Name = name;
            IsDefaultRole = isDefaultRole;
            _onSelected = onSelected;
            _onDelete = onDelete;
            _onExport = onExport;
        }

        /// <summary>
        /// Stable persisted role identifier. Unlike the original enum, this
        /// also identifies a player-created role.
        /// </summary>
        public string RoleId { get; private set; }

        public bool IsDefaultRole { get; private set; }

        [DataSourceProperty]
        public string Name { get; private set; }

        [DataSourceProperty]
        public bool IsCustomRole => !IsDefaultRole;

        [DataSourceProperty]
        public HintViewModel DeleteRoleHint => new HintViewModel(
            new TaleWorlds.Localization.TextObject(
                "Delete this custom role and all of its tier configuration when you save."),
            null);

        [DataSourceProperty]
        public HintViewModel ExportRoleHint => new HintViewModel(
            new TaleWorlds.Localization.TextObject(
                "Export this role and its three tier presets to a JSON file."),
            null);

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

        public void ExecuteDelete()
        {
            _onDelete?.Invoke(this);
        }

        public void ExecuteExport()
        {
            _onExport?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
        }
    }
}
