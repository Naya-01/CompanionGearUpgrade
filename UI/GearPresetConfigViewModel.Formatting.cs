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
            return new[]
            {
                "OneHandedWeapon",
                "TwoHandedWeapon",
                "Polearm",
                "Bow",
                "Crossbow",
                "Thrown",
                "Shield",
                "Arrows",
                "Bolts",
                "Banner",
                "HeadArmor",
                "BodyArmor",
                "Cape",
                "HandArmor",
                "LegArmor",
                "Horse",
                "HorseHarness"
            };
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
                    return new[]
                    {
                        EquipmentIndex.Weapon0,
                        EquipmentIndex.Weapon1,
                        EquipmentIndex.Weapon2,
                        EquipmentIndex.Weapon3
                    };
                case GearPresetCategory.Armors:
                    return new[]
                    {
                        EquipmentIndex.Head,
                        EquipmentIndex.Body,
                        EquipmentIndex.Cape,
                        EquipmentIndex.Gloves,
                        EquipmentIndex.Leg
                    };
                default:
                    return new[] { EquipmentIndex.Horse, EquipmentIndex.HorseHarness };
            }
        }

        private static string GetSlotName(EquipmentIndex slot)
        {
            switch (slot)
            {
                case EquipmentIndex.Weapon0: return "Weapon0";
                case EquipmentIndex.Weapon1: return "Weapon1";
                case EquipmentIndex.Weapon2: return "Weapon2";
                case EquipmentIndex.Weapon3: return "Weapon3";
                case EquipmentIndex.Head: return "Head";
                case EquipmentIndex.Body: return "Body";
                case EquipmentIndex.Cape: return "Cape";
                case EquipmentIndex.Gloves: return "Gloves";
                case EquipmentIndex.Leg: return "Leg";
                case EquipmentIndex.Horse: return "Horse";
                case EquipmentIndex.HorseHarness: return "HorseHarness";
                default: return slot.ToString();
            }
        }
    }
}
