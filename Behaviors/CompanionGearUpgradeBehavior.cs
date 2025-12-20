using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Dialog;
using CompanionGearUpgrades.Services;
using TaleWorlds.CampaignSystem;

namespace CompanionGearUpgrades.Behaviors
{
    public sealed class CompanionGearUpgradeBehavior : CampaignBehaviorBase
    {
        private readonly CompanionGearUpgradeDialog _dialog;

        public CompanionGearUpgradeBehavior()
        {
            var presets = GearPresetRepository.BuildPresets();
            var service = new CompanionGearUpgradeService(presets);
            _dialog = new CompanionGearUpgradeDialog(service);
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
            _dialog.AddDialogs(starter);
        }
    }
}
