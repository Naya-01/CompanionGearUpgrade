using CompanionGearUpgrades.Behaviors;
using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Domain;
using CompanionGearUpgrades.Services;
using System.Collections.Generic;
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
        private static readonly string[] CustomRoleTextVariables = CreateCustomRoleTextVariables();

        private readonly CompanionGearUpgradeService _service;
        private string _selectedRoleId;

        public CompanionGearUpgradeDialog(CompanionGearUpgradeService service)
        {
            _service = service;
        }

        private static string[] CreateCustomRoleTextVariables()
        {
            var variables = new string[GearPresetRepository.MaxCustomRoleCount];
            for (int index = 0; index < variables.Length; index++)
                variables[index] = "CGU_CUSTOM_ROLE_" + (index + 1);

            return variables;
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
            return _service.IsHeroEligibleForPresetApplication(Hero.OneToOneConversationHero);
        }

        private void ApplySelectedTierFromConversation(int tier)
        {
            Hero target = Hero.OneToOneConversationHero;
            // Preserve the historical conversation behavior: if the target
            // becomes ineligible while the dialogue is open, do nothing.
            if (!_service.IsHeroEligibleForPresetApplication(target))
                return;

            GearPresetApplicationResult result = _service.TryApplyTier(target, _selectedRoleId, tier);
            if (!string.IsNullOrEmpty(result.Message))
                InformationManager.DisplayMessage(new InformationMessage(result.Message));
        }

        private GearRoleDefinition GetCustomRoleForDialogSlot(int slotIndex)
        {
            IReadOnlyList<GearRoleDefinition> customRoles = _service.GetCustomRoles();
            if (slotIndex < 0 || customRoles == null || slotIndex >= customRoles.Count)
                return null;

            return customRoles[slotIndex];
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
                () => _selectedRoleId = GearPresetRepository.GetRoleId(GearRole.Infantry));

            starter.AddPlayerLine(
                "cgu_role_archer",
                "cgu_role_player",
                "cgu_tier_npc",
                "{=cgu_role_archer}Archer",
                () => true,
                () => _selectedRoleId = GearPresetRepository.GetRoleId(GearRole.Archer));

            starter.AddPlayerLine(
                "cgu_role_lancer",
                "cgu_role_player",
                "cgu_tier_npc",
                "{=cgu_role_lancer}Lancer (cavalry)",
                () => true,
                () => _selectedRoleId = GearPresetRepository.GetRoleId(GearRole.Lancer));

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
               () => ApplySelectedTierFromConversation(1));

            starter.AddPlayerLine(
                "cgu_tier_2",
                "cgu_tier_player",
                "cgu_apply_npc",
                "{=cgu_t2}Tier 2 ({COST_T2} gold)",
                () => _service.SetTierCostVar(_selectedRoleId, 2, "COST_T2"),
                () => ApplySelectedTierFromConversation(2));

            starter.AddPlayerLine(
                "cgu_tier_3",
                "cgu_tier_player",
                "cgu_apply_npc",
                "{=cgu_t3}Tier 3 ({COST_T3} gold)",
                () => _service.SetTierCostVar(_selectedRoleId, 3, "COST_T3"),
                () => ApplySelectedTierFromConversation(3));

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
