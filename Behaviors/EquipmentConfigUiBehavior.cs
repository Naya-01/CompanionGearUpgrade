using CompanionGearUpgrades.UI;
using TaleWorlds.CampaignSystem;

namespace CompanionGearUpgrades.Behaviors
{
    /// <summary>
    /// Keeps the equipment movie attached while the Clan screen is active.
    /// </summary>
    public sealed class EquipmentConfigUiBehavior : CampaignBehaviorBase
    {
        private readonly EquipmentConfigView _equipmentConfigView = new EquipmentConfigView();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Configuration is deliberately stored in ModuleData, not in savegames.
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            _equipmentConfigView.Update();
        }

        private void OnTick(float dt)
        {
            // Clan screens can be created after session launch and are replaced
            // during navigation, so update the attachment on each campaign tick.
            _equipmentConfigView.Update();
        }
    }
}
