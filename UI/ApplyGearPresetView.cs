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
    /// Modal host for <see cref="ApplyGearPresetViewModel"/>. The host owns
    /// the Gauntlet movie and all native preview lifetime; callers only pass
    /// the hero to configure through <see cref="Open"/>.
    /// </summary>
    public sealed class ApplyGearPresetView
    {
        private const string LayerName = "CompanionGearUpgradeApplyPreset";
        private const string MovieName = "ApplyGearPresetWindow";

        private readonly CompanionGearUpgradeService _service;

        private EquipmentConfigGlobalLayer _globalLayer;
        private GauntletLayer _layer;
        private GauntletMovieIdentifier _movie;
        private ApplyGearPresetViewModel _viewModel;
        private Hero _pendingTarget;
        private ScreenBase _pendingHostScreen;
        private ScreenBase _hostScreen;
        private bool _isLayerModal;
        private bool _releaseMovieOnNextTick;

        public ApplyGearPresetView(CompanionGearUpgradeService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public void Initialize()
        {
            if (_globalLayer != null)
                return;

            _layer = new GauntletLayer(LayerName, 1001, false);
            _globalLayer = new EquipmentConfigGlobalLayer(_layer, OnGauntletTick);

            ScreenManager.OnPushScreen += OnPushScreen;
            ScreenManager.OnPopScreen += OnPopScreen;
            ScreenManager.AddGlobalLayer(_globalLayer, false);
        }

        public void Dispose()
        {
            ScreenManager.OnPushScreen -= OnPushScreen;
            ScreenManager.OnPopScreen -= OnPopScreen;

            _pendingTarget = null;
            _pendingHostScreen = null;
            SetLayerInteraction(false);

            if (_globalLayer != null)
                ScreenManager.RemoveGlobalLayer(_globalLayer);

            ReleaseMovie();
            _layer = null;
            _globalLayer = null;
        }

        /// <summary>
        /// Queues the modal for the next Gauntlet tick, after the Clan screen
        /// has completed the button command that opened it.
        /// </summary>
        public bool Open(Hero target)
        {
            if (_layer == null || target == null || !_service.IsHeroEligibleForPresetApplication(target))
                return false;

            ScreenBase hostScreen = ScreenManager.TopScreen;
            if (!(hostScreen is GauntletClanScreen) || _pendingTarget != null || _viewModel != null)
                return false;

            _pendingTarget = target;
            _pendingHostScreen = hostScreen;
            return true;
        }

        private void OnPushScreen(ScreenBase screen)
        {
            UpdateHostVisibility(screen);
        }

        private void OnPopScreen(ScreenBase screen)
        {
            UpdateHostVisibility(ScreenManager.TopScreen);
        }

        private void UpdateHostVisibility(ScreenBase currentScreen)
        {
            if (_hostScreen == null || ReferenceEquals(_hostScreen, currentScreen))
                return;

            _viewModel?.CloseFromHost();
            RequestClose();
        }

        private void OnGauntletTick()
        {
            OpenPendingRequest();
            UpdateHostVisibility(ScreenManager.TopScreen);

            if (_releaseMovieOnNextTick)
            {
                ReleaseMovie();
                return;
            }

            if (_viewModel == null)
                return;

            if (!_viewModel.IsWindowOpen)
            {
                RequestClose();
                return;
            }

            bool isHostReady;
            bool isTextureReady;
            ItemPreviewHostProbe.GetState(
                _layer,
                "CGUApplyPresetPreviewTableau",
                out isHostReady,
                out isTextureReady);

            _viewModel.OnGauntletTick(isHostReady, isTextureReady);
        }

        private void OpenPendingRequest()
        {
            if (_pendingTarget == null)
                return;

            Hero target = _pendingTarget;
            ScreenBase hostScreen = _pendingHostScreen;
            _pendingTarget = null;
            _pendingHostScreen = null;

            if (!ReferenceEquals(hostScreen, ScreenManager.TopScreen) ||
                !OpenInternal(target, hostScreen))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[CGU] Gear preset application could not be opened."));
            }
        }

        private bool OpenInternal(Hero target, ScreenBase hostScreen)
        {
            if (_layer == null || hostScreen == null ||
                !(hostScreen is GauntletClanScreen) ||
                !ReferenceEquals(hostScreen, ScreenManager.TopScreen) ||
                !_service.IsHeroEligibleForPresetApplication(target))
            {
                return false;
            }

            ApplyGearPresetViewModel createdViewModel = null;
            GauntletMovieIdentifier createdMovie = null;
            try
            {
                createdViewModel = new ApplyGearPresetViewModel(_service, RequestClose);
                createdMovie = _layer.LoadMovie(MovieName, createdViewModel);
                _viewModel = createdViewModel;
                _movie = createdMovie;
                _hostScreen = hostScreen;

                if (!_viewModel.Open(target))
                    throw new InvalidOperationException("The target hero is no longer eligible.");

                SetLayerInteraction(true);
                return true;
            }
            catch
            {
                _viewModel = null;
                _movie = null;
                _hostScreen = null;
                _releaseMovieOnNextTick = false;
                SetLayerInteraction(false);

                try
                {
                    createdViewModel?.OnFinalize();
                }
                catch
                {
                }

                try
                {
                    if (_layer != null && createdMovie != null)
                        _layer.ReleaseMovie(createdMovie);
                }
                catch
                {
                }

                return false;
            }
        }

        private void RequestClose()
        {
            SetLayerInteraction(false);
            _releaseMovieOnNextTick = _movie != null;
        }

        private void SetLayerInteraction(bool isModal)
        {
            GauntletModalLayerInteraction.SetModal(_layer, ref _isLayerModal, isModal);
        }

        private void ReleaseMovie()
        {
            _releaseMovieOnNextTick = false;
            SetLayerInteraction(false);

            ApplyGearPresetViewModel viewModel = _viewModel;
            GauntletMovieIdentifier movie = _movie;
            _viewModel = null;
            _movie = null;
            _hostScreen = null;

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
    }
}
