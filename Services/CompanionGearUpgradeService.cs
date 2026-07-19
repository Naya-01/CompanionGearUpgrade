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

        public CompanionGearUpgradeService(
            Dictionary<(GearRole role, int tier), GearPreset> defaultPresets,
            GearPresetOverrides overrides)
        {
            _defaultPresets = defaultPresets ?? throw new ArgumentNullException(nameof(defaultPresets));
            _overrides = overrides ?? throw new ArgumentNullException(nameof(overrides));
        }

        public GearPreset GetDefaultPresetOrNull(GearRole role, int tier)
        {
            GearPreset p;
            return _defaultPresets.TryGetValue((role, tier), out p) ? p : null;
        }

        public int GetEffectiveCost(GearRole role, int tier)
        {
            GearPreset p;
            if (!_defaultPresets.TryGetValue((role, tier), out p))
                return 0;

            return _overrides.GetEffectiveCost(role, tier, p.Cost);
        }

        public bool SetTierCostVar(GearRole role, int tier, string varName)
        {
            GearPreset preset;
            if (_defaultPresets.TryGetValue((role, tier), out preset))
            {
                int cost = _overrides.GetEffectiveCost(role, tier, preset.Cost);
                MBTextManager.SetTextVariable(varName, cost);
                return true;
            }

            MBTextManager.SetTextVariable(varName, "-");
            return false;
        }

        public void TryApplyTier(GearRole role, int tier)
        {
            Hero target = Hero.OneToOneConversationHero;
            if (target == null || !(target.IsPlayerCompanion || target.Clan == Clan.PlayerClan))
                return;

            GearPreset preset;
            if (!_defaultPresets.TryGetValue((role, tier), out preset))
            {
                InformationManager.DisplayMessage(new InformationMessage("[CGU] Missing preset."));
                return;
            }

            int cost = _overrides.GetEffectiveCost(role, tier, preset.Cost);
            if (Hero.MainHero.Gold < cost)
            {
                InformationManager.DisplayMessage(new InformationMessage("Not enough gold."));
                return;
            }

            GearPresetSnapshot eff = BuildEffectiveSnapshot(role, tier, preset);

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
            return _overrides.CaptureSnapshot(role, tier, defaultPreset);
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
