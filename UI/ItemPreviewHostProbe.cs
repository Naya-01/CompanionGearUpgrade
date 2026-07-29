using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;

namespace CompanionGearUpgrades.UI
{
    /// <summary>
    /// Reads the native tableau readiness boundary shared by both modal views.
    /// ItemPreviewVM.Open must remain deferred until these checks succeed.
    /// </summary>
    internal static class ItemPreviewHostProbe
    {
        internal static void GetState(
            GauntletLayer layer,
            string widgetId,
            out bool isHostReady,
            out bool isTextureReady)
        {
            isHostReady = false;
            isTextureReady = false;

            Widget root = layer?.UIContext?.Root;
            if (root == null)
                return;

            var previewWidgets = root.FindChildrenWithId<ItemTableauWidget>(widgetId, true);
            if (previewWidgets == null || previewWidgets.Count == 0)
                return;

            ItemTableauWidget previewHost = previewWidgets[0];
            isHostReady = previewHost != null &&
                previewHost.ConnectedToRoot &&
                previewHost.IsRecursivelyVisible() &&
                previewHost.TextureProvider != null;
            isTextureReady = isHostReady &&
                previewHost.Texture != null &&
                previewHost.Texture.IsValid;
        }
    }
}
