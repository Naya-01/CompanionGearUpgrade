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
            return _itemOverrides != null && _itemOverrides.TryGetValue(ItemKey(role, tier, slot), out itemId);
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
                throw new ArgumentException("itemId must be a non-empty string", nameof(itemId));

            if (_itemOverrides == null)
                _itemOverrides = new Dictionary<string, string>();

            _itemOverrides[ItemKey(role, tier, slot)] = itemId;
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
            foreach (var kv in defaultPreset.Slots)
            {
                string id;
                if (TryGetOverrideItemId(role, tier, kv.Key, out id) && !string.IsNullOrEmpty(id))
                    merged[kv.Key] = id;
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

            // Item overrides: only store diffs from default.
            foreach (var kv in snapshot.Slots)
            {
                string defaultId;
                bool hasDefault = defaultPreset.Slots.TryGetValue(kv.Key, out defaultId);
                string newId = kv.Value;

                if (!hasDefault)
                {
                    // New slot introduced by user: store it.
                    SetItemOverride(role, tier, kv.Key, newId);
                    continue;
                }

                if (!string.Equals(defaultId, newId, StringComparison.Ordinal))
                    SetItemOverride(role, tier, kv.Key, newId);
                else
                    ClearItemOverride(role, tier, kv.Key);
            }
        }

        public static string CostKey(GearRole role, int tier) => $"{role}:{tier}:cost";
        public static string ItemKey(GearRole role, int tier, EquipmentIndex slot) => $"{role}:{tier}:{(int)slot}";
    }

    public sealed class GearPresetSnapshot
    {
        public int Cost { get; private set; }
        public Dictionary<EquipmentIndex, string> Slots { get; private set; }

        public GearPresetSnapshot(int cost, Dictionary<EquipmentIndex, string> slots)
        {
            Cost = cost;
            Slots = slots ?? new Dictionary<EquipmentIndex, string>();
        }
    }
}
