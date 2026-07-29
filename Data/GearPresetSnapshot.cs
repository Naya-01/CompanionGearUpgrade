using CompanionGearUpgrades.Domain;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace CompanionGearUpgrades.Data
{
    public sealed class GearPresetSnapshot
    {
        public GearPresetSnapshot(int cost, Dictionary<EquipmentIndex, string> slots)
        {
            Cost = cost;
            Slots = slots != null
                ? new Dictionary<EquipmentIndex, string>(slots)
                : new Dictionary<EquipmentIndex, string>();
        }

        public int Cost { get; private set; }
        public Dictionary<EquipmentIndex, string> Slots { get; private set; }

        public GearPresetSnapshot Clone()
        {
            return new GearPresetSnapshot(Cost, Slots);
        }

        public GearPresetSnapshot WithCost(int cost)
        {
            return new GearPresetSnapshot(cost, Slots);
        }

        public bool HasSameContent(GearPresetSnapshot other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (other == null || Cost != other.Cost)
                return false;

            foreach (EquipmentIndex slot in GearSlotCatalog.EditableSlots)
            {
                string leftId;
                string rightId;
                bool hasLeft = Slots.TryGetValue(slot, out leftId);
                bool hasRight = other.Slots.TryGetValue(slot, out rightId);
                if (hasLeft != hasRight || !string.Equals(leftId, rightId, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }
    }
}
