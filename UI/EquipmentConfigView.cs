using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Services;
using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace CompanionGearUpgrades.UI
{
    /// <summary>
    /// Hosts the preset editor as a modal Gauntlet layer while the Clan screen
    /// is active. The editor itself never touches a hero or an inventory.
    /// </summary>
    public sealed class EquipmentConfigView
    {
        private const string LayerName = "CompanionGearUpgradeClanEquipment";
        private static EquipmentConfigView _current;

        private readonly CompanionGearUpgradeService _service;
        private readonly GearPresetOverrides _overrides;

        private GlobalLayer _globalLayer;
        private GauntletLayer _layer;
        private GauntletMovieIdentifier _movie;
        private GearPresetConfigViewModel _viewModel;
        private bool _isClanScreen;

        public EquipmentConfigView(CompanionGearUpgradeService service, GearPresetOverrides overrides)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _overrides = overrides ?? throw new ArgumentNullException(nameof(overrides));
        }

        public void Initialize()
        {
            if (_globalLayer != null)
                return;

            _current = this;
            _layer = new GauntletLayer(LayerName, 1000, false);
            _viewModel = new GearPresetConfigViewModel(_service, _overrides, SetWindowLayerState);
            _movie = _layer.LoadMovie("EquipmentConfigWindow", _viewModel);
            _globalLayer = new EquipmentGlobalLayer(_layer, OnGauntletTick);

            ScreenManager.OnPushScreen += OnPushScreen;
            ScreenManager.OnPopScreen += OnPopScreen;
            ScreenManager.AddGlobalLayer(_globalLayer, false);
            UpdateScreenVisibility(ScreenManager.TopScreen);
        }

        public void Dispose()
        {
            ScreenManager.OnPushScreen -= OnPushScreen;
            ScreenManager.OnPopScreen -= OnPopScreen;

            if (_globalLayer != null)
                ScreenManager.RemoveGlobalLayer(_globalLayer);

            if (_layer != null && _movie != null)
                _layer.ReleaseMovie(_movie);

            _movie = null;
            _viewModel = null;
            _layer = null;
            _globalLayer = null;

            if (ReferenceEquals(_current, this))
                _current = null;
        }

        public static bool OpenConfiguration()
        {
            if (_current == null || _current._viewModel == null)
                return false;

            _current._viewModel.ExecuteOpenConfiguration();
            return true;
        }

        private void OnPushScreen(ScreenBase screen)
        {
            UpdateScreenVisibility(screen);
        }

        private void OnPopScreen(ScreenBase screen)
        {
            UpdateScreenVisibility(ScreenManager.TopScreen);
        }

        private void UpdateScreenVisibility(ScreenBase screen)
        {
            _isClanScreen = screen != null &&
                string.Equals(screen.GetType().Name, "GauntletClanScreen", StringComparison.Ordinal);

            if (_viewModel != null)
                _viewModel.SetClanScreenVisible(_isClanScreen);

            if (_layer != null)
            {
                bool isModal = _isClanScreen && _viewModel != null && _viewModel.IsWindowOpen;
                _layer.IsFocusLayer = isModal;
                _layer.InputRestrictions.SetInputRestrictions(isModal, InputUsageMask.All);
            }
        }

        private void SetWindowLayerState(bool isOpen)
        {
            if (_layer == null)
                return;

            _layer.IsFocusLayer = _isClanScreen && isOpen;
            _layer.InputRestrictions.SetInputRestrictions(isOpen, InputUsageMask.All);
        }

        private void OnGauntletTick()
        {
            _viewModel?.OnGauntletTick();
        }

        private sealed class EquipmentGlobalLayer : GlobalLayer
        {
            private readonly Action _onTick;

            public EquipmentGlobalLayer(ScreenLayer layer, Action onTick)
            {
                Layer = layer;
                _onTick = onTick;
            }

            protected override void OnTick(float dt)
            {
                base.OnTick(dt);
                _onTick?.Invoke();
            }
        }
    }
}
