using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Domain;
using Helpers;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace CompanionGearUpgrades.Services
{
    public sealed class CompanionGearUpgradeService
    {
        private readonly Dictionary<(GearRole role, int tier), GearPreset> _defaultPresets;
        private readonly GearPresetOverrides _overrides;
        private readonly Dictionary<string, string> _customRoleNames;

        public CompanionGearUpgradeService(
            Dictionary<(GearRole role, int tier), GearPreset> defaultPresets,
            GearPresetOverrides overrides)
            : this(defaultPresets, overrides, new Dictionary<string, string>())
        {
        }

        public CompanionGearUpgradeService(
            Dictionary<(GearRole role, int tier), GearPreset> defaultPresets,
            GearPresetOverrides overrides,
            Dictionary<string, string> customRoleNames)
        {
            _defaultPresets = defaultPresets ?? throw new ArgumentNullException(nameof(defaultPresets));
            _overrides = overrides ?? throw new ArgumentNullException(nameof(overrides));
            _customRoleNames = customRoleNames ?? new Dictionary<string, string>();
            GearPresetRepository.NormalizeCustomRoles(_customRoleNames);
        }

        /// <summary>
        /// A copy of the persisted catalog. The view model may freely stage
        /// additions/deletions in its own collection until Save commits them.
        /// </summary>
        public IReadOnlyList<GearRoleDefinition> GetRoleDefinitions()
        {
            return GearPresetRepository.GetRoles(_customRoleNames).AsReadOnly();
        }

        public IReadOnlyList<GearRoleDefinition> GetCustomRoles()
        {
            return GearPresetRepository.GetCustomRoles(_customRoleNames).AsReadOnly();
        }

        /// <summary>
        /// Creates a non-persisted role definition for the UI's staged list.
        /// The definition becomes campaign data only through
        /// <see cref="TryCommitCustomRoles"/>.
        /// </summary>
        public bool TryCreateCustomRoleDraft(
            string name,
            IEnumerable<GearRoleDefinition> currentRoles,
            out GearRoleDefinition role,
            out string error)
        {
            role = null;

            string normalizedName;
            if (!GearPresetRepository.TryNormalizeCustomRoleName(name, out normalizedName, out error))
                return false;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GearRoleDefinition defaultRole in GearPresetRepository.GetDefaultRoles())
                names.Add(defaultRole.Name);

            int customRoleCount = 0;
            IEnumerable<GearRoleDefinition> rolesToInspect = currentRoles ?? GetRoleDefinitions();
            foreach (GearRoleDefinition existingRole in rolesToInspect)
            {
                if (existingRole == null)
                    continue;

                if (!string.IsNullOrEmpty(existingRole.Id))
                    ids.Add(existingRole.Id);
                if (!string.IsNullOrWhiteSpace(existingRole.Name))
                    names.Add(existingRole.Name.Trim());
                if (!existingRole.IsDefaultRole)
                    customRoleCount++;
            }

            if (!names.Add(normalizedName))
            {
                error = "Role names must be unique.";
                return false;
            }

            if (customRoleCount >= GearPresetRepository.MaxRoleCount - GearPresetRepository.DefaultRoleCount)
            {
                error = $"You can create at most {GearPresetRepository.MaxRoleCount} roles.";
                return false;
            }

            string id;
            do
            {
                id = GearPresetRepository.CustomRoleIdPrefix + Guid.NewGuid().ToString("N");
            }
            while (ids.Contains(id));

            role = new GearRoleDefinition(id, normalizedName, false);
            error = null;
            return true;
        }

        /// <summary>
        /// Atomically replaces the persisted custom-role list after validating
        /// it. Overrides belonging to a deleted custom role are purged in the
        /// same operation; default-role overrides are never touched.
        /// </summary>
        public bool TryCommitCustomRoles(IEnumerable<GearRoleDefinition> customRoles, out string error)
        {
            List<GearRoleDefinition> validatedRoles;
            if (!GearPresetRepository.TryValidateCustomRoles(customRoles, out validatedRoles, out error))
                return false;

            var retainedRoleIds = new List<string>();
            foreach (GearRoleDefinition role in validatedRoles)
                retainedRoleIds.Add(role.Id);

            _overrides.RemoveCustomRoleOverridesExcept(retainedRoleIds);

            _customRoleNames.Clear();
            foreach (GearRoleDefinition role in validatedRoles)
                _customRoleNames.Add(role.Id, role.Name);

            error = null;
            return true;
        }

        public GearPreset GetDefaultPresetOrNull(GearRole role, int tier)
        {
            GearPreset p;
            return _defaultPresets.TryGetValue((role, tier), out p) ? p : null;
        }

        /// <summary>
        /// Custom roles intentionally use the Infantry defaults as their
        /// editable baseline. They therefore always expose exactly tiers 1-3,
        /// including while a newly-added role still exists only in the UI
        /// snapshot.
        /// </summary>
        public GearPreset GetDefaultPresetOrNull(string roleId, int tier)
        {
            GearRole templateRole;
            if (!TryResolveTemplateRole(roleId, out templateRole) || !GearPresetRepository.IsValidTier(tier))
                return null;

            return GetDefaultPresetOrNull(templateRole, tier);
        }

        public int GetEffectiveCost(GearRole role, int tier)
        {
            return GetEffectiveCost(GearPresetRepository.GetRoleId(role), tier);
        }

        public int GetEffectiveCost(string roleId, int tier)
        {
            GearPreset preset = GetDefaultPresetOrNull(roleId, tier);
            if (preset == null)
                return 0;

            return _overrides.GetEffectiveCost(roleId, tier, preset.Cost);
        }

        public bool SetTierCostVar(GearRole role, int tier, string varName)
        {
            return SetTierCostVar(GearPresetRepository.GetRoleId(role), tier, varName);
        }

        public bool SetTierCostVar(string roleId, int tier, string varName)
        {
            if (!IsRoleAvailableForApplication(roleId))
            {
                MBTextManager.SetTextVariable(varName, "-");
                return false;
            }

            GearPreset preset = GetDefaultPresetOrNull(roleId, tier);
            if (preset != null)
            {
                int cost = _overrides.GetEffectiveCost(roleId, tier, preset.Cost);
                MBTextManager.SetTextVariable(varName, cost);
                return true;
            }

            MBTextManager.SetTextVariable(varName, "-");
            return false;
        }

        public void TryApplyTier(GearRole role, int tier)
        {
            TryApplyTier(GearPresetRepository.GetRoleId(role), tier);
        }

        public void TryApplyTier(string roleId, int tier)
        {
            Hero target = Hero.OneToOneConversationHero;
            // Preserve the historical conversation behavior: there is simply
            // no action if the conversation target is no longer eligible.
            if (!IsHeroEligibleForPresetApplication(target))
                return;

            GearPresetApplicationResult result = TryApplyTierToHero(target, roleId, tier);
            if (!string.IsNullOrEmpty(result.Message))
                InformationManager.DisplayMessage(new InformationMessage(result.Message));
        }

        /// <summary>
        /// Returns whether a hero can receive a gear preset. This is shared by
        /// the conversation and the Clan Gauntlet entry point so they cannot
        /// drift apart over time.
        /// </summary>
        public bool IsHeroEligibleForPresetApplication(Hero target)
        {
            return target != null &&
                (target.IsPlayerCompanion || target.Clan == Clan.PlayerClan);
        }

        /// <summary>
        /// Kept in the service so callers do not need to recreate the payer
        /// rule used by <see cref="TryApplyTierToHero"/>.
        /// </summary>
        public bool CanPlayerAffordPreset(int cost)
        {
            return cost >= 0 && Hero.MainHero != null && Hero.MainHero.Gold >= cost;
        }

        /// <summary>
        /// Returns the persisted snapshot and its current price for a role and
        /// tier. The returned snapshot is a copy and is safe for UI preview.
        /// </summary>
        public bool TryGetPresetForApplication(
            string roleId,
            int tier,
            out GearPresetSnapshot snapshot,
            out int cost,
            out string error)
        {
            snapshot = null;
            cost = 0;
            error = null;

            if (!IsRoleAvailableForApplication(roleId))
            {
                error = "[CGU] Missing preset.";
                return false;
            }

            GearPreset preset = GetDefaultPresetOrNull(roleId, tier);
            if (preset == null)
            {
                error = "[CGU] Missing preset.";
                return false;
            }

            cost = _overrides.GetEffectiveCost(roleId, tier, preset.Cost);
            snapshot = BuildEffectiveSnapshot(roleId, tier, preset);
            return true;
        }

        /// <summary>
        /// Applies a preset to an explicit hero. All domain work remains here:
        /// eligibility, role validation, payment, old-item transfer and
        /// equipment assignment. UI callers only choose a role and tier.
        /// </summary>
        public GearPresetApplicationResult TryApplyTierToHero(Hero target, string roleId, int tier)
        {
            if (!IsHeroEligibleForPresetApplication(target))
                return GearPresetApplicationResult.Failed("[CGU] This companion cannot receive a gear preset.");

            GearPresetSnapshot snapshot;
            int cost;
            string error;
            if (!TryGetPresetForApplication(roleId, tier, out snapshot, out cost, out error))
                return GearPresetApplicationResult.Failed(error);

            Hero payer = Hero.MainHero;
            if (payer == null)
                return GearPresetApplicationResult.Failed(
                    "[CGU] No player hero is available to pay for this preset.");

            if (!CanPlayerAffordPreset(cost))
                return GearPresetApplicationResult.Failed("Not enough gold.");

            Equipment newEquipment;
            if (!TryBuildEquipmentAndMoveOldItemsToInventory(target, snapshot, out newEquipment, out error))
                return GearPresetApplicationResult.Failed(error);

            payer.ChangeHeroGold(-cost);
            EquipmentHelper.AssignHeroEquipmentFromEquipment(target, newEquipment);

            return GearPresetApplicationResult.Succeeded(
                $"{target.Name}: equipment updated (Tier {tier}) for {cost} gold.");
        }

        public GearPresetSnapshot BuildEffectiveSnapshot(GearRole role, int tier, GearPreset defaultPreset)
        {
            return BuildEffectiveSnapshot(GearPresetRepository.GetRoleId(role), tier, defaultPreset);
        }

        public GearPresetSnapshot BuildEffectiveSnapshot(string roleId, int tier, GearPreset defaultPreset)
        {
            return _overrides.CaptureSnapshot(roleId, tier, defaultPreset);
        }

        /// <summary>
        /// Builds a self-contained transfer document from the currently
        /// persisted role catalogue. Every editable slot is present, including
        /// intentionally empty slots, so the resulting JSON does not depend on
        /// either the source campaign or repository defaults.
        /// </summary>
        public GearPresetTransferDocument CreateTransferDocument(IEnumerable<GearRoleDefinition> roles = null)
        {
            var document = new GearPresetTransferDocument
            {
                SchemaVersion = GearPresetTransferDocument.CurrentSchemaVersion,
                ModVersion = GetModVersion(),
                Roles = new List<GearPresetTransferRole>()
            };

            IReadOnlyList<GearRoleDefinition> persistedRoles = GetRoleDefinitions();
            var persistedById = new Dictionary<string, GearRoleDefinition>(StringComparer.Ordinal);
            foreach (GearRoleDefinition persistedRole in persistedRoles)
                persistedById[persistedRole.Id] = persistedRole;

            IEnumerable<GearRoleDefinition> rolesToExport = roles ?? persistedRoles;
            var exportedRoleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GearRoleDefinition requestedRole in rolesToExport)
            {
                if (requestedRole == null || string.IsNullOrEmpty(requestedRole.Id) ||
                    !exportedRoleIds.Add(requestedRole.Id))
                {
                    continue;
                }

                GearRoleDefinition role;
                if (!persistedById.TryGetValue(requestedRole.Id, out role))
                    continue;

                var transferRole = new GearPresetTransferRole
                {
                    Name = role.Name,
                    Tiers = new List<GearPresetTransferTier>()
                };

                for (int tier = 1; tier <= GearPresetRepository.TierCount; tier++)
                {
                    GearPreset defaultPreset = GetDefaultPresetOrNull(role.Id, tier);
                    if (defaultPreset == null)
                        continue;

                    GearPresetSnapshot snapshot = BuildEffectiveSnapshot(role.Id, tier, defaultPreset);
                    var slots = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (EquipmentIndex slot in GearPresetOverrides.EditableSlots)
                    {
                        string itemId;
                        snapshot.Slots.TryGetValue(slot, out itemId);
                        slots[GearPresetTransferSlots.GetSlotName(slot)] = itemId;
                    }

                    transferRole.Tiers.Add(new GearPresetTransferTier
                    {
                        Tier = tier,
                        Price = snapshot.Cost,
                        Slots = slots
                    });
                }

                document.Roles.Add(transferRole);
            }

            return document;
        }

        /// <summary>
        /// Applies configurations already validated and conflict-resolved by
        /// the UI. The map key is the stable destination role identifier; the
        /// supplied custom-role list is the complete catalogue that should
        /// remain in the campaign after the import. All data and item lookups
        /// are prepared before the first campaign-backed collection is changed.
        /// </summary>
        public bool TryApplyImportedConfiguration(
            IEnumerable<GearRoleDefinition> finalCustomRoles,
            IDictionary<string, GearPresetTransferRole> configurationsByTargetRoleId,
            out GearPresetImportResult result,
            out string error)
        {
            result = new GearPresetImportResult();
            error = null;

            if (finalCustomRoles == null)
            {
                error = "The complete target role catalogue is required for import.";
                return false;
            }

            List<GearRoleDefinition> validatedCustomRoles;
            if (!GearPresetRepository.TryValidateCustomRoles(finalCustomRoles, out validatedCustomRoles, out error))
                return false;

            if (configurationsByTargetRoleId == null || configurationsByTargetRoleId.Count == 0)
            {
                error = "There are no role configurations to import.";
                return false;
            }

            var validCustomRoleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GearRoleDefinition customRole in validatedCustomRoles)
                validCustomRoleIds.Add(customRole.Id);

            var preparedConfigurations = new List<PreparedImportedRole>();
            var targetRoleIds = new HashSet<string>(StringComparer.Ordinal);
            var missingItemIds = new HashSet<string>(StringComparer.Ordinal);
            int importedItemCount = 0;
            int missingSlotCount = 0;

            foreach (KeyValuePair<string, GearPresetTransferRole> entry in configurationsByTargetRoleId)
            {
                string targetRoleId;
                if (!TryCanonicalizeImportedTargetRoleId(entry.Key, validCustomRoleIds, out targetRoleId, out error))
                    return false;

                if (!targetRoleIds.Add(targetRoleId))
                {
                    error = "The import contains more than one configuration for the same role.";
                    return false;
                }

                PreparedImportedRole preparedRole;
                int roleImportedItemCount;
                int roleMissingSlotCount;
                if (!TryPrepareImportedRole(
                    targetRoleId,
                    entry.Value,
                    out preparedRole,
                    out roleImportedItemCount,
                    out roleMissingSlotCount,
                    missingItemIds,
                    out error))
                {
                    return false;
                }

                foreach (PreparedImportedTier preparedTier in preparedRole.Tiers)
                {
                    if (GetDefaultPresetOrNull(targetRoleId, preparedTier.Tier) == null)
                    {
                        error = "The import target has no valid preset template.";
                        return false;
                    }
                }

                preparedConfigurations.Add(preparedRole);
                importedItemCount += roleImportedItemCount;
                missingSlotCount += roleMissingSlotCount;
            }

            // No mutation has occurred above this point. The two operations
            // below only use already checked role IDs, tiers and snapshots.
            if (!TryCommitCustomRoles(validatedCustomRoles, out error))
                return false;

            foreach (PreparedImportedRole preparedRole in preparedConfigurations)
            {
                foreach (PreparedImportedTier preparedTier in preparedRole.Tiers)
                {
                    GearPreset defaultPreset = GetDefaultPresetOrNull(preparedRole.TargetRoleId, preparedTier.Tier);
                    _overrides.CommitSnapshot(
                        preparedRole.TargetRoleId,
                        preparedTier.Tier,
                        defaultPreset,
                        preparedTier.Snapshot);
                }
            }

            result.ImportedRoleCount = preparedConfigurations.Count;
            result.ImportedItemCount = importedItemCount;
            result.MissingSlotCount = missingSlotCount;
            result.MissingItemIds = new List<string>(missingItemIds);
            result.ImportedRoleNames = GetImportedRoleNames(preparedConfigurations);
            return true;
        }

        private static string GetModVersion()
        {
            Version version = typeof(CompanionGearUpgradeService).Assembly.GetName().Version;
            return version != null ? version.ToString() : "unknown";
        }

        private static bool TryCanonicalizeImportedTargetRoleId(
            string requestedRoleId,
            HashSet<string> validCustomRoleIds,
            out string targetRoleId,
            out string error)
        {
            targetRoleId = null;
            error = null;
            if (string.IsNullOrWhiteSpace(requestedRoleId))
            {
                error = "An imported role has no destination.";
                return false;
            }

            string trimmedRoleId = requestedRoleId.Trim();
            foreach (GearRoleDefinition defaultRole in GearPresetRepository.GetDefaultRoles())
            {
                if (string.Equals(trimmedRoleId, defaultRole.Id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(trimmedRoleId, defaultRole.Name, StringComparison.OrdinalIgnoreCase))
                {
                    targetRoleId = defaultRole.Id;
                    return true;
                }
            }

            if (validCustomRoleIds.Contains(trimmedRoleId))
            {
                targetRoleId = trimmedRoleId;
                return true;
            }

            error = "An imported role no longer exists in the target catalogue.";
            return false;
        }

        private static bool TryPrepareImportedRole(
            string targetRoleId,
            GearPresetTransferRole transferRole,
            out PreparedImportedRole preparedRole,
            out int importedItemCount,
            out int missingSlotCount,
            HashSet<string> missingItemIds,
            out string error)
        {
            preparedRole = null;
            importedItemCount = 0;
            missingSlotCount = 0;
            error = null;

            if (transferRole == null || transferRole.Tiers == null ||
                transferRole.Tiers.Count != GearPresetRepository.TierCount)
            {
                error = "Each imported role must contain exactly three tiers.";
                return false;
            }

            var tiersByNumber = new Dictionary<int, GearPresetTransferTier>();
            foreach (GearPresetTransferTier transferTier in transferRole.Tiers)
            {
                if (transferTier == null || !GearPresetRepository.IsValidTier(transferTier.Tier) ||
                    transferTier.Price < 0 || transferTier.Slots == null ||
                    transferTier.Slots.Count != GearPresetOverrides.EditableSlots.Length ||
                    tiersByNumber.ContainsKey(transferTier.Tier))
                {
                    error = "An imported tier is invalid.";
                    return false;
                }

                tiersByNumber.Add(transferTier.Tier, transferTier);
            }

            var preparedTiers = new List<PreparedImportedTier>();
            for (int tier = 1; tier <= GearPresetRepository.TierCount; tier++)
            {
                GearPresetTransferTier transferTier;
                if (!tiersByNumber.TryGetValue(tier, out transferTier))
                {
                    error = "Each imported role must contain tiers 1, 2 and 3.";
                    return false;
                }

                var slots = new Dictionary<EquipmentIndex, string>();
                foreach (EquipmentIndex slot in GearPresetOverrides.EditableSlots)
                    slots.Add(slot, null);

                var assignedSlots = new HashSet<EquipmentIndex>();
                foreach (KeyValuePair<string, string> slotEntry in transferTier.Slots)
                {
                    EquipmentIndex slot;
                    if (!GearPresetTransferSlots.TryGetSlot(slotEntry.Key, out slot) ||
                        !assignedSlots.Add(slot))
                    {
                        error = "An imported tier contains an unsupported slot.";
                        return false;
                    }

                    string itemId = slotEntry.Value;
                    if (itemId == null)
                    {
                        slots[slot] = null;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(itemId))
                    {
                        error = "An imported equipment StringId is invalid.";
                        return false;
                    }

                    ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                    if (item == null)
                    {
                        // A third-party item's absence is a warning, not a
                        // failed import. Explicitly persist an empty slot so
                        // no default item is silently substituted later.
                        slots[slot] = null;
                        missingSlotCount++;
                        missingItemIds.Add(itemId);
                        continue;
                    }

                    if (!GetAllowedItemTypesForSlot(slot).Contains(item.ItemType))
                    {
                        error = "An imported item is not compatible with its target slot.";
                        return false;
                    }

                    slots[slot] = itemId;
                    importedItemCount++;
                }

                if (assignedSlots.Count != GearPresetOverrides.EditableSlots.Length)
                {
                    error = "Each imported tier must explicitly include every editable slot.";
                    return false;
                }

                preparedTiers.Add(new PreparedImportedTier(
                    tier,
                    new GearPresetSnapshot(transferTier.Price, slots)));
            }

            preparedRole = new PreparedImportedRole(targetRoleId, preparedTiers);
            return true;
        }

        private IReadOnlyList<string> GetImportedRoleNames(IEnumerable<PreparedImportedRole> preparedRoles)
        {
            var namesById = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (GearRoleDefinition role in GetRoleDefinitions())
                namesById[role.Id] = role.Name;

            var names = new List<string>();
            foreach (PreparedImportedRole preparedRole in preparedRoles)
            {
                string name;
                names.Add(namesById.TryGetValue(preparedRole.TargetRoleId, out name)
                    ? name
                    : preparedRole.TargetRoleId);
            }
            return names;
        }

        private sealed class PreparedImportedRole
        {
            public PreparedImportedRole(string targetRoleId, List<PreparedImportedTier> tiers)
            {
                TargetRoleId = targetRoleId;
                Tiers = tiers;
            }

            public string TargetRoleId { get; private set; }
            public List<PreparedImportedTier> Tiers { get; private set; }
        }

        private sealed class PreparedImportedTier
        {
            public PreparedImportedTier(int tier, GearPresetSnapshot snapshot)
            {
                Tier = tier;
                Snapshot = snapshot;
            }

            public int Tier { get; private set; }
            public GearPresetSnapshot Snapshot { get; private set; }
        }

        private static bool TryResolveTemplateRole(string roleId, out GearRole templateRole)
        {
            if (GearPresetRepository.TryGetDefaultRole(roleId, out templateRole))
                return true;

            // A custom role has no immutable repository entry of its own. It
            // starts from Infantry's three presets and stores every user change
            // under its own stable role ID in GearPresetOverrides.
            if (GearPresetRepository.IsCustomRoleId(roleId))
            {
                templateRole = GearRole.Infantry;
                return true;
            }

            templateRole = default(GearRole);
            return false;
        }

        /// <summary>
        /// Draft roles are valid inside the configurator before Save, but a
        /// conversation must only apply a built-in role or a custom role that
        /// still exists in the persisted catalogue.
        /// </summary>
        public bool IsRoleAvailableForApplication(string roleId)
        {
            return GearPresetRepository.IsDefaultRoleId(roleId) ||
                (GearPresetRepository.IsCustomRoleId(roleId) && _customRoleNames.ContainsKey(roleId));
        }

        public List<ItemObject> GetCompatibleItems(EquipmentIndex slot)
        {
            HashSet<ItemObject.ItemTypeEnum> allowed = GetAllowedItemTypesForSlot(slot);
            var list = new List<ItemObject>();

            foreach (ItemObject item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                if (item == null || string.IsNullOrEmpty(item.StringId) || !allowed.Contains(item.ItemType))
                    continue;

                list.Add(item);
            }

            return list;
        }

        private bool TryBuildEquipmentAndMoveOldItemsToInventory(Hero target, GearPresetSnapshot preset, out Equipment equipment, out string error)
        {
            error = null;
            equipment = target.BattleEquipment.Clone();

            MobileParty mainParty = MobileParty.MainParty;
            ItemRoster playerRoster = (mainParty != null) ? mainParty.ItemRoster : null;
            var resolvedItems = new Dictionary<EquipmentIndex, ItemObject>();

            // Resolve every configured item before changing the roster. A bad
            // StringId must not leave half of an upgrade applied.
            foreach (EquipmentIndex slot in GearPresetOverrides.EditableSlots)
            {
                string itemId;
                if (!preset.Slots.TryGetValue(slot, out itemId) || string.IsNullOrEmpty(itemId))
                    continue;

                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                if (item == null)
                {
                    error = $"[CGU] Item not found: '{itemId}'. Check the ID (vanilla/War Sails/mods).";
                    equipment = null;
                    return false;
                }

                resolvedItems[slot] = item;
            }

            foreach (EquipmentIndex slot in GearPresetOverrides.EditableSlots)
            {
                ItemObject item;
                resolvedItems.TryGetValue(slot, out item);

                if (playerRoster != null)
                {
                    EquipmentElement oldElement = target.BattleEquipment.GetEquipmentFromSlot(slot);
                    string oldId = oldElement.IsEmpty || oldElement.Item == null ? null : oldElement.Item.StringId;
                    string newId = item == null ? null : item.StringId;

                    if (!string.Equals(oldId, newId, StringComparison.Ordinal) &&
                        !oldElement.IsEmpty && !oldElement.IsQuestItem && !oldElement.IsInvalid())
                    {
                        try
                        {
                            playerRoster.AddToCounts(new EquipmentElement(oldElement), 1);
                        }
                        catch (Exception ex)
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                $"[CGU] Unable to transfer the old item from slot {slot}: {ex.Message}"
                            ));
                        }
                    }
                }

                // Always write every editable slot. In particular, a missing
                // or null value explicitly clears the cloned equipment slot.
                equipment.AddEquipmentToSlotWithoutAgent(
                    slot,
                    item != null ? new EquipmentElement(item) : default(EquipmentElement));
            }

            return true;
        }

        private static HashSet<ItemObject.ItemTypeEnum> GetAllowedItemTypesForSlot(EquipmentIndex slot)
        {
            switch (slot)
            {
                case EquipmentIndex.Head:
                    return new HashSet<ItemObject.ItemTypeEnum> { ItemObject.ItemTypeEnum.HeadArmor };
                case EquipmentIndex.Body:
                    return new HashSet<ItemObject.ItemTypeEnum> { ItemObject.ItemTypeEnum.BodyArmor };
                case EquipmentIndex.Cape:
                    return new HashSet<ItemObject.ItemTypeEnum> { ItemObject.ItemTypeEnum.Cape };
                case EquipmentIndex.Gloves:
                    return new HashSet<ItemObject.ItemTypeEnum> { ItemObject.ItemTypeEnum.HandArmor };
                case EquipmentIndex.Leg:
                    return new HashSet<ItemObject.ItemTypeEnum> { ItemObject.ItemTypeEnum.LegArmor };
                case EquipmentIndex.Horse:
                    return new HashSet<ItemObject.ItemTypeEnum> { ItemObject.ItemTypeEnum.Horse };
                case EquipmentIndex.HorseHarness:
                    return new HashSet<ItemObject.ItemTypeEnum> { ItemObject.ItemTypeEnum.HorseHarness };
                default:
                    return new HashSet<ItemObject.ItemTypeEnum>
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
            }
        }
    }

    /// <summary>
    /// Outcome returned by the shared preset-application workflow. It lets a
    /// Gauntlet screen present the same success/error feedback as the legacy
    /// dialogue without reimplementing campaign mutations.
    /// </summary>
    public sealed class GearPresetApplicationResult
    {
        private GearPresetApplicationResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message;
        }

        public bool IsSuccess { get; private set; }
        public string Message { get; private set; }

        internal static GearPresetApplicationResult Failed(string message)
        {
            return new GearPresetApplicationResult(false, message);
        }

        internal static GearPresetApplicationResult Succeeded(string message)
        {
            return new GearPresetApplicationResult(true, message);
        }
    }

    /// <summary>
    /// Details of a completed import. Missing StringIds are kept separate from
    /// invalid JSON: they merely mean their destination slots were persisted
    /// as empty and can be selected manually later.
    /// </summary>
    public sealed class GearPresetImportResult
    {
        public GearPresetImportResult()
        {
            MissingItemIds = new List<string>();
            ImportedRoleNames = new List<string>();
        }

        public int ImportedRoleCount { get; internal set; }
        public int ImportedItemCount { get; internal set; }
        public int MissingSlotCount { get; internal set; }
        public IReadOnlyList<string> MissingItemIds { get; internal set; }
        public IReadOnlyList<string> ImportedRoleNames { get; internal set; }

        public bool HasWarnings => MissingSlotCount > 0;
    }
}
