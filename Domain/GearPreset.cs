using System.Collections.Generic;
using TaleWorlds.Core;

namespace CompanionGearUpgrades.Domain
{
    public sealed class GearPreset
    {
        public int Cost { get; private set; }
        public Dictionary<EquipmentIndex, string> Slots { get; private set; }

        public GearPreset(int cost, Dictionary<EquipmentIndex, string> slots)
        {
            Cost = cost;
            Slots = slots;
        }
    }
}
