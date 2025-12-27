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
            _defaultPresets = defaultPresets;
            _overrides = overrides;
        }

        public bool TryGetDefaultPreset(GearRole role, int tier, out GearPreset preset)
        {
            return _defaultPresets.TryGetValue((role, tier), out preset);
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

            return _overrides != null ? _overrides.GetEffectiveCost(role, tier, p.Cost) : p.Cost;
        }

        public bool SetTierCostVar(GearRole role, int tier, string varName)
        {
            GearPreset preset;
            if (_defaultPresets.TryGetValue((role, tier), out preset))
            {
                int cost = (_overrides != null) ? _overrides.GetEffectiveCost(role, tier, preset.Cost) : preset.Cost;
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

            int cost = (_overrides != null) ? _overrides.GetEffectiveCost(role, tier, preset.Cost) : preset.Cost;
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
            return (_overrides != null)
                ? _overrides.CaptureSnapshot(role, tier, defaultPreset)
                : new GearPresetSnapshot(defaultPreset.Cost, new Dictionary<EquipmentIndex, string>(defaultPreset.Slots));
        }

        private bool TryBuildEquipmentAndMoveOldItemsToInventory(Hero target, GearPresetSnapshot preset, out Equipment equipment, out string error)
        {
            error = null;
            equipment = target.BattleEquipment.Clone();

            MobileParty mainParty = MobileParty.MainParty;
            ItemRoster playerRoster = (mainParty != null) ? mainParty.ItemRoster : null;

            foreach (var kv in preset.Slots)
            {
                EquipmentIndex slot = kv.Key;
                string itemId = kv.Value;

                if (playerRoster != null)
                {
                    EquipmentElement oldElement = target.BattleEquipment.GetEquipmentFromSlot(slot);

                    if (!oldElement.IsEmpty && !oldElement.IsQuestItem && !oldElement.IsInvalid())
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

                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                if (item == null)
                {
                    error = $"[CGU] Item not found: '{itemId}'. Check the ID (vanilla/War Sails/mods).";
                    equipment = null;
                    return false;
                }

                equipment.AddEquipmentToSlotWithoutAgent(slot, new EquipmentElement(item));
            }

            return true;
        }
    }
}
