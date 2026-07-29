using CompanionGearUpgrades.Behaviors;
using Bannerlord.UIExtenderEx;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CompanionGearUpgrades
{
    public sealed class SubModule : MBSubModuleBase
    {
        private const string HarmonyId = "CompanionGearUpgrades.ClanEquipment";

        private UIExtender _uiExtender;
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            _uiExtender = UIExtender.Create("CompanionGearUpgrades");
            _uiExtender.Register(typeof(SubModule).Assembly);
            _uiExtender.Enable();

            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll(typeof(SubModule).Assembly);
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            var starter = gameStarterObject as CampaignGameStarter;
            if (starter == null)
                return;

            starter.AddBehavior(new CompanionGearUpgradeBehavior());
        }

        public override void OnGameEnd(Game game)
        {
            CompanionGearUpgradeBehavior.ClearPresetConfiguration();

            base.OnGameEnd(game);
        }

        protected override void OnSubModuleUnloaded()
        {
            if (_uiExtender != null)
            {
                _uiExtender.Deregister();
                _uiExtender = null;
            }

            if (_harmony != null)
            {
                _harmony.UnpatchAll(HarmonyId);
                _harmony = null;
            }

            base.OnSubModuleUnloaded();
        }
    }
}
