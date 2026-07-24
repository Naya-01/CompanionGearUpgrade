using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace CompanionGearUpgrades.Domain
{
    /// <summary>
    /// Single source of truth for the equipment slots supported by presets.
    /// The order and stable names are part of the JSON schema and must remain
    /// unchanged unless that schema is versioned.
    /// </summary>
    internal static class GearSlotCatalog
    {
        private sealed class SlotDefinition
        {
            public SlotDefinition(
                EquipmentIndex slot,
                string stableName,
                HashSet<ItemObject.ItemTypeEnum> allowedItemTypes)
            {
                Slot = slot;
                StableName = stableName;
                AllowedItemTypes = allowedItemTypes;
            }

            public EquipmentIndex Slot { get; private set; }
            public string StableName { get; private set; }
            public HashSet<ItemObject.ItemTypeEnum> AllowedItemTypes { get; private set; }
        }

        private static readonly Dictionary<EquipmentIndex, SlotDefinition> DefinitionsBySlot;
        private static readonly Dictionary<string, EquipmentIndex> SlotsByStableName;
        private static readonly HashSet<ItemObject.ItemTypeEnum> WeaponItemTypes;

        static GearSlotCatalog()
        {
            WeaponItemTypes = new HashSet<ItemObject.ItemTypeEnum>
            {
                ItemObject.ItemTypeEnum.OneHandedWeapon,
                ItemObject.ItemTypeEnum.TwoHandedWeapon,
                ItemObject.ItemTypeEnum.Polearm,
                ItemObject.ItemTypeEnum.Bow,
                ItemObject.ItemTypeEnum.Crossbow,
                ItemObject.ItemTypeEnum.Thrown,
                ItemObject.ItemTypeEnum.Shield,
                ItemObject.ItemTypeEnum.Arrows,
                ItemObject.ItemTypeEnum.Bolts,
                ItemObject.ItemTypeEnum.Banner
            };

            SupportedItemTypes = new List<ItemObject.ItemTypeEnum>
            {
                ItemObject.ItemTypeEnum.OneHandedWeapon,
                ItemObject.ItemTypeEnum.TwoHandedWeapon,
                ItemObject.ItemTypeEnum.Polearm,
                ItemObject.ItemTypeEnum.Bow,
                ItemObject.ItemTypeEnum.Crossbow,
                ItemObject.ItemTypeEnum.Thrown,
                ItemObject.ItemTypeEnum.Shield,
                ItemObject.ItemTypeEnum.Arrows,
                ItemObject.ItemTypeEnum.Bolts,
                ItemObject.ItemTypeEnum.Banner,
                ItemObject.ItemTypeEnum.HeadArmor,
                ItemObject.ItemTypeEnum.BodyArmor,
                ItemObject.ItemTypeEnum.Cape,
                ItemObject.ItemTypeEnum.HandArmor,
                ItemObject.ItemTypeEnum.LegArmor,
                ItemObject.ItemTypeEnum.Horse,
                ItemObject.ItemTypeEnum.HorseHarness
            }.AsReadOnly();

            DefinitionsBySlot = new Dictionary<EquipmentIndex, SlotDefinition>();
            SlotsByStableName = new Dictionary<string, EquipmentIndex>(StringComparer.Ordinal);

            var editableSlots = new List<EquipmentIndex>();
            var weaponSlots = new List<EquipmentIndex>();
            var armorSlots = new List<EquipmentIndex>();
            var mountSlots = new List<EquipmentIndex>();

            AddSlot(editableSlots, weaponSlots, EquipmentIndex.Weapon0, "Weapon0", WeaponItemTypes);
            AddSlot(editableSlots, weaponSlots, EquipmentIndex.Weapon1, "Weapon1", WeaponItemTypes);
            AddSlot(editableSlots, weaponSlots, EquipmentIndex.Weapon2, "Weapon2", WeaponItemTypes);
            AddSlot(editableSlots, weaponSlots, EquipmentIndex.Weapon3, "Weapon3", WeaponItemTypes);
            AddSlot(editableSlots, armorSlots, EquipmentIndex.Head, "Head", ItemObject.ItemTypeEnum.HeadArmor);
            AddSlot(editableSlots, armorSlots, EquipmentIndex.Body, "Body", ItemObject.ItemTypeEnum.BodyArmor);
            AddSlot(editableSlots, armorSlots, EquipmentIndex.Cape, "Cape", ItemObject.ItemTypeEnum.Cape);
            AddSlot(editableSlots, armorSlots, EquipmentIndex.Gloves, "Gloves", ItemObject.ItemTypeEnum.HandArmor);
            AddSlot(editableSlots, armorSlots, EquipmentIndex.Leg, "Leg", ItemObject.ItemTypeEnum.LegArmor);
            AddSlot(editableSlots, mountSlots, EquipmentIndex.Horse, "Horse", ItemObject.ItemTypeEnum.Horse);
            AddSlot(editableSlots, mountSlots, EquipmentIndex.HorseHarness, "HorseHarness", ItemObject.ItemTypeEnum.HorseHarness);

            EditableSlots = editableSlots.AsReadOnly();
            WeaponSlots = weaponSlots.AsReadOnly();
            ArmorSlots = armorSlots.AsReadOnly();
            MountSlots = mountSlots.AsReadOnly();
        }

        internal static IReadOnlyList<EquipmentIndex> EditableSlots { get; private set; }
        internal static IReadOnlyList<EquipmentIndex> WeaponSlots { get; private set; }
        internal static IReadOnlyList<EquipmentIndex> ArmorSlots { get; private set; }
        internal static IReadOnlyList<EquipmentIndex> MountSlots { get; private set; }
        internal static IReadOnlyList<ItemObject.ItemTypeEnum> SupportedItemTypes { get; private set; }

        internal static bool TryGetSlot(string stableName, out EquipmentIndex slot)
        {
            slot = default(EquipmentIndex);
            return !string.IsNullOrEmpty(stableName) &&
                SlotsByStableName.TryGetValue(stableName, out slot);
        }

        internal static string GetStableName(EquipmentIndex slot)
        {
            SlotDefinition definition;
            return DefinitionsBySlot.TryGetValue(slot, out definition)
                ? definition.StableName
                : null;
        }

        internal static string GetDisplayName(EquipmentIndex slot)
        {
            return GetStableName(slot) ?? slot.ToString();
        }

        internal static bool IsItemTypeAllowed(
            EquipmentIndex slot,
            ItemObject.ItemTypeEnum itemType)
        {
            SlotDefinition definition;
            if (DefinitionsBySlot.TryGetValue(slot, out definition))
                return definition.AllowedItemTypes.Contains(itemType);

            // Preserve the historical fallback used by the service for an
            // unknown EquipmentIndex, even though public flows only pass an
            // editable slot from this catalogue.
            return WeaponItemTypes.Contains(itemType);
        }

        private static void AddSlot(
            List<EquipmentIndex> editableSlots,
            List<EquipmentIndex> categorySlots,
            EquipmentIndex slot,
            string stableName,
            HashSet<ItemObject.ItemTypeEnum> allowedItemTypes)
        {
            var definition = new SlotDefinition(slot, stableName, allowedItemTypes);
            DefinitionsBySlot.Add(slot, definition);
            SlotsByStableName.Add(stableName, slot);
            editableSlots.Add(slot);
            categorySlots.Add(slot);
        }

        private static void AddSlot(
            List<EquipmentIndex> editableSlots,
            List<EquipmentIndex> categorySlots,
            EquipmentIndex slot,
            string stableName,
            ItemObject.ItemTypeEnum allowedItemType)
        {
            AddSlot(
                editableSlots,
                categorySlots,
                slot,
                stableName,
                new HashSet<ItemObject.ItemTypeEnum> { allowedItemType });
        }
    }
}
