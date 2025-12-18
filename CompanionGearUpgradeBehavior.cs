using Helpers;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace CompanionGearUpgrades
{
    public enum GearRole
    {
        Infantry,
        Archer,
        Lancer
    }

    public sealed class CompanionGearUpgradeBehavior : CampaignBehaviorBase
    {
        private GearRole _selectedRole;

        private readonly Dictionary<(GearRole role, int tier), GearPreset> _presets;

        public CompanionGearUpgradeBehavior()
        {
            _presets = BuildPresets();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddDialogs(starter);
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            // 1) Entrée depuis le menu principal -> on va vers une réplique NPC
            starter.AddPlayerLine(
                "cgu_open",
                "hero_main_options",
                "cgu_role_npc",
                "{=cgu_open}Augmenter votre équipement",
                IsTalkingToPlayerCompanion,
                null,
                100);

            // 2) NPC pose la question -> menu de choix (player options)
            starter.AddDialogLine(
                "cgu_role_npc_line",
                "cgu_role_npc",
                "cgu_role_player",
                "{=cgu_role}Quel style d’équipement ?",
                null,
                null);

            // 3) Choix du rôle -> on passe par un état NPC puis on arrive au menu tier
            starter.AddPlayerLine(
                "cgu_role_infantry",
                "cgu_role_player",
                "cgu_tier_npc",
                "{=cgu_role_infantry}Soldat (infanterie)",
                () => true,
                () => _selectedRole = GearRole.Infantry);

            starter.AddPlayerLine(
                "cgu_role_archer",
                "cgu_role_player",
                "cgu_tier_npc",
                "{=cgu_role_archer}Archer",
                () => true,
                () => _selectedRole = GearRole.Archer);

            starter.AddPlayerLine(
                "cgu_role_lancer",
                "cgu_role_player",
                "cgu_tier_npc",
                "{=cgu_role_lancer}Lancier (cavalerie)",
                () => true,
                () => _selectedRole = GearRole.Lancer);

            // Retour depuis le menu rôle -> passe par NPC -> hero_main_options
            starter.AddPlayerLine(
                "cgu_back_from_role",
                "cgu_role_player",
                "cgu_back_main_npc",
                "{=cgu_back}Retour",
                () => true,
                null);

            starter.AddDialogLine(
                "cgu_back_main_npc_line",
                "cgu_back_main_npc",
                "hero_main_options",
                "{=cgu_back_main}Très bien.",
                null,
                null);

            // 4) NPC introduit le menu de choix de rang
            starter.AddDialogLine(
                "cgu_tier_npc_line",
                "cgu_tier_npc",
                "cgu_tier_player",
                "{=cgu_tier}Choisis un rang (tu paies en or).",
                null,
                null);

            // 5) Choix du tier -> on va vers un état NPC "résultat" puis retour au menu principal
            starter.AddPlayerLine(
                "cgu_tier_1",
                "cgu_tier_player",
                "cgu_apply_npc",
                "{=cgu_t1}Rang 1",
                () => true,
                () => TryApplyTier(1));

            starter.AddPlayerLine(
                "cgu_tier_2",
                "cgu_tier_player",
                "cgu_apply_npc",
                "{=cgu_t2}Rang 2",
                () => true,
                () => TryApplyTier(2));

            starter.AddPlayerLine(
                "cgu_tier_3",
                "cgu_tier_player",
                "cgu_apply_npc",
                "{=cgu_t3}Rang 3",
                () => true,
                () => TryApplyTier(3));

            // NPC "ack" après application -> retour au menu principal
            starter.AddDialogLine(
                "cgu_apply_npc_line",
                "cgu_apply_npc",
                "hero_main_options",
                "{=cgu_done}D’accord.",
                null,
                null);

            // Retour depuis le menu tier -> passe par NPC -> menu rôle
            starter.AddPlayerLine(
                "cgu_back_from_tier",
                "cgu_tier_player",
                "cgu_back_role_npc",
                "{=cgu_back2}Retour",
                () => true,
                null);

            starter.AddDialogLine(
                "cgu_back_role_npc_line",
                "cgu_back_role_npc",
                "cgu_role_player",
                "{=cgu_back_role}Très bien.",
                null,
                null);
        }


        private bool IsTalkingToPlayerCompanion()
        {
            Hero h = Hero.OneToOneConversationHero;
            return h != null && h.IsPlayerCompanion;
        }

        private void TryApplyTier(int tier)
        {
            Hero target = Hero.OneToOneConversationHero;
            if (target == null || !target.IsPlayerCompanion)
                return;

            if (!_presets.TryGetValue((_selectedRole, tier), out var preset))
            {
                InformationManager.DisplayMessage(new InformationMessage($"[CGU] Preset manquant pour {_selectedRole} T{tier}."));
                return;
            }

            int cost = preset.Cost;
            if (Hero.MainHero.Gold < cost)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Pas assez d’or. Il faut {cost}."));
                return;
            }

            if (!TryBuildEquipment(target, preset, out Equipment newEquipment, out string error))
            {
                InformationManager.DisplayMessage(new InformationMessage(error));
                return;
            }

            Hero.MainHero.ChangeHeroGold(-cost);
            EquipmentHelper.AssignHeroEquipmentFromEquipment(target, newEquipment);

            InformationManager.DisplayMessage(new InformationMessage($"{target.Name}: équipement mis à jour (Rang {tier}) pour {cost} or."));
        }

        private bool TryBuildEquipment(Hero target, GearPreset preset, out Equipment equipment, out string error)
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
                                $"[CGU] Impossible de transférer l'ancien item du slot {slot} : {ex.Message}"
                            ));
                        }
                    }
                }


                var item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                if (item == null)
                {
                    error = $"[CGU] Item introuvable: '{itemId}'. Vérifie l’ID (vanilla/War Sails/mods).";
                    equipment = null;
                    return false;
                }

                equipment.AddEquipmentToSlotWithoutAgent(slot, new EquipmentElement(item));
            }

            return true;
        }

        private static Dictionary<(GearRole, int), GearPreset> BuildPresets()
        {

            return new Dictionary<(GearRole, int), GearPreset>
            {
                // ARCHER
                [(GearRole.Archer, 1)] = new GearPreset(3000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "woodland_yew_bow",
                    [EquipmentIndex.Weapon1] = "default_arrows",
                    [EquipmentIndex.Weapon2] = "battania_sword_4_t4",
                    [EquipmentIndex.Head] = "battania_fur_helmet",
                    [EquipmentIndex.Body] = "ranger_mail",
                    [EquipmentIndex.Cape] = "battania_cloak",
                    [EquipmentIndex.Gloves] = "highland_gloves",
                    [EquipmentIndex.Leg] = "battania_leather_boots",
                }),

                [(GearRole.Archer, 2)] = new GearPreset(8000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "steppe_war_bow",
                    [EquipmentIndex.Weapon1] = "default_arrows",
                    [EquipmentIndex.Weapon2] = "aserai_sword_5_t4",
                    [EquipmentIndex.Head] = "desert_helmet_with_mail",
                    [EquipmentIndex.Body] = "desert_robe_over_mail",
                    [EquipmentIndex.Cape] = "wrapped_scarf",
                    [EquipmentIndex.Leg] = "khuzait_curved_boots",
                }),

                [(GearRole.Archer, 3)] = new GearPreset(20000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "noble_bow",
                    [EquipmentIndex.Weapon1] = "default_arrows",
                    [EquipmentIndex.Weapon2] = "aserai_sword_5_t4",
                    [EquipmentIndex.Weapon3] = "large_adarga",
                }),

                // INFANTERIE
                [(GearRole.Infantry, 1)] = new GearPreset(2500, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "battania_sword_4_t4",
                    [EquipmentIndex.Weapon1] = "desert_round_shield",
                    [EquipmentIndex.Weapon2] = "northern_spear_3_t4",
                    [EquipmentIndex.Head] = "battania_fur_helmet",
                    [EquipmentIndex.Body] = "ranger_mail",
                    [EquipmentIndex.Cape] = "battania_cloak",
                    [EquipmentIndex.Gloves] = "highland_gloves",
                    [EquipmentIndex.Leg] = "battania_leather_boots",
                }),

                [(GearRole.Infantry, 2)] = new GearPreset(9000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "northern_spear_2_t3",
                    [EquipmentIndex.Weapon1] = "northern_round_shield",
                    [EquipmentIndex.Weapon2] = "sturgia_axe_3_t3",
                    [EquipmentIndex.Head] = "sturgian_helmet_base",
                    [EquipmentIndex.Body] = "northern_padded_gambeson",
                    [EquipmentIndex.Cape] = "wrapped_scarf",
                    [EquipmentIndex.Gloves] = "buttoned_leather_bracers",
                    [EquipmentIndex.Leg] = "highland_boots",
                }),

                [(GearRole.Infantry, 3)] = new GearPreset(22000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "northern_spear_3_t4",
                    [EquipmentIndex.Weapon1] = "heavy_round_shield",
                    [EquipmentIndex.Weapon2] = "sturgia_axe_4_t4",
                    [EquipmentIndex.Weapon3] = "northern_throwing_axe_1_t1",
                    [EquipmentIndex.Head] = "sturgian_lord_helmet_b",
                    [EquipmentIndex.Body] = "northern_coat_of_plates",
                    [EquipmentIndex.Cape] = "battania_civil_cape",
                    [EquipmentIndex.Gloves] = "reinforced_leather_vambraces",
                    [EquipmentIndex.Leg] = "fine_town_boots",
                }),

                // CAVALERIE (LANCIER)
                [(GearRole.Lancer, 1)] = new GearPreset(6000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Horse] = "aserai_horse",
                    [EquipmentIndex.HorseHarness] = "aseran_village_harness",
                    [EquipmentIndex.Weapon0] = "eastern_spear_3_t3",
                    [EquipmentIndex.Weapon1] = "studded_bound_kite_shield",
                    [EquipmentIndex.Weapon2] = "aserai_sword_1_t2",
                    [EquipmentIndex.Weapon3] = "eastern_javelin_2_t3",
                    [EquipmentIndex.Head] = "trailed_desert_helmet",
                    [EquipmentIndex.Cape] = "wrapped_scarf",
                    [EquipmentIndex.Body] = "studded_leather_coat",
                    [EquipmentIndex.Gloves] = "buttoned_leather_bracers",
                    [EquipmentIndex.Leg] = "steppe_leather_boots",
                }),

                [(GearRole.Lancer, 2)] = new GearPreset(12000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Horse] = "t2_aserai_horse",
                    [EquipmentIndex.HorseHarness] = "chain_horse_harness",
                    [EquipmentIndex.Weapon0] = "eastern_spear_4_t4",
                    [EquipmentIndex.Weapon1] = "bound_adarga",
                    [EquipmentIndex.Weapon2] = "aserai_sword_3_t3",
                    [EquipmentIndex.Head] = "trailed_desert_helmet",
                    [EquipmentIndex.Cape] = "wrapped_scarf",
                    [EquipmentIndex.Body] = "belted_leather_cuirass",
                    [EquipmentIndex.Gloves] = "rough_tied_bracers",
                    [EquipmentIndex.Leg] = "strapped_mail_chausses",
                }),

                [(GearRole.Lancer, 3)] = new GearPreset(25000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Horse] = "t2_aserai_horse",
                    [EquipmentIndex.HorseHarness] = "chain_horse_harness",
                    [EquipmentIndex.Weapon0] = "eastern_spear_4_t4",
                    [EquipmentIndex.Weapon1] = "bound_adarga",
                    [EquipmentIndex.Weapon2] = "aserai_sword_4_t4",
                    [EquipmentIndex.Weapon3] = "eastern_javelin_3_t4",
                    [EquipmentIndex.Head] = "desert_helmet_with_mail",
                    [EquipmentIndex.Cape] = "wrapped_scarf",
                    [EquipmentIndex.Body] = "northern_lamellar_armor",
                    [EquipmentIndex.Gloves] = "reinforced_leather_vambraces",
                    [EquipmentIndex.Leg] = "eastern_leather_boots",
                }),
            };
        }
    }

    public sealed class GearPreset
    {
        public int Cost { get; }
        public Dictionary<EquipmentIndex, string> Slots { get; }

        public GearPreset(int cost, Dictionary<EquipmentIndex, string> slots)
        {
            Cost = cost;
            Slots = slots;
        }
    }
}
