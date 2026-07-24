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
        // Stable custom role ID -> display name. Definitions themselves stay
        // plain savegame-friendly dictionary data; the runtime catalog is
        // rebuilt when the campaign session starts.
        private Dictionary<string, string> _customRoleNames;
        private static EquipmentConfigView _equipmentConfigView;
        private static ApplyGearPresetView _applyGearPresetView;

        public CompanionGearUpgradeBehavior()
        {
            _costOverrides = new Dictionary<string, int>();
            _itemOverrides = new Dictionary<string, string>();
            _customRoleNames = new Dictionary<string, string>();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("CGU_CostOverrides", ref _costOverrides);
            dataStore.SyncData("CGU_ItemOverrides", ref _itemOverrides);
            dataStore.SyncData("CGU_CustomRoles", ref _customRoleNames);

            if (_costOverrides == null)
                _costOverrides = new Dictionary<string, int>();
            if (_itemOverrides == null)
                _itemOverrides = new Dictionary<string, string>();
            if (_customRoleNames == null)
                _customRoleNames = new Dictionary<string, string>();

            // Old saves contain no entry under CGU_CustomRoles and therefore
            // naturally keep the three built-in roles. Normalize newly-loaded
            // data before it can be exposed to the UI or used for cleanup.
            GearPresetRepository.NormalizeCustomRoles(_customRoleNames);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            var defaults = GearPresetRepository.BuildPresets();
            var overrides = new GearPresetOverrides(_costOverrides, _itemOverrides);

            var service = new CompanionGearUpgradeService(defaults, overrides, _customRoleNames);
            var dialog = new CompanionGearUpgradeDialog(service);
            dialog.AddDialogs(starter);

            // Clan and conversation configuration share this exact Gauntlet
            // view, service and persisted override store.
            _equipmentConfigView = new EquipmentConfigView(service);
            _equipmentConfigView.Initialize();

            _applyGearPresetView = new ApplyGearPresetView(service);
            _applyGearPresetView.Initialize();
        }

        public static bool TryOpenClanPresetConfiguration()
        {
            return _equipmentConfigView != null && _equipmentConfigView.OpenClanConfiguration();
        }

        public static bool TryOpenConversationPresetConfiguration()
        {
            return _equipmentConfigView != null && _equipmentConfigView.OpenConversationConfiguration();
        }

        /// <summary>
        /// Opens the dedicated, read-only preset application window for the
        /// explicitly selected Clan hero. The apply view owns no domain
        /// mutation; it delegates confirmation to the shared service.
        /// </summary>
        public static bool TryOpenClanPresetApplication(Hero target)
        {
            return _applyGearPresetView != null && _applyGearPresetView.Open(target);
        }

        public static void ClearPresetConfiguration()
        {
            _applyGearPresetView?.Dispose();
            _applyGearPresetView = null;
            _equipmentConfigView?.Dispose();
            _equipmentConfigView = null;
        }
    }
}
