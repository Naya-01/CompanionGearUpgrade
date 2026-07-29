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
        private const string EmptySlotMarker = "__CGU_EMPTY_SLOT__";

        private readonly Dictionary<string, int> _costOverrides;
        private readonly Dictionary<string, string> _itemOverrides;

        public GearPresetOverrides(Dictionary<string, int> costOverrides, Dictionary<string, string> itemOverrides)
        {
            _costOverrides = costOverrides ?? new Dictionary<string, int>();
            _itemOverrides = itemOverrides ?? new Dictionary<string, string>();
        }

        public int GetEffectiveCost(string roleId, int tier, int defaultCost)
        {
            if (!IsSupportedRoleId(roleId) || !GearPresetRepository.IsValidTier(tier))
                return defaultCost;

            int value;
            return _costOverrides.TryGetValue(CostKey(roleId, tier), out value) ? value : defaultCost;
        }

        // The enum overload keeps the fixed conversation flow and existing
        // callers source-compatible. Its keys stay exactly the same as before
        // (for example, "Infantry:1:cost").
        public int GetEffectiveCost(GearRole role, int tier, int defaultCost)
        {
            return GetEffectiveCost(GearPresetRepository.GetRoleId(role), tier, defaultCost);
        }

        private bool TryGetOverrideItemId(string roleId, int tier, EquipmentIndex slot, out string itemId)
        {
            itemId = null;
            string storedId;
            if (!_itemOverrides.TryGetValue(ItemKey(roleId, tier, slot), out storedId))
                return false;

            itemId = IsEmptyMarker(storedId) ? null : storedId;
            return true;
        }

        private void SetCostOverride(string roleId, int tier, int cost)
        {
            _costOverrides[CostKey(roleId, tier)] = GearPresetPricePolicy.NormalizeForStorage(cost);
        }

        private void ClearCostOverride(string roleId, int tier)
        {
            _costOverrides.Remove(CostKey(roleId, tier));
        }

        private void SetItemOverride(string roleId, int tier, EquipmentIndex slot, string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                SetSlotEmpty(roleId, tier, slot);
                return;
            }

            _itemOverrides[ItemKey(roleId, tier, slot)] = itemId;
        }

        private void SetSlotEmpty(string roleId, int tier, EquipmentIndex slot)
        {
            _itemOverrides[ItemKey(roleId, tier, slot)] = EmptySlotMarker;
        }

        private void ClearItemOverride(string roleId, int tier, EquipmentIndex slot)
        {
            _itemOverrides.Remove(ItemKey(roleId, tier, slot));
        }

        public GearPresetSnapshot CaptureSnapshot(string roleId, int tier, GearPreset defaultPreset)
        {
            EnsureValidRoleAndTier(roleId, tier);
            if (defaultPreset == null)
                throw new ArgumentNullException(nameof(defaultPreset));

            var merged = new Dictionary<EquipmentIndex, string>(defaultPreset.Slots);
            foreach (EquipmentIndex slot in GearSlotCatalog.EditableSlots)
            {
                string id;
                if (TryGetOverrideItemId(roleId, tier, slot, out id))
                    merged[slot] = id;
            }

            int cost = GetEffectiveCost(roleId, tier, defaultPreset.Cost);
            return new GearPresetSnapshot(cost, merged);
        }

        public GearPresetSnapshot CaptureSnapshot(GearRole role, int tier, GearPreset defaultPreset)
        {
            return CaptureSnapshot(GearPresetRepository.GetRoleId(role), tier, defaultPreset);
        }

        public void CommitSnapshot(string roleId, int tier, GearPreset defaultPreset, GearPresetSnapshot snapshot)
        {
            EnsureValidRoleAndTier(roleId, tier);
            if (defaultPreset == null)
                throw new ArgumentNullException(nameof(defaultPreset));
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            // Cost override: only store if different from default.
            if (snapshot.Cost != defaultPreset.Cost)
                SetCostOverride(roleId, tier, snapshot.Cost);
            else
                ClearCostOverride(roleId, tier);

            // Item overrides: only store diffs from default. An empty value is
            // persisted explicitly; clearing the dictionary entry would make
            // the default item silently come back on the next upgrade.
            foreach (EquipmentIndex slot in GearSlotCatalog.EditableSlots)
            {
                string defaultId;
                defaultPreset.Slots.TryGetValue(slot, out defaultId);
                string newId;

                // A slot absent from a snapshot was not edited. This matters
                // for slots which are absent from the repository default.
                if (!snapshot.Slots.TryGetValue(slot, out newId))
                    continue;

                if (string.IsNullOrEmpty(newId))
                {
                    SetSlotEmpty(roleId, tier, slot);
                    continue;
                }

                if (!string.Equals(defaultId, newId, StringComparison.Ordinal))
                    SetItemOverride(roleId, tier, slot, newId);
                else
                    ClearItemOverride(roleId, tier, slot);
            }
        }

        public void CommitSnapshot(GearRole role, int tier, GearPreset defaultPreset, GearPresetSnapshot snapshot)
        {
            CommitSnapshot(GearPresetRepository.GetRoleId(role), tier, defaultPreset, snapshot);
        }

        /// <summary>
        /// Removes every persisted cost and item override for a deleted custom
        /// role. Built-in role keys are never eligible for removal here.
        /// </summary>
        public void RemoveRoleOverrides(string roleId)
        {
            if (!GearPresetRepository.IsCustomRoleId(roleId))
                return;

            string prefix = roleId + ":";
            RemoveKeysWithPrefix(_costOverrides, prefix);
            RemoveKeysWithPrefix(_itemOverrides, prefix);
        }

        /// <summary>
        /// Used when a staged role list is committed. This also cleans up a
        /// newly-created draft role that had tier changes saved before it was
        /// deleted again, while leaving every default-role override untouched.
        /// </summary>
        public void RemoveCustomRoleOverridesExcept(IEnumerable<string> retainedRoleIds)
        {
            var retained = new HashSet<string>(retainedRoleIds ?? new string[0], StringComparer.Ordinal);
            var removedRoleIds = new HashSet<string>(StringComparer.Ordinal);
            CollectDeletedCustomRoleIds(_costOverrides.Keys, retained, removedRoleIds);
            CollectDeletedCustomRoleIds(_itemOverrides.Keys, retained, removedRoleIds);

            foreach (string roleId in removedRoleIds)
                RemoveRoleOverrides(roleId);
        }

        private static void RemoveKeysWithPrefix<T>(Dictionary<string, T> dictionary, string prefix)
        {
            var keysToRemove = new List<string>();
            foreach (string key in dictionary.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    keysToRemove.Add(key);
            }

            foreach (string key in keysToRemove)
                dictionary.Remove(key);
        }

        private static void CollectDeletedCustomRoleIds(
            ICollection<string> keys,
            HashSet<string> retainedRoleIds,
            HashSet<string> removedRoleIds)
        {
            foreach (string key in keys)
            {
                string roleId;
                if (TryGetCustomRoleIdFromKey(key, out roleId) && !retainedRoleIds.Contains(roleId))
                    removedRoleIds.Add(roleId);
            }
        }

        private static bool TryGetCustomRoleIdFromKey(string key, out string roleId)
        {
            roleId = null;
            if (string.IsNullOrEmpty(key))
                return false;

            int separatorIndex = key.IndexOf(':');
            if (separatorIndex <= 0)
                return false;

            string candidate = key.Substring(0, separatorIndex);
            if (!GearPresetRepository.IsCustomRoleId(candidate))
                return false;

            roleId = candidate;
            return true;
        }

        private static bool IsSupportedRoleId(string roleId)
        {
            return GearPresetRepository.IsDefaultRoleId(roleId) ||
                GearPresetRepository.IsCustomRoleId(roleId);
        }

        private static void EnsureValidRoleAndTier(string roleId, int tier)
        {
            if (!IsSupportedRoleId(roleId))
                throw new ArgumentException("Unknown role identifier.", nameof(roleId));
            if (!GearPresetRepository.IsValidTier(tier))
                throw new ArgumentOutOfRangeException(nameof(tier));
        }

        private static string CostKey(string roleId, int tier) => $"{roleId}:{tier}:cost";
        private static string ItemKey(string roleId, int tier, EquipmentIndex slot) => $"{roleId}:{tier}:{(int)slot}";

        private static bool IsEmptyMarker(string value)
        {
            return string.IsNullOrEmpty(value) || string.Equals(value, EmptySlotMarker, StringComparison.Ordinal);
        }
    }

}
