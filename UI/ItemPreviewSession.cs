using System;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;

namespace CompanionGearUpgrades.UI
{
    /// <summary>
    /// Owns the native <see cref="ItemPreviewVM"/> for one Gauntlet movie.
    /// The caller supplies only its current screen state and item lookup; the
    /// session keeps opening the preview behind the actual tableau readiness
    /// boundary and releases native resources when the movie closes.
    /// </summary>
    internal sealed class ItemPreviewSession
    {
        private const int PreviewOpenDelayTicks = 1;
        private const int PreviewTextureSettleTicks = 2;
        private const int PreviewTextureTimeoutTicks = 30;
        private const int MaxPreviewOpenAttempts = 3;

        private readonly Func<string, ItemObject> _findItem;
        private readonly Func<bool> _canRetryAfterClose;
        private readonly Action _stateChanged;
        private readonly Action _previewChanged;
        private readonly string _unavailableState;

        private ItemPreviewVM _itemPreview;
        private string _requestedItemId;
        private string _openedItemId;
        private string _readyItemId;
        private int _openDelayTicks;
        private int _openAttempt;
        private int _textureSettleTicks;
        private int _textureWaitTicks;
        private bool _isReleasing;

        public ItemPreviewSession(
            Func<string, ItemObject> findItem,
            Func<bool> canRetryAfterClose,
            Action stateChanged,
            Action previewChanged,
            string initialState,
            string unavailableState)
        {
            _findItem = findItem ?? throw new ArgumentNullException(nameof(findItem));
            _canRetryAfterClose = canRetryAfterClose ?? throw new ArgumentNullException(nameof(canRetryAfterClose));
            _stateChanged = stateChanged ?? throw new ArgumentNullException(nameof(stateChanged));
            _previewChanged = previewChanged ?? throw new ArgumentNullException(nameof(previewChanged));
            _unavailableState = unavailableState ?? throw new ArgumentNullException(nameof(unavailableState));
            StateText = initialState ?? string.Empty;
            _itemPreview = new ItemPreviewVM(OnItemPreviewClosed);
        }

        public ItemCollectionElementViewModel PreviewTableau => _itemPreview?.ItemTableau;

        public string StateText { get; private set; }

        public bool HasPreviewItem =>
            !string.IsNullOrEmpty(_readyItemId) &&
            string.Equals(_readyItemId, _requestedItemId, StringComparison.Ordinal);

        /// <summary>
        /// Queues an item for rendering. The actual native Open call remains
        /// deferred until <see cref="Tick"/> observes a ready tableau host.
        /// </summary>
        public void SetItem(ItemObject item, string noItemState)
        {
            string itemId = item != null ? item.StringId : null;
            if (string.Equals(_requestedItemId, itemId, StringComparison.Ordinal) &&
                (!string.IsNullOrEmpty(_openedItemId) || _openDelayTicks > 0))
            {
                return;
            }

            _requestedItemId = itemId;
            ResetTrackingForRequestedItem();

            if (item == null)
            {
                CloseAndClearNativePreview();
                SetState(noItemState);
                NotifyPreviewChanged();
                return;
            }

            _openDelayTicks = PreviewOpenDelayTicks;
            SetState("Loading 3D preview...");
            NotifyPreviewChanged();
        }

        /// <summary>
        /// Re-arms the selected item after its ItemTableauWidget has been
        /// recreated while returning to the items page.
        /// </summary>
        public void ArmHostInitialization()
        {
            if (_itemPreview == null || string.IsNullOrEmpty(_requestedItemId))
                return;

            _openedItemId = null;
            _readyItemId = null;
            _openAttempt = 0;
            _textureSettleTicks = 0;
            _textureWaitTicks = 0;
            _openDelayTicks = PreviewOpenDelayTicks;
            SetState("Loading 3D preview...");
            NotifyPreviewChanged();
        }

