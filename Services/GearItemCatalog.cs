using CompanionGearUpgrades.Domain;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace CompanionGearUpgrades.Services
{
    /// <summary>
    /// Null-safe gateway to Bannerlord's global item registry. Callers retain
    /// their own policy for an absent StringId, while IsAvailable lets import
    /// and mutation paths distinguish that case from an uninitialized registry.
    /// </summary>
    internal static class GearItemCatalog
    {
        internal static bool IsAvailable => MBObjectManager.Instance != null;

        internal static ItemObject FindById(string itemId)
        {
            MBObjectManager objectManager = MBObjectManager.Instance;
            if (string.IsNullOrEmpty(itemId) || objectManager == null)
                return null;

            return objectManager.GetObject<ItemObject>(itemId);
        }

        internal static List<ItemObject> GetCompatibleItems(EquipmentIndex slot)
        {
            var items = new List<ItemObject>();
            MBObjectManager objectManager = MBObjectManager.Instance;
            if (objectManager == null)
                return items;

            foreach (ItemObject item in objectManager.GetObjectTypeList<ItemObject>())
            {
                if (item == null ||
                    string.IsNullOrEmpty(item.StringId) ||
                    !GearSlotCatalog.IsItemTypeAllowed(slot, item.ItemType))
                {
                    continue;
                }

                items.Add(item);
            }

            return items;
        }
    }
}
