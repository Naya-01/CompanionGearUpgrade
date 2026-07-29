using HarmonyLib;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using CompanionGearUpgrades.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

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

    /// <summary>
    /// Adds the per-row Gauntlet bindings without replacing Native's view model.
    /// ClanLordTuple is also used for family rows, so the action is exposed only
    /// for actual player companions.
    /// </summary>
    [ViewModelMixin("RefreshValues")]
    internal sealed class ClanCompanionGearPresetViewModelMixin : BaseViewModelMixin<ClanLordItemVM>
    {
        private readonly Hero _hero;
        private readonly HintViewModel _gearPresetApplyHint;
        private readonly bool _isGearPresetApplyVisible;

        public ClanCompanionGearPresetViewModelMixin(ClanLordItemVM viewModel)
            : base(viewModel)
        {
            _hero = viewModel != null ? viewModel.GetHero() : null;
            _isGearPresetApplyVisible = _hero != null &&
                _hero != Hero.MainHero &&
                _hero.IsPlayerCompanion;
            _gearPresetApplyHint = new HintViewModel(
                new TextObject("Apply gear preset to this companion."),
                "CGU.ApplyGearPreset");
        }

        [DataSourceProperty]
        public bool IsGearPresetApplyVisible
        {
            get { return _isGearPresetApplyVisible; }
        }

        [DataSourceProperty]
        public bool IsGearPresetApplyEnabled
        {
            get { return _isGearPresetApplyVisible; }
        }

        [DataSourceProperty]
        public HintViewModel GearPresetApplyHint
        {
            get { return _gearPresetApplyHint; }
        }

        [DataSourceMethod]
        public void ExecuteApplyGearPreset()
        {
            if (!_isGearPresetApplyVisible)
                return;

            if (!CompanionGearUpgradeBehavior.TryOpenClanPresetApplication(_hero))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[CGU] Gear preset application is not ready yet."));
            }
        }
    }

    /// <summary>
    /// Supplies the tooltip for the Gear Presets Clan action without changing
    /// the native ClanManagementVM or the Native/SandBox prefab.
    /// </summary>
    [ViewModelMixin]
    internal sealed class ClanGearPresetsTabViewModelMixin : BaseViewModelMixin<ClanManagementVM>
    {
        private readonly HintViewModel _gearPresetManagementHint;

        public ClanGearPresetsTabViewModelMixin(ClanManagementVM viewModel)
            : base(viewModel)
        {
            _gearPresetManagementHint = new HintViewModel(
                new TextObject("Gear Presets\nOpen role, tier and equipment preset management."),
                null);
        }

        [DataSourceProperty]
        public HintViewModel GearPresetManagementHint
        {
            get { return _gearPresetManagementHint; }
        }
    }
}
