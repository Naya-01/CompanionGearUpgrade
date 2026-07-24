using System;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace CompanionGearUpgrades.UI
{
    /// <summary>
    /// Lightweight data backing the custom tooltip. It deliberately reuses
    /// ItemMenuTooltipPropertyVM so its rows follow native inventory tooltip
    /// conventions without creating an InventoryLogic or SPInventoryVM.
    /// </summary>
    public sealed class GearItemTooltipViewModel : ViewModel
    {
        private const int PropertyLabelWidth = 25;

        private readonly MBBindingList<ItemMenuTooltipPropertyVM> _properties =
            new MBBindingList<ItemMenuTooltipPropertyVM>();
        private ItemImageIdentifierVM _imageIdentifier;
        private string _itemName;

        [DataSourceProperty]
        public MBBindingList<ItemMenuTooltipPropertyVM> Properties => _properties;

        [DataSourceProperty]
        public ItemImageIdentifierVM ImageIdentifier
        {
            get { return _imageIdentifier; }
            private set
            {
                if (ReferenceEquals(_imageIdentifier, value))
                    return;

                _imageIdentifier = value;
                OnPropertyChanged(nameof(ImageIdentifier));
            }
        }

        [DataSourceProperty]
        public string ItemName
        {
            get { return _itemName; }
            private set
            {
                if (string.Equals(_itemName, value, StringComparison.Ordinal))
                    return;

                _itemName = value;
                OnPropertyChanged(nameof(ItemName));
            }
        }

        public void SetItem(ItemObject item)
        {
            _properties.Clear();
            ItemName = item != null ? item.Name.ToString() : string.Empty;
            ImageIdentifier = item != null ? new ItemImageIdentifierVM(item, string.Empty) : null;

            if (item == null)
                return;

            AddProperty("Type", item.ItemType.ToString());
            AddProperty("Tier", item.Tier.ToString());
            AddProperty("Value", item.Value.ToString());
            AddProperty("Weight", item.Weight.ToString("0.##"));

            if (item.ArmorComponent != null)
            {
                AddPositiveProperty("Head armor", item.ArmorComponent.HeadArmor);
                AddPositiveProperty("Body armor", item.ArmorComponent.BodyArmor);
                AddPositiveProperty("Arm armor", item.ArmorComponent.ArmArmor);
                AddPositiveProperty("Leg armor", item.ArmorComponent.LegArmor);
                AddPositiveProperty("Speed bonus", item.ArmorComponent.SpeedBonus);
                AddPositiveProperty("Maneuver bonus", item.ArmorComponent.ManeuverBonus);
                AddPositiveProperty("Charge bonus", item.ArmorComponent.ChargeBonus);
            }

            if (item.HorseComponent != null)
            {
                AddPositiveProperty("Hit points", item.HorseComponent.HitPoints);
                AddPositiveProperty("Speed", item.HorseComponent.Speed);
                AddPositiveProperty("Maneuver", item.HorseComponent.Maneuver);
                AddPositiveProperty("Charge damage", item.HorseComponent.ChargeDamage);
            }

            if (item.PrimaryWeapon != null)
            {
                AddPositiveProperty("Swing damage", item.PrimaryWeapon.SwingDamage);
                AddPositiveProperty("Swing speed", item.PrimaryWeapon.SwingSpeed);
                AddPositiveProperty("Thrust damage", item.PrimaryWeapon.ThrustDamage);
                AddPositiveProperty("Thrust speed", item.PrimaryWeapon.ThrustSpeed);
                AddPositiveProperty("Missile damage", item.PrimaryWeapon.MissileDamage);
                AddPositiveProperty("Missile speed", item.PrimaryWeapon.MissileSpeed);
                AddPositiveProperty("Accuracy", item.PrimaryWeapon.Accuracy);
                AddPositiveProperty("Handling", item.PrimaryWeapon.Handling);
                AddPositiveProperty("Weapon length", item.PrimaryWeapon.WeaponLength);
                AddPositiveProperty("Ammo", item.PrimaryWeapon.MaxDataValue);
            }
        }

        private void AddPositiveProperty(string label, int value)
        {
            if (value > 0)
                AddProperty(label, value.ToString());
        }

        private void AddProperty(string label, string value)
        {
            _properties.Add(new ItemMenuTooltipPropertyVM(
                label,
                value,
                PropertyLabelWidth,
                false,
                null,
                string.Empty,
                false));
        }
    }
}
