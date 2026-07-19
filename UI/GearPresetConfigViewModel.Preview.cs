using System;
using TaleWorlds.Core;

namespace CompanionGearUpgrades.UI
{
    public sealed partial class GearPresetConfigViewModel
    {
        /// <summary>
        /// Called by the owning Gauntlet layer. ItemPreviewVM.Open is allowed
        /// only after the visible ItemTableauWidget has a native texture
        /// provider; this is the actual readiness boundary for the 3D host.
        /// </summary>
        public void OnGauntletTick(bool isPreviewHostReady, bool isPreviewTextureReady)
        {
            if (!IsWindowOpen || _page != Page.Items || _itemPreview == null ||
                string.IsNullOrEmpty(_requestedPreviewItemId))
                return;

            if (!isPreviewHostReady)
            {
                SetPreviewState("Preparing the 3D preview context...");
                return;
            }

            if (string.Equals(_openedPreviewItemId, _requestedPreviewItemId, StringComparison.Ordinal))
            {
                if (string.Equals(_readyPreviewItemId, _requestedPreviewItemId, StringComparison.Ordinal))
                    return;

                if (_previewTextureSettleTicks > 0)
                {
                    _previewTextureSettleTicks--;
                    return;
                }

                if (isPreviewTextureReady)
                {
                    _readyPreviewItemId = _requestedPreviewItemId;
                    SetPreviewState("3D preview ready.");
                    NotifyPreviewChanged();
                    return;
                }

                _previewTextureWaitTicks++;
                if (_previewTextureWaitTicks >= PreviewTextureTimeoutTicks)
                    SchedulePreviewRetryOrReportFailure();
                return;
            }

            if (_previewOpenDelayTicks > 0)
            {
                _previewOpenDelayTicks--;
                return;
            }

            if (!string.Equals(_openedPreviewItemId, _requestedPreviewItemId, StringComparison.Ordinal) &&
                _previewOpenAttempt < MaxPreviewOpenAttempts)
                OpenRequestedPreview();
        }

        private void SetPreviewItem(ItemObject item)
        {
            string itemId = item != null ? item.StringId : null;
            if (string.Equals(_requestedPreviewItemId, itemId, StringComparison.Ordinal) &&
                (!string.IsNullOrEmpty(_openedPreviewItemId) || _previewOpenDelayTicks > 0))
                return;

            _requestedPreviewItemId = itemId;
            _openedPreviewItemId = null;
            _readyPreviewItemId = null;
            _previewOpenAttempt = 0;
            _previewTextureSettleTicks = 0;
            _previewTextureWaitTicks = 0;

            if (item == null)
            {
                _previewOpenDelayTicks = 0;
                CloseAndClearNativePreview();

                SetPreviewState("Hover or select an item to preview it.");
                NotifyPreviewChanged();
                return;
            }

            _previewOpenDelayTicks = PreviewOpenDelayTicks;
            SetPreviewState("Loading 3D preview...");
            NotifyPreviewChanged();
        }

        private void ArmPreviewHostInitialization()
        {
            if (_itemPreview == null || string.IsNullOrEmpty(_requestedPreviewItemId))
                return;

            _openedPreviewItemId = null;
            _readyPreviewItemId = null;
            _previewOpenAttempt = 0;
            _previewTextureSettleTicks = 0;
            _previewTextureWaitTicks = 0;
            _previewOpenDelayTicks = PreviewOpenDelayTicks;
            SetPreviewState("Loading 3D preview...");
            NotifyPreviewChanged();
        }

        private void OpenRequestedPreview()
        {
            ItemObject item = FindItem(_requestedPreviewItemId);
            if (item == null)
            {
                SetPreviewState("The selected item is no longer available for preview.");
                NotifyPreviewChanged();
                return;
            }

            try
            {
                _previewOpenAttempt++;
                ClearNativePreviewTableau();
                _itemPreview.Open(new EquipmentElement(item));
                _openedPreviewItemId = _requestedPreviewItemId;
                _readyPreviewItemId = null;
                _previewTextureSettleTicks = PreviewTextureSettleTicks;
                _previewTextureWaitTicks = 0;
                SetPreviewState("Rendering 3D preview...");
                NotifyPreviewChanged();
            }
            catch (Exception)
            {
                SchedulePreviewRetryOrReportFailure();
            }
        }

