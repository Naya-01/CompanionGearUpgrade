using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace CompanionGearUpgrades
{
    public sealed class SubModule : MBSubModuleBase
    {
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            //if (game.GameType is not Campaign) return;

            //if (gameStarterObject is CampaignGameStarter starter)
            //{
            //    starter.AddBehavior(new CompanionGearUpgradeBehavior());
            //}

            var starter = gameStarterObject as CampaignGameStarter;
            if (starter == null)
                return;

            starter.AddBehavior(new CompanionGearUpgradeBehavior());
        }
    }
}
