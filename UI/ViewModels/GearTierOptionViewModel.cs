using System;
using TaleWorlds.Library;

namespace CompanionGearUpgrades.UI
{
    public sealed class GearTierOptionViewModel : ViewModel
    {
        private readonly Action<GearTierOptionViewModel> _onSelected;
        private bool _isSelected;

        public GearTierOptionViewModel(int tier, int cost, Action<GearTierOptionViewModel> onSelected)
        {
            Tier = tier;
            Cost = cost;
            _onSelected = onSelected;
        }

        public int Tier { get; private set; }

        [DataSourceProperty]
        public string TierName => $"Tier {Tier}";

        [DataSourceProperty]
        public string TierCostText => $"{Cost} gold";

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

        private int Cost { get; set; }

        public void ExecuteSelect()
        {
            _onSelected?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
        }

        public void SetCost(int cost)
        {
            if (Cost == cost)
                return;

            Cost = cost;
            OnPropertyChanged(nameof(TierCostText));
        }
    }
}
