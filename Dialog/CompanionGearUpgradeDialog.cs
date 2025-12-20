using CompanionGearUpgrades.Domain;
using CompanionGearUpgrades.Services;
using CompanionGearUpgrades.Data;
using TaleWorlds.CampaignSystem;

namespace CompanionGearUpgrades.Dialog
{
    public sealed class CompanionGearUpgradeDialog
    {
        private readonly CompanionGearUpgradeService _service;
        private readonly GearPresetConfigUi _configUi;
        private GearRole _selectedRole;

        public CompanionGearUpgradeDialog(CompanionGearUpgradeService service, GearPresetOverrides overrides)
        {
            _service = service;
            _configUi = new GearPresetConfigUi(service, overrides);
        }

        private bool IsTalkingToPlayerCompanion()
        {
            Hero h = Hero.OneToOneConversationHero;
            return h != null && h.IsPlayerCompanion;
        }

        public void AddDialogs(CampaignGameStarter starter)
        {
            // 1) Entry from the main menu -> go to an NPC reply
            starter.AddPlayerLine(
                "cgu_open",
                "hero_main_options",
                "cgu_role_npc",
                "{=cgu_open}Upgrade your equipment",
                IsTalkingToPlayerCompanion,
                null,
                100);

            // Dynamic preset configuration
            starter.AddPlayerLine(
                "cgu_config_open",
                "hero_main_options",
                "hero_main_options",
                "{=cgu_config_open}Configure upgrade presets",
                IsTalkingToPlayerCompanion,
                () => _configUi.Open(),
                99);

            // 2) NPC asks the question -> choice menu (player options)
            starter.AddDialogLine(
                "cgu_role_npc_line",
                "cgu_role_npc",
                "cgu_role_player",
                "{=cgu_role}What equipment style?",
                null,
                null);

            // 3) Role choice -> go through an NPC state then reach the tier menu
            starter.AddPlayerLine(
                "cgu_role_infantry",
                "cgu_role_player",
                "cgu_tier_npc",
                "{=cgu_role_infantry}Soldier (infantry)",
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
                "{=cgu_role_lancer}Lancer (cavalry)",
                () => true,
                () => _selectedRole = GearRole.Lancer);

            // Back from the role menu -> go through an NPC state -> hero_main_options
            starter.AddPlayerLine(
                "cgu_back_from_role",
                "cgu_role_player",
                "cgu_back_main_npc",
                "{=cgu_back}Back",
                () => true,
                null);

            starter.AddDialogLine(
                "cgu_back_main_npc_line",
                "cgu_back_main_npc",
                "hero_main_options",
                "{=cgu_back_main}Alright.",
                null,
                null);

            // 4) NPC introduces the tier selection menu
            starter.AddDialogLine(
                "cgu_tier_npc_line",
                "cgu_tier_npc",
                "cgu_tier_player",
                "{=cgu_tier}Choose a tier (you pay in gold).",
                null,
                null);

            // 5) Tier choice -> go to an NPC "result" state then return to the main menu
            starter.AddPlayerLine(
               "cgu_tier_1",
               "cgu_tier_player",
               "cgu_apply_npc",
               "{=cgu_t1}Tier 1 ({COST_T1} gold)",
               () => _service.SetTierCostVar(_selectedRole, 1, "COST_T1"),
               () => _service.TryApplyTier(_selectedRole, 1));

            starter.AddPlayerLine(
                "cgu_tier_2",
                "cgu_tier_player",
                "cgu_apply_npc",
                "{=cgu_t2}Tier 2 ({COST_T2} gold)",
                () => _service.SetTierCostVar(_selectedRole, 2, "COST_T2"),
                () => _service.TryApplyTier(_selectedRole, 2));

            starter.AddPlayerLine(
                "cgu_tier_3",
                "cgu_tier_player",
                "cgu_apply_npc",
                "{=cgu_t3}Tier 3 ({COST_T3} gold)",
                () => _service.SetTierCostVar(_selectedRole, 3, "COST_T3"),
                () => _service.TryApplyTier(_selectedRole, 3));

            // NPC "ack" after applying -> return to the main menu
            starter.AddDialogLine(
                "cgu_apply_npc_line",
                "cgu_apply_npc",
                "hero_main_options",
                "{=cgu_done}Okay.",
                null,
                null);

            // Back from the tier menu -> go through an NPC state -> role menu
            starter.AddPlayerLine(
                "cgu_back_from_tier",
                "cgu_tier_player",
                "cgu_back_role_npc",
                "{=cgu_back2}Back",
                () => true,
                null);

            starter.AddDialogLine(
                "cgu_back_role_npc_line",
                "cgu_back_role_npc",
                "cgu_role_player",
                "{=cgu_back_role}Alright.",
                null,
                null);
        }

    }
}
