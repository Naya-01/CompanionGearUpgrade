using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Services;
using SandBox.GauntletUI;
using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;
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
        private bool _releaseMovieOnNextTick;

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

            ReleaseConfigurationMovie();

            _layer = null;
            _globalLayer = null;

            if (ReferenceEquals(_current, this))
                _current = null;
        }

        public static bool OpenConfiguration()
        {
            return _current != null && _current.OpenConfigurationInternal();
        }

        private bool OpenConfigurationInternal()
        {
            _isClanScreen = IsClanScreen(ScreenManager.TopScreen);
            if (!_isClanScreen || _layer == null)
                return false;

            if (_releaseMovieOnNextTick)
                ReleaseConfigurationMovie();

            if (_viewModel == null)
            {
                GearPresetConfigViewModel viewModel =
                    new GearPresetConfigViewModel(_service, _overrides, SetWindowLayerState);
                GauntletMovieIdentifier movie = null;
                try
                {
                    movie = _layer.LoadMovie("EquipmentConfigWindow", viewModel);
                    _viewModel = viewModel;
                    _movie = movie;
                    _viewModel.SetClanScreenVisible(true);
                }
                catch
                {
                    viewModel.OnFinalize();
                    if (movie != null)
                        _layer.ReleaseMovie(movie);
                    return false;
                }
            }

            _viewModel.ExecuteOpenConfiguration();
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
            _isClanScreen = IsClanScreen(screen);

            if (_viewModel != null)
                _viewModel.SetClanScreenVisible(_isClanScreen);

            if (_layer != null)
            {
                bool isModal = _isClanScreen && _viewModel != null && _viewModel.IsWindowOpen;
                _layer.IsFocusLayer = isModal;
                _layer.InputRestrictions.SetInputRestrictions(isModal, InputUsageMask.All);
            }
        }

        private static bool IsClanScreen(ScreenBase screen)
        {
            if (screen == null)
                return false;

            // NavalDLC's NavalGauntletClanScreen derives from this native
            // screen, so the type check covers both vanilla and DLC variants.
            return screen is GauntletClanScreen;
        }

        private void SetWindowLayerState(bool isOpen)
        {
            if (_layer == null)
                return;

            bool isModal = _isClanScreen && isOpen;
            _layer.IsFocusLayer = isModal;
            _layer.InputRestrictions.SetInputRestrictions(isModal, InputUsageMask.All);
            _releaseMovieOnNextTick = !isOpen && _movie != null;
        }

        private void OnGauntletTick()
        {
            if (_releaseMovieOnNextTick)
            {
                ReleaseConfigurationMovie();
                return;
            }

            ItemTableauWidget previewHost = GetPreviewHost();
            bool isHostReady = previewHost != null &&
                previewHost.ConnectedToRoot &&
                previewHost.IsRecursivelyVisible() &&
                previewHost.TextureProvider != null;
            bool isTextureReady = isHostReady &&
                previewHost.Texture != null &&
                previewHost.Texture.IsValid;

            _viewModel?.OnGauntletTick(isHostReady, isTextureReady);
        }

        private ItemTableauWidget GetPreviewHost()
        {
            Widget root = _layer?.UIContext?.Root;
            if (root == null)
                return null;

            var previewWidgets = root.FindChildrenWithId<ItemTableauWidget>("CGUPreviewTableau", true);
            if (previewWidgets == null || previewWidgets.Count == 0)
                return null;

            return previewWidgets[0];
        }

        private void ReleaseConfigurationMovie()
        {
            _releaseMovieOnNextTick = false;

            GearPresetConfigViewModel viewModel = _viewModel;
            GauntletMovieIdentifier movie = _movie;
            _viewModel = null;
            _movie = null;

            try
            {
                viewModel?.OnFinalize();
            }
            finally
            {
                if (_layer != null && movie != null)
                    _layer.ReleaseMovie(movie);
            }
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
