using CompanionGearUpgrades.Domain;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace CompanionGearUpgrades.Data
{
    /// <summary>
    /// Stores user overrides in savegame-friendly structures (Dictionary&lt;string,int&gt; / Dictionary&lt;string,string&gt;).
    /// Keys are stable strings (role+tier and role+tier+slot).
    /// </summary>
    public sealed class GearPresetOverrides
    {
        // A persisted marker is required because removing an item must be
        // distinguishable from having no override (which means "use default").
        public const string EmptySlotMarker = "__CGU_EMPTY_SLOT__";

        public static readonly EquipmentIndex[] EditableSlots =
        {
            EquipmentIndex.Weapon0,
            EquipmentIndex.Weapon1,
            EquipmentIndex.Weapon2,
            EquipmentIndex.Weapon3,
            EquipmentIndex.Head,
            EquipmentIndex.Body,
            EquipmentIndex.Cape,
            EquipmentIndex.Gloves,
            EquipmentIndex.Leg,
            EquipmentIndex.Horse,
            EquipmentIndex.HorseHarness
        };

        private Dictionary<string, int> _costOverrides;
        private Dictionary<string, string> _itemOverrides;

        public GearPresetOverrides(Dictionary<string, int> costOverrides, Dictionary<string, string> itemOverrides)
        {
            _costOverrides = costOverrides ?? new Dictionary<string, int>();
            _itemOverrides = itemOverrides ?? new Dictionary<string, string>();
        }

        public int GetEffectiveCost(GearRole role, int tier, int defaultCost)
        {
            int v;
            return _costOverrides != null && _costOverrides.TryGetValue(CostKey(role, tier), out v) ? v : defaultCost;
        }

        public bool TryGetOverrideItemId(GearRole role, int tier, EquipmentIndex slot, out string itemId)
        {
            itemId = null;
            string storedId;
            if (_itemOverrides == null || !_itemOverrides.TryGetValue(ItemKey(role, tier, slot), out storedId))
                return false;

            itemId = IsEmptyMarker(storedId) ? null : storedId;
            return true;
        }

        public void SetCostOverride(GearRole role, int tier, int cost)
        {
            if (_costOverrides == null)
                _costOverrides = new Dictionary<string, int>();
            _costOverrides[CostKey(role, tier)] = Math.Max(0, cost);
        }

        public void ClearCostOverride(GearRole role, int tier)
        {
            if (_costOverrides == null)
                return;

            _costOverrides.Remove(CostKey(role, tier));
        }

        public void SetItemOverride(GearRole role, int tier, EquipmentIndex slot, string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                SetSlotEmpty(role, tier, slot);
                return;
            }

            if (_itemOverrides == null)
                _itemOverrides = new Dictionary<string, string>();

            _itemOverrides[ItemKey(role, tier, slot)] = itemId;
        }

        public void SetSlotEmpty(GearRole role, int tier, EquipmentIndex slot)
        {
            if (_itemOverrides == null)
                _itemOverrides = new Dictionary<string, string>();

            _itemOverrides[ItemKey(role, tier, slot)] = EmptySlotMarker;
        }

        public void ClearItemOverride(GearRole role, int tier, EquipmentIndex slot)
        {
            if (_itemOverrides == null)
                return;

            _itemOverrides.Remove(ItemKey(role, tier, slot));
        }

        public GearPresetSnapshot CaptureSnapshot(GearRole role, int tier, GearPreset defaultPreset)
        {
            if (defaultPreset == null)
                throw new ArgumentNullException(nameof(defaultPreset));

            var merged = new Dictionary<EquipmentIndex, string>(defaultPreset.Slots);
            foreach (EquipmentIndex slot in EditableSlots)
            {
                string id;
                if (TryGetOverrideItemId(role, tier, slot, out id))
                    merged[slot] = id;
            }

            int cost = GetEffectiveCost(role, tier, defaultPreset.Cost);
            return new GearPresetSnapshot(cost, merged);
        }

        public void CommitSnapshot(GearRole role, int tier, GearPreset defaultPreset, GearPresetSnapshot snapshot)
        {
            if (defaultPreset == null)
                throw new ArgumentNullException(nameof(defaultPreset));
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            // Cost override: only store if different from default.
            if (snapshot.Cost != defaultPreset.Cost)
                SetCostOverride(role, tier, snapshot.Cost);
            else
                ClearCostOverride(role, tier);

            // Item overrides: only store diffs from default. An empty value is
            // persisted explicitly; clearing the dictionary entry would make
            // the default item silently come back on the next upgrade.
            foreach (EquipmentIndex slot in EditableSlots)
            {
                string defaultId;
                bool hasDefault = defaultPreset.Slots.TryGetValue(slot, out defaultId);
                string newId;

                // A slot absent from a snapshot was not edited. This matters
                // for slots which are absent from the repository default.
                if (!snapshot.Slots.TryGetValue(slot, out newId))
                    continue;

                if (string.IsNullOrEmpty(newId))
                {
                    SetSlotEmpty(role, tier, slot);
                    continue;
                }

                if (!string.Equals(defaultId, newId, StringComparison.Ordinal))
                    SetItemOverride(role, tier, slot, newId);
                else
                    ClearItemOverride(role, tier, slot);
            }
        }

        public static string CostKey(GearRole role, int tier) => $"{role}:{tier}:cost";
        public static string ItemKey(GearRole role, int tier, EquipmentIndex slot) => $"{role}:{tier}:{(int)slot}";

        private static bool IsEmptyMarker(string value)
        {
            return string.IsNullOrEmpty(value) || string.Equals(value, EmptySlotMarker, StringComparison.Ordinal);
        }
    }

    public sealed class GearPresetSnapshot
    {
        public int Cost { get; private set; }
        public Dictionary<EquipmentIndex, string> Slots { get; private set; }

        public GearPresetSnapshot(int cost, Dictionary<EquipmentIndex, string> slots)
        {
            Cost = cost;
            Slots = slots != null
                ? new Dictionary<EquipmentIndex, string>(slots)
                : new Dictionary<EquipmentIndex, string>();
        }

        public GearPresetSnapshot Clone()
        {
            return new GearPresetSnapshot(Cost, Slots);
        }
    }
}