        /// <summary>
        /// Called only by the Gauntlet host, after it has checked the native
        /// ItemTableauWidget. This protects ItemPreviewVM.Open from running
        /// before the texture provider exists.
        /// </summary>
        public void Tick(bool isSessionActive, bool isPreviewHostReady, bool isPreviewTextureReady)
        {
            if (!isSessionActive || _itemPreview == null || string.IsNullOrEmpty(_requestedItemId))
                return;

            if (!isPreviewHostReady)
            {
                SetState("Preparing the 3D preview context...");
                return;
            }

            if (string.Equals(_openedItemId, _requestedItemId, StringComparison.Ordinal))
            {
                if (string.Equals(_readyItemId, _requestedItemId, StringComparison.Ordinal))
                    return;

                if (_textureSettleTicks > 0)
                {
                    _textureSettleTicks--;
                    return;
                }

                if (isPreviewTextureReady)
                {
                    _readyItemId = _requestedItemId;
                    SetState("3D preview ready.");
                    NotifyPreviewChanged();
                    return;
                }

                _textureWaitTicks++;
                if (_textureWaitTicks >= PreviewTextureTimeoutTicks)
                    ScheduleRetryOrReportFailure();
                return;
            }

            if (_openDelayTicks > 0)
            {
                _openDelayTicks--;
                return;
            }

            if (_openAttempt < MaxPreviewOpenAttempts)
                OpenRequestedPreview();
        }

        public void Prepare(string state)
        {
            ResetTracking();
            CloseAndClearNativePreview();
            SetState(state);
            NotifyPreviewChanged();
        }

        public void Release(string state)
        {
            ResetTracking();
            CloseAndClearNativePreview();
            SetState(state);
            NotifyPreviewChanged();
        }

        /// <summary>
        /// Finalizes the native preview exactly once when its owning movie is
        /// released. Call <see cref="Release"/> first when a visible state
        /// update is required.
        /// </summary>
        public void FinalizeSession()
        {
            if (_itemPreview == null)
                return;

            _isReleasing = true;
            try
            {
                _itemPreview.OnFinalize();
            }
            finally
            {
                _isReleasing = false;
                _itemPreview = null;
            }
        }

        private void OpenRequestedPreview()
        {
            ItemObject item = _findItem(_requestedItemId);
            if (item == null)
            {
                SetState("The selected item is no longer available for preview.");
                NotifyPreviewChanged();
                return;
            }

            try
            {
                _openAttempt++;
                ClearNativePreviewTableau();
                _itemPreview.Open(new EquipmentElement(item));
                _openedItemId = _requestedItemId;
                _readyItemId = null;
                _textureSettleTicks = PreviewTextureSettleTicks;
                _textureWaitTicks = 0;
                SetState("Rendering 3D preview...");
                NotifyPreviewChanged();
            }
            catch (Exception)
            {
                ScheduleRetryOrReportFailure();
            }
        }

        private void ScheduleRetryOrReportFailure()
        {
            _openedItemId = null;
            _readyItemId = null;
            _textureSettleTicks = 0;
            _textureWaitTicks = 0;
            ClearNativePreviewTableau();

            if (_openAttempt < MaxPreviewOpenAttempts)
            {
                _openDelayTicks = PreviewOpenDelayTicks;
                SetState("Retrying 3D preview...");
                NotifyPreviewChanged();
                return;
            }

            SetState(_unavailableState);
            NotifyPreviewChanged();
        }

        private void ResetTrackingForRequestedItem()
        {
            _openedItemId = null;
            _readyItemId = null;
            _openDelayTicks = 0;
            _openAttempt = 0;
            _textureSettleTicks = 0;
            _textureWaitTicks = 0;
        }

        private void ResetTracking()
        {
            _requestedItemId = null;
            ResetTrackingForRequestedItem();
        }

        private void CloseAndClearNativePreview()
        {
            if (_itemPreview == null)
                return;

            _isReleasing = true;
            try
            {
                if (_itemPreview.IsSelected)
                    _itemPreview.Close();

                ClearNativePreviewTableau();
            }
            finally
            {
                _isReleasing = false;
            }
        }

        private void ClearNativePreviewTableau()
        {
            if (_itemPreview?.ItemTableau != null)
                _itemPreview.ItemTableau.StringId = string.Empty;
        }

        private void OnItemPreviewClosed()
        {
            _openedItemId = null;
            _readyItemId = null;
            _textureSettleTicks = 0;
            _textureWaitTicks = 0;
            NotifyPreviewChanged();

            if (_isReleasing || !_canRetryAfterClose() || string.IsNullOrEmpty(_requestedItemId))
                return;

            _openAttempt = 0;
            _openDelayTicks = PreviewOpenDelayTicks;
            SetState("Reinitializing 3D preview...");
        }

        private void SetState(string state)
        {
            string nextState = state ?? string.Empty;
            if (string.Equals(StateText, nextState, StringComparison.Ordinal))
                return;

            StateText = nextState;
            _stateChanged();
        }

        private void NotifyPreviewChanged()
        {
            _previewChanged();
        }
    }
}
