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
    }
}
