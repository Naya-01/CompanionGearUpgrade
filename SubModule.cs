using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CompanionGearUpgrades
{
    public sealed class SubModule : MBSubModuleBase
    {
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {

            var starter = gameStarterObject as CampaignGameStarter;
            if (starter == null)
                return;

            starter.AddBehavior(new CompanionGearUpgradeBehavior());
        }
    }
}
