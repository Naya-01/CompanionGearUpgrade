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
            if (target == null || !(target.IsPlayerCompanion || target.Clan == Clan.PlayerClan))
                return;

            if (!IsRoleAvailableForApplication(roleId))
            {
                InformationManager.DisplayMessage(new InformationMessage("[CGU] Missing preset."));
                return;
            }

            GearPreset preset = GetDefaultPresetOrNull(roleId, tier);
            if (preset == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[CGU] Missing preset."));
                return;
            }

            int cost = _overrides.GetEffectiveCost(roleId, tier, preset.Cost);
            if (Hero.MainHero.Gold < cost)
            {
                InformationManager.DisplayMessage(new InformationMessage("Not enough gold."));
                return;
            }

            GearPresetSnapshot eff = BuildEffectiveSnapshot(roleId, tier, preset);

            Equipment newEquipment;
            string error;
            if (!TryBuildEquipmentAndMoveOldItemsToInventory(target, eff, out newEquipment, out error))
            {
                InformationManager.DisplayMessage(new InformationMessage(error));
                return;
            }

            Hero.MainHero.ChangeHeroGold(-cost);
            EquipmentHelper.AssignHeroEquipmentFromEquipment(target, newEquipment);

            InformationManager.DisplayMessage(new InformationMessage($"{target.Name}: equipment updated (Tier {tier}) for {cost} gold."));
        }

        public GearPresetSnapshot BuildEffectiveSnapshot(GearRole role, int tier, GearPreset defaultPreset)
        {
            return BuildEffectiveSnapshot(GearPresetRepository.GetRoleId(role), tier, defaultPreset);
        }

        public GearPresetSnapshot BuildEffectiveSnapshot(string roleId, int tier, GearPreset defaultPreset)
        {
            return _overrides.CaptureSnapshot(roleId, tier, defaultPreset);
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
        private bool IsRoleAvailableForApplication(string roleId)
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
}
