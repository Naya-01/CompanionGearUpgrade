using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Dialog;
using CompanionGearUpgrades.Services;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace CompanionGearUpgrades.Behaviors
{
    public sealed class CompanionGearUpgradeBehavior : CampaignBehaviorBase
    {
        // Saved in the savegame via SyncData (simple types only)
        private Dictionary<string, int> _costOverrides;
        private Dictionary<string, string> _itemOverrides;
        private CompanionGearUpgradeDialog _dialog;

        public CompanionGearUpgradeBehavior()
        {
            _costOverrides = new Dictionary<string, int>();
            _itemOverrides = new Dictionary<string, string>();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("CGU_CostOverrides", ref _costOverrides);
            dataStore.SyncData("CGU_ItemOverrides", ref _itemOverrides);

            if (_costOverrides == null)
                _costOverrides = new Dictionary<string, int>();
            if (_itemOverrides == null)
                _itemOverrides = new Dictionary<string, string>();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            var defaults = GearPresetRepository.BuildPresets();
            var overrides = new GearPresetOverrides(_costOverrides, _itemOverrides);

            var service = new CompanionGearUpgradeService(defaults, overrides);
            _dialog = new CompanionGearUpgradeDialog(service, overrides);
            _dialog.AddDialogs(starter);
        }
    }
}