        private void SchedulePreviewRetryOrReportFailure()
        {
            _openedPreviewItemId = null;
            _readyPreviewItemId = null;
            _previewTextureSettleTicks = 0;
            _previewTextureWaitTicks = 0;
            ClearNativePreviewTableau();

            if (_previewOpenAttempt < MaxPreviewOpenAttempts)
            {
                _previewOpenDelayTicks = PreviewOpenDelayTicks;
                SetPreviewState("Retrying 3D preview...");
                NotifyPreviewChanged();
                return;
            }

            SetPreviewState("3D preview is temporarily unavailable. Hover the item again to retry.");
            NotifyPreviewChanged();
        }

        private void PreparePreviewSession()
        {
            ResetPreviewTracking();
            CloseAndClearNativePreview();

            SetPreviewState("Preview will load when an item is selected.");
            NotifyPreviewChanged();
        }

        private void ReleasePreviewSession()
        {
            ResetPreviewTracking();
            CloseAndClearNativePreview();

            SetPreviewState("Preview is closed.");
            NotifyPreviewChanged();
        }

        private void ResetPreviewTracking()
        {
            _requestedPreviewItemId = null;
            _openedPreviewItemId = null;
            _readyPreviewItemId = null;
            _previewOpenDelayTicks = 0;
            _previewOpenAttempt = 0;
            _previewTextureSettleTicks = 0;
            _previewTextureWaitTicks = 0;
        }

        private void CloseAndClearNativePreview()
        {
            if (_itemPreview == null)
                return;

            _isReleasingPreview = true;
            try
            {
                if (_itemPreview.IsSelected)
                    _itemPreview.Close();
                ClearNativePreviewTableau();
            }
            finally
            {
                _isReleasingPreview = false;
            }
        }

        private void ClearNativePreviewTableau()
        {
            if (_itemPreview?.ItemTableau != null)
                _itemPreview.ItemTableau.StringId = string.Empty;
        }

        private void SetPreviewState(string state)
        {
            if (string.Equals(_previewStateText, state, StringComparison.Ordinal))
                return;

            _previewStateText = state;
            OnPropertyChanged(nameof(PreviewStateText));
        }

        private void NotifyPreviewChanged()
        {
            OnPropertyChanged(nameof(HasPreviewItem));
        }

        private void ClearItemInspection()
        {
            ClearHoveredCandidate();
            _inspectedItem = null;
            _inspectionTooltip.SetItem(null);
            _configuredTooltip.SetItem(null);
            _hasComparison = false;
            SetPreviewItem(null);

            OnPropertyChanged(nameof(HasInspectionItem));
            OnPropertyChanged(nameof(HasSingleInspection));
            OnPropertyChanged(nameof(HasHoveredComparison));
        }

        private void OnItemPreviewClosed()
        {
            _openedPreviewItemId = null;
            _readyPreviewItemId = null;
            _previewTextureSettleTicks = 0;
            _previewTextureWaitTicks = 0;
            NotifyPreviewChanged();

            if (_isReleasingPreview || !IsWindowOpen || _page != Page.Items ||
                string.IsNullOrEmpty(_requestedPreviewItemId))
                return;

            _previewOpenAttempt = 0;
            _previewOpenDelayTicks = PreviewOpenDelayTicks;
            SetPreviewState("Reinitializing 3D preview...");
        }

        public override void OnFinalize()
        {
            ClearItemInspection();
            ReleasePreviewSession();

            if (_itemPreview != null)
            {
                _isReleasingPreview = true;
                try
                {
                    _itemPreview.OnFinalize();
                }
                finally
                {
                    _isReleasingPreview = false;
                    _itemPreview = null;
                }
            }

            base.OnFinalize();
        }

    }
}
