using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Services;
using SandBox.GauntletUI;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
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

        private readonly CompanionGearUpgradeService _service;

        private EquipmentConfigGlobalLayer _globalLayer;
        private GauntletLayer _layer;
        private GauntletMovieIdentifier _movie;
        private GearPresetConfigViewModel _viewModel;
        private ConfigurationHost _host;
        private ScreenBase _hostScreen;
        private bool _isHostScreenVisible;
        private bool _isLayerModal;
        private bool _releaseMovieOnNextTick;
        private ScreenBase _pendingConversationScreen;

        public EquipmentConfigView(CompanionGearUpgradeService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        // Compatibility overload: the service owns persistence now, but older
        // callers can keep passing the shared override store.
        public EquipmentConfigView(
            CompanionGearUpgradeService service,
            GearPresetOverrides overrides)
            : this(service)
        {
            if (overrides == null)
                throw new ArgumentNullException(nameof(overrides));
        }

        public void Initialize()
        {
            if (_globalLayer != null)
                return;

            _layer = new GauntletLayer(LayerName, 1000, false);
            _globalLayer = new EquipmentConfigGlobalLayer(_layer, OnGauntletTick);

            ScreenManager.OnPushScreen += OnPushScreen;
            ScreenManager.OnPopScreen += OnPopScreen;
            ScreenManager.AddGlobalLayer(_globalLayer, false);
        }

        public void Dispose()
        {
            ScreenManager.OnPushScreen -= OnPushScreen;
            ScreenManager.OnPopScreen -= OnPopScreen;

            _pendingConversationScreen = null;
            SetLayerInteraction(false);

            if (_globalLayer != null)
                ScreenManager.RemoveGlobalLayer(_globalLayer);

            ReleaseConfigurationMovie();

            _layer = null;
            _globalLayer = null;

        }

        public bool OpenClanConfiguration()
        {
            ScreenBase screen = ScreenManager.TopScreen;
            if (!IsClanScreen(screen))
                return false;

            return OpenConfigurationInternal(ConfigurationHost.Clan, screen);
        }

        public bool OpenConversationConfiguration()
        {
            ScreenBase screen = ScreenManager.TopScreen;
            if (_layer == null || screen == null || !IsConversationInProgress())
                return false;

            // The dialogue consequence is still running here. Loading on the
            // next global-layer tick lets Bannerlord finish DoOptionContinue
            // before this layer takes focus.
            _pendingConversationScreen = screen;
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
                        new GearPresetConfigViewModel(_service, SetWindowLayerState);
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
            GauntletModalLayerInteraction.SetModal(_layer, ref _isLayerModal, isModal);
        }

        private void OnGauntletTick()
        {
            if (_pendingConversationScreen != null)
            {
                ScreenBase conversationScreen = _pendingConversationScreen;
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

            bool isHostReady;
            bool isTextureReady;
            ItemPreviewHostProbe.GetState(
                _layer,
                "CGUPreviewTableau",
                out isHostReady,
                out isTextureReady);

            _viewModel?.OnGauntletTick(isHostReady, isTextureReady);
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

    }
}
