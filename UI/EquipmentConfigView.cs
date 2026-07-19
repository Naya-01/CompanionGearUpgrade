using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Services;
using SandBox.GauntletUI;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;
using TaleWorlds.ScreenSystem;

namespace CompanionGearUpgrades.UI
{
    /// <summary>
    /// Hosts one shared preset editor as a modal Gauntlet layer from either
    /// Clan > Equipment or a companion conversation.
    /// </summary>
    public sealed class EquipmentConfigView
    {
        private enum ConfigurationHost
        {
            None,
            Clan,
            Conversation
        }

        private const string LayerName = "CompanionGearUpgradeEquipmentConfig";
        private static EquipmentConfigView _current;

        private readonly CompanionGearUpgradeService _service;
        private readonly GearPresetOverrides _overrides;

        private GlobalLayer _globalLayer;
        private GauntletLayer _layer;
        private GauntletMovieIdentifier _movie;
        private GearPresetConfigViewModel _viewModel;
        private ConfigurationHost _host;
        private ScreenBase _hostScreen;
        private bool _isHostScreenVisible;
        private bool _isLayerModal;
        private bool _releaseMovieOnNextTick;
        private bool _openConversationOnNextTick;
        private ScreenBase _pendingConversationScreen;

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
        }

        public void Dispose()
        {
            ScreenManager.OnPushScreen -= OnPushScreen;
            ScreenManager.OnPopScreen -= OnPopScreen;

            _openConversationOnNextTick = false;
            _pendingConversationScreen = null;
            SetLayerInteraction(false);

            if (_globalLayer != null)
                ScreenManager.RemoveGlobalLayer(_globalLayer);

            ReleaseConfigurationMovie();

            _layer = null;
            _globalLayer = null;

            if (ReferenceEquals(_current, this))
                _current = null;
        }

        public static bool OpenClanConfiguration()
        {
            return _current != null && _current.OpenClanConfigurationInternal();
        }

        public static bool OpenConversationConfiguration()
        {
            return _current != null && _current.QueueConversationConfiguration();
        }

        private bool OpenClanConfigurationInternal()
        {
            ScreenBase screen = ScreenManager.TopScreen;
            if (!IsClanScreen(screen))
                return false;

            return OpenConfigurationInternal(ConfigurationHost.Clan, screen);
        }

        private bool QueueConversationConfiguration()
        {
            ScreenBase screen = ScreenManager.TopScreen;
            if (_layer == null || screen == null || !IsConversationInProgress())
                return false;

            // The dialogue consequence is still running here. Loading on the
            // next global-layer tick lets Bannerlord finish DoOptionContinue
            // before this layer takes focus.
            _pendingConversationScreen = screen;
            _openConversationOnNextTick = true;
            return true;
        }

        private bool OpenConfigurationInternal(ConfigurationHost host, ScreenBase hostScreen)
        {
            if (_layer == null || !IsHostValid(host, hostScreen, ScreenManager.TopScreen))
                return false;

            if (_releaseMovieOnNextTick)
                ReleaseConfigurationMovie();

            _host = host;
            _hostScreen = hostScreen;
            _isHostScreenVisible = true;

            GearPresetConfigViewModel createdViewModel = null;
            GauntletMovieIdentifier createdMovie = null;
            try
            {
                if (_viewModel == null)
                {
                    createdViewModel =
                        new GearPresetConfigViewModel(_service, _overrides, SetWindowLayerState);
                    createdMovie = _layer.LoadMovie("EquipmentConfigWindow", createdViewModel);
                    _viewModel = createdViewModel;
                    _movie = createdMovie;
                }

                _viewModel.SetHostScreenVisible(true);
                _viewModel.ExecuteOpenConfiguration();
                return true;
            }
            catch
            {
                GearPresetConfigViewModel failedViewModel = _viewModel ?? createdViewModel;
                GauntletMovieIdentifier failedMovie = _movie ?? createdMovie;
                _viewModel = null;
                _movie = null;
                _releaseMovieOnNextTick = false;
                SetLayerInteraction(false);
                ResetHost();

                try
                {
                    failedViewModel?.OnFinalize();
                }
                catch
                {
                }

                try
                {
                    if (_layer != null && failedMovie != null)
                        _layer.ReleaseMovie(failedMovie);
                }
                catch
                {
                }

                return false;
            }
        }

        private void OnPushScreen(ScreenBase screen)
        {
            UpdateHostVisibility(screen);
        }

        private void OnPopScreen(ScreenBase screen)
        {
            UpdateHostVisibility(ScreenManager.TopScreen);
        }

        private void UpdateHostVisibility(ScreenBase screen)
        {
            if (_host == ConfigurationHost.None)
                return;

            _isHostScreenVisible = IsHostValid(_host, _hostScreen, screen);

            if (_viewModel != null)
                _viewModel.SetHostScreenVisible(_isHostScreenVisible);

            bool isModal = _isHostScreenVisible && _viewModel != null && _viewModel.IsWindowOpen;
            SetLayerInteraction(isModal);
        }

        private static bool IsClanScreen(ScreenBase screen)
        {
            if (screen == null)
                return false;

            // NavalDLC's NavalGauntletClanScreen derives from this native
            // screen, so the type check covers both vanilla and DLC variants.
            return screen is GauntletClanScreen;
        }

        private static bool IsConversationInProgress()
        {
            return Campaign.Current != null &&
                Campaign.Current.ConversationManager != null &&
                Campaign.Current.ConversationManager.IsConversationInProgress;
        }

        private static bool IsHostValid(
            ConfigurationHost host,
            ScreenBase expectedScreen,
            ScreenBase currentScreen)
        {
            if (expectedScreen == null || !ReferenceEquals(expectedScreen, currentScreen))
                return false;

            if (host == ConfigurationHost.Clan)
                return IsClanScreen(currentScreen);

            return host == ConfigurationHost.Conversation && IsConversationInProgress();
        }

        private void SetWindowLayerState(bool isOpen)
        {
            bool isModal = _isHostScreenVisible && isOpen;
            SetLayerInteraction(isModal);
            _releaseMovieOnNextTick = !isOpen && _movie != null;
        }

        private void SetLayerInteraction(bool isModal)
        {
            if (_layer == null || _isLayerModal == isModal)
                return;

            _isLayerModal = isModal;
            if (isModal)
            {
                _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
                _layer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(_layer);
            }
            else
            {
                _layer.IsFocusLayer = false;
                _layer.InputRestrictions.ResetInputRestrictions();
                ScreenManager.TryLoseFocus(_layer);
            }
        }

        private void OnGauntletTick()
        {
            if (_openConversationOnNextTick)
            {
                ScreenBase conversationScreen = _pendingConversationScreen;
                _openConversationOnNextTick = false;
                _pendingConversationScreen = null;

                if (!OpenConfigurationInternal(ConfigurationHost.Conversation, conversationScreen))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[CGU] Preset configuration could not be opened."));
                }
            }

            if (_host != ConfigurationHost.None)
                UpdateHostVisibility(ScreenManager.TopScreen);

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
            SetLayerInteraction(false);

            GearPresetConfigViewModel viewModel = _viewModel;
            GauntletMovieIdentifier movie = _movie;
            _viewModel = null;
            _movie = null;
            ResetHost();

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

        private void ResetHost()
        {
            _host = ConfigurationHost.None;
            _hostScreen = null;
            _isHostScreenVisible = false;
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
