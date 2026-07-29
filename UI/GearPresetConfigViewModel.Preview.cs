using TaleWorlds.Core;

namespace CompanionGearUpgrades.UI
{
    public sealed partial class GearPresetConfigViewModel
    {
        /// <summary>
        /// Called by the owning Gauntlet layer only after it has inspected the
        /// direct ItemTableauWidget. The shared session performs the delayed
        /// native ItemPreviewVM.Open when that context is ready.
        /// </summary>
        public void OnGauntletTick(bool isPreviewHostReady, bool isPreviewTextureReady)
        {
            _previewSession.Tick(
                IsWindowOpen && _page == Page.Items,
                isPreviewHostReady,
                isPreviewTextureReady);
        }

        private void SetPreviewItem(ItemObject item)
        {
            _previewSession.SetItem(item, "Hover or select an item to preview it.");
        }

        private void ArmPreviewHostInitialization()
        {
            _previewSession.ArmHostInitialization();
        }

        private void PreparePreviewSession()
        {
            _previewSession.Prepare("Preview will load when an item is selected.");
        }

        private void ReleasePreviewSession()
        {
            _previewSession.Release("Preview is closed.");
        }

        private bool CanRetryPreviewAfterClose()
        {
            return IsWindowOpen && _page == Page.Items;
        }

        private void NotifyPreviewStateChanged()
        {
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

        public override void OnFinalize()
        {
            ClearItemInspection();
            ReleasePreviewSession();
            _previewSession.FinalizeSession();

            base.OnFinalize();
        }
    }
}
