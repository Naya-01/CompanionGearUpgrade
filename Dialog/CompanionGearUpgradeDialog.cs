using CompanionGearUpgrades.Behaviors;
using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CompanionGearUpgrades.Dialog
{
    public sealed class CompanionGearUpgradeDialog
    {
        // Dialogues are registered only once when the campaign starts. Reserve
        // the seven possible custom-role entries up front, then decide whether
        // each entry is visible from the current persisted role catalog.
        private static readonly string[] CustomRoleTextVariables =
        {
            "CGU_CUSTOM_ROLE_1",
            "CGU_CUSTOM_ROLE_2",
            "CGU_CUSTOM_ROLE_3",
            "CGU_CUSTOM_ROLE_4",
            "CGU_CUSTOM_ROLE_5",
            "CGU_CUSTOM_ROLE_6",
            "CGU_CUSTOM_ROLE_7"
        };

        private readonly CompanionGearUpgradeService _service;
        private string _selectedRoleId;

        public CompanionGearUpgradeDialog(CompanionGearUpgradeService service)
        {
            _service = service;
        }

        private static void OpenPresetConfiguration()
        {
            if (!CompanionGearUpgradeBehavior.TryOpenConversationPresetConfiguration())
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[CGU] Preset configuration is not ready yet."));
            }
        }

        private bool IsTalkingToPlayerCompanion()
        {
            Hero h = Hero.OneToOneConversationHero;
            return h != null && (h.IsPlayerCompanion || h.Clan == Clan.PlayerClan);
        }

        private GearRoleDefinition GetCustomRoleForDialogSlot(int slotIndex)
        {
            if (slotIndex < 0)
                return null;

            int customRoleIndex = 0;
            foreach (GearRoleDefinition role in _service.GetRoleDefinitions())
            {
                if (role == null ||
                    role.IsDefaultRole ||
                    string.IsNullOrWhiteSpace(role.Id) ||
                    string.IsNullOrWhiteSpace(role.Name))
                {
                    continue;
                }

                if (customRoleIndex == slotIndex)
                    return role;

                customRoleIndex++;
            }

            return null;
        }

        private bool CanShowCustomRoleDialogSlot(int slotIndex, string textVariable)
        {
            GearRoleDefinition role = GetCustomRoleForDialogSlot(slotIndex);
            if (role == null)
                return false;

            MBTextManager.SetTextVariable(textVariable, role.Name);
            return true;
        }

        private void SelectCustomRoleDialogSlot(int slotIndex)
        {
            GearRoleDefinition role = GetCustomRoleForDialogSlot(slotIndex);
            if (role != null)
                _selectedRoleId = role.Id;
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
                "cgu_back_main_npc",
                "{=cgu_config_open}Configure upgrade presets",
                IsTalkingToPlayerCompanion,
                OpenPresetConfiguration,
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
                () => _selectedRoleId = "Infantry");

            starter.AddPlayerLine(
                "cgu_role_archer",
                "cgu_role_player",
                "cgu_tier_npc",
                "{=cgu_role_archer}Archer",
                () => true,
                () => _selectedRoleId = "Archer");

            starter.AddPlayerLine(
                "cgu_role_lancer",
                "cgu_role_player",
                "cgu_tier_npc",
                "{=cgu_role_lancer}Lancer (cavalry)",
                () => true,
                () => _selectedRoleId = "Lancer");

            for (int customRoleSlot = 0; customRoleSlot < CustomRoleTextVariables.Length; customRoleSlot++)
            {
                int slotIndex = customRoleSlot;
                string textVariable = CustomRoleTextVariables[slotIndex];

                // Static ASCII IDs avoid registering user input as a dialogue
                // identifier while the role name itself remains fully dynamic.
                starter.AddPlayerLine(
                    "cgu_role_custom_" + (slotIndex + 1),
                    "cgu_role_player",
                    "cgu_tier_npc",
                    "{" + textVariable + "}",
                    () => CanShowCustomRoleDialogSlot(slotIndex, textVariable),
                    () => SelectCustomRoleDialogSlot(slotIndex));
            }

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
               () => _service.SetTierCostVar(_selectedRoleId, 1, "COST_T1"),
               () => _service.TryApplyTier(_selectedRoleId, 1));

            starter.AddPlayerLine(
                "cgu_tier_2",
                "cgu_tier_player",
                "cgu_apply_npc",
                "{=cgu_t2}Tier 2 ({COST_T2} gold)",
                () => _service.SetTierCostVar(_selectedRoleId, 2, "COST_T2"),
                () => _service.TryApplyTier(_selectedRoleId, 2));

            starter.AddPlayerLine(
                "cgu_tier_3",
                "cgu_tier_player",
                "cgu_apply_npc",
                "{=cgu_t3}Tier 3 ({COST_T3} gold)",
                () => _service.SetTierCostVar(_selectedRoleId, 3, "COST_T3"),
                () => _service.TryApplyTier(_selectedRoleId, 3));

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
