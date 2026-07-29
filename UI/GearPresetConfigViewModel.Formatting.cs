using CompanionGearUpgrades.Domain;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Localization;

namespace CompanionGearUpgrades.UI
{
    public sealed partial class GearPresetConfigViewModel
    {
        private static IEnumerable<string> GetOrderedItemTypeNames()
        {
            foreach (ItemObject.ItemTypeEnum itemType in GearSlotCatalog.SupportedItemTypes)
                yield return itemType.ToString();
        }

        private static string GetItemTypeDisplayName(string itemTypeName)
        {
            switch (itemTypeName)
            {
                case "OneHandedWeapon": return "One-handed";
                case "TwoHandedWeapon": return "Two-handed";
                case "Polearm": return "Polearms";
                case "Bow": return "Bows";
                case "Crossbow": return "Crossbows";
                case "Thrown": return "Thrown";
                case "Shield": return "Shields";
                case "Arrows": return "Arrows";
                case "Bolts": return "Bolts";
                case "HeadArmor": return "Head";
                case "BodyArmor": return "Body";
                case "Cape": return "Capes";
                case "HandArmor": return "Gloves";
                case "LegArmor": return "Legs";
                case "Horse": return "Horses";
                case "HorseHarness": return "Harnesses";
                default: return itemTypeName;
            }
        }

        private static string TruncatePreviewName(string name)
        {
            const int maximumLength = 28;
            if (string.IsNullOrEmpty(name) || name.Length <= maximumLength)
                return name;

            return name.Substring(0, maximumLength - 3) + "...";
        }

        private static HintViewModel CreateNameHint(string name)
        {
            return new HintViewModel(new TextObject(name ?? string.Empty), null);
        }

        private static IEnumerable<EquipmentIndex> GetSlotsForCategory(GearPresetCategory category)
        {
            switch (category)
            {
                case GearPresetCategory.Weapons:
                    return GearSlotCatalog.WeaponSlots;
                case GearPresetCategory.Armors:
                    return GearSlotCatalog.ArmorSlots;
                default:
                    return GearSlotCatalog.MountSlots;
            }
        }

        private static string GetSlotName(EquipmentIndex slot)
        {
            return GearSlotCatalog.GetDisplayName(slot);
        }
    }
}
