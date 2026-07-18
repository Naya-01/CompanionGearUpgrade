using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CompanionGearUpgrades.UI
{
    /// <summary>
    /// Adds one command to Native's ClanManagementVM. The command is bound by
    /// the injected Clan tab and opens the already-loaded equipment movie.
    /// </summary>
    [ViewModelMixin("RefreshValues")]
    internal sealed class ClanManagementViewModelMixin : BaseViewModelMixin<ClanManagementVM>
    {
        public ClanManagementViewModelMixin(ClanManagementVM vm)
            : base(vm)
        {
        }

        [DataSourceMethod]
        public void ExecuteOpenEquipmentConfiguration()
        {
            InformationManager.DisplayMessage(new InformationMessage("[CGU] Clic sur l'onglet Equipment détecté."));
            EquipmentConfigView.OpenConfiguration();
        }
    }
}
