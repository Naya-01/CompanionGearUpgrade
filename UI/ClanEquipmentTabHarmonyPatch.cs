using HarmonyLib;
using CompanionGearUpgrades.Behaviors;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Library;

namespace CompanionGearUpgrades.UI
{
    /// <summary>
    /// The injected tab calls Native's existing SetSelectedCategory command.
    /// Category 4 is reserved by this module, intercepted before Native can
    /// process it, and opens the equipment configuration instead.
    /// </summary>
    [HarmonyPatch(typeof(ClanManagementVM), "SetSelectedCategory")]
    internal static class ClanEquipmentTabHarmonyPatch
    {
        internal const int EquipmentCategory = 4;

        [HarmonyPrefix]
        private static bool Prefix(int __0)
        {
            if (__0 != EquipmentCategory)
                return true;

            if (!CompanionGearUpgradeBehavior.TryOpenClanPresetConfiguration())
                InformationManager.DisplayMessage(new InformationMessage("[CGU] Preset configuration is not ready yet."));

            // Do not let Native process category 4: it only owns categories 0-3.
            return false;
        }
    }
}
