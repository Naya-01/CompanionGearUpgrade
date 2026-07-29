using System;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace CompanionGearUpgrades.UI
{
    public sealed class GearItemOptionViewModel : ViewModel
    {
        private readonly Action<GearItemOptionViewModel> _onSelected;
        private readonly Action<GearItemOptionViewModel> _onHoverBegin;
        private readonly Action<GearItemOptionViewModel> _onHoverEnd;
        private bool _isSelected;

        public GearItemOptionViewModel(
            ItemObject item,
            Action<GearItemOptionViewModel> onSelected,
            Action<GearItemOptionViewModel> onHoverBegin,
            Action<GearItemOptionViewModel> onHoverEnd)
        {
            Item = item;
            ItemId = item.StringId;
            ItemName = item.Name.ToString();
            ItemTypeName = item.ItemType.ToString();
            ItemValue = item.Value;
            _onSelected = onSelected;
            _onHoverBegin = onHoverBegin;
            _onHoverEnd = onHoverEnd;
            ImageIdentifier = new ItemImageIdentifierVM(item, string.Empty);
        }

        public ItemObject Item { get; private set; }

        [DataSourceProperty]
        public string ItemTypeName { get; private set; }

        [DataSourceProperty]
        public int ItemValue { get; private set; }

        [DataSourceProperty]
        public ItemImageIdentifierVM ImageIdentifier { get; private set; }

        [DataSourceProperty]
        public string ItemId { get; private set; }

        [DataSourceProperty]
        public string ItemName { get; private set; }

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

        public void ExecuteHighlight()
        {
            _onSelected?.Invoke(this);
        }

        public void ExecuteHoverBegin()
        {
            _onHoverBegin?.Invoke(this);
        }

        public void ExecuteHoverEnd()
        {
            _onHoverEnd?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
        }
    }
}
