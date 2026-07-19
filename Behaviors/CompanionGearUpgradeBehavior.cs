using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Dialog;
using CompanionGearUpgrades.Services;
using CompanionGearUpgrades.UI;
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
        private static EquipmentConfigView _equipmentConfigView;

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
            _dialog = new CompanionGearUpgradeDialog(service);
            _dialog.AddDialogs(starter);

            // Clan and conversation configuration share this exact Gauntlet
            // view, service and persisted override store.
            _equipmentConfigView = new EquipmentConfigView(service, overrides);
            _equipmentConfigView.Initialize();
        }

        public static bool TryOpenClanPresetConfiguration()
        {
            if (_equipmentConfigView == null)
                return false;

            return EquipmentConfigView.OpenClanConfiguration();
        }

        public static bool TryOpenConversationPresetConfiguration()
        {
            if (_equipmentConfigView == null)
                return false;

            return EquipmentConfigView.OpenConversationConfiguration();
        }

        public static void ClearPresetConfiguration()
        {
            if (_equipmentConfigView != null)
            {
                _equipmentConfigView.Dispose();
                _equipmentConfigView = null;
            }
        }
    }
}
