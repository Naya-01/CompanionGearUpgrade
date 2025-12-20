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
        private readonly Dictionary<(GearRole role, int tier), GearPreset> _presets;

        public CompanionGearUpgradeService(Dictionary<(GearRole role, int tier), GearPreset> presets)
        {
            _presets = presets;
        }

        public bool TryGetPreset(GearRole role, int tier, out GearPreset preset)
        {
            return _presets.TryGetValue((role, tier), out preset);
        }

        public bool SetTierCostVar(GearRole role, int tier, string varName)
        {
            GearPreset preset;
            if (_presets.TryGetValue((role, tier), out preset))
            {
                MBTextManager.SetTextVariable(varName, preset.Cost);
                return true;
            }

            MBTextManager.SetTextVariable(varName, "-");
            return false;
        }

        public void TryApplyTier(GearRole role, int tier)
        {
            Hero target = Hero.OneToOneConversationHero;
            if (target == null || !target.IsPlayerCompanion)
                return;

            GearPreset preset;
            if (!_presets.TryGetValue((role, tier), out preset))
            {
                InformationManager.DisplayMessage(new InformationMessage("[CGU] Missing preset."));
                return;
            }

            int cost = preset.Cost;
            if (Hero.MainHero.Gold < cost)
            {
                InformationManager.DisplayMessage(new InformationMessage("Not enough gold."));
                return;
            }

            Equipment newEquipment;
            string error;
            if (!TryBuildEquipmentAndMoveOldItemsToInventory(target, preset, out newEquipment, out error))
            {
                InformationManager.DisplayMessage(new InformationMessage(error));
                return;
            }

            Hero.MainHero.ChangeHeroGold(-cost);
            EquipmentHelper.AssignHeroEquipmentFromEquipment(target, newEquipment);

            InformationManager.DisplayMessage(new InformationMessage($"{target.Name}: equipment updated (Tier {tier}) for {cost} gold."));
        }

        private bool TryBuildEquipmentAndMoveOldItemsToInventory(Hero target, GearPreset preset, out Equipment equipment, out string error)
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
