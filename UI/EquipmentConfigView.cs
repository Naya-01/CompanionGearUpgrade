using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace CompanionGearUpgrades.UI
{
    /// <summary>
    /// Hosts the equipment movie as a global Gauntlet layer. ScreenManager
    /// events decide when the button is visible, avoiding campaign-tick timing
    /// and avoiding edits to Native's ClanScreen.xml.
    /// </summary>
    public sealed class EquipmentConfigView
    {
        private const string LayerName = "CompanionGearUpgradeClanEquipment";
        private static EquipmentConfigView _current;

        private GlobalLayer _globalLayer;
        private GauntletLayer _layer;
        private GauntletMovieIdentifier _movie;
        private EquipmentConfigViewModel _viewModel;
        private bool _isClanScreen;

        public void Initialize()
        {
            if (_globalLayer != null)
                return;

            _current = this;

            _layer = new GauntletLayer(LayerName, 1000, false);
            _viewModel = new EquipmentConfigViewModel(SetWindowLayerState);
            _movie = _layer.LoadMovie("EquipmentConfigWindow", _viewModel);

            _globalLayer = new EquipmentGlobalLayer(_layer);

            ScreenManager.OnPushScreen += OnPushScreen;
            ScreenManager.OnPopScreen += OnPopScreen;
            ScreenManager.AddGlobalLayer(_globalLayer, false);

            UpdateScreenVisibility(ScreenManager.TopScreen);
        }

        public void Update()
        {
            // Compatibility entry point for the existing campaign behavior.
            // The primary path is ScreenManager's push/pop events.
            if (_globalLayer == null)
                Initialize();
            else
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

        public static void OpenConfiguration()
        {
            if (_current == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[CGU] Erreur : EquipmentConfigView n'est pas initialisée."));
                return;
            }

            if (_current._viewModel == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[CGU] Erreur : EquipmentConfigViewModel est nul."));
                return;
            }

            _current._viewModel.ExecuteOpenConfiguration();
            InformationManager.DisplayMessage(new InformationMessage("[CGU] EquipmentConfigWindow demandée."));
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
                _layer.IsFocusLayer = _isClanScreen;
                _layer.InputRestrictions.SetInputRestrictions(false, InputUsageMask.All);
            }
        }

        private void SetWindowLayerState(bool isOpen)
        {
            if (_layer == null)
                return;

            _layer.IsFocusLayer = _isClanScreen || isOpen;
            _layer.InputRestrictions.SetInputRestrictions(isOpen, InputUsageMask.All);
        }

        private sealed class EquipmentGlobalLayer : GlobalLayer
        {
            public EquipmentGlobalLayer(ScreenLayer layer)
            {
                Layer = layer;
            }
        }
    }
}
