using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace CompanionGearUpgrades.UI
{
    /// <summary>
    /// Applies the shared focus and input-restriction policy for CGU modal
    /// Gauntlet layers.
    /// </summary>
    internal static class GauntletModalLayerInteraction
    {
        internal static void SetModal(
            GauntletLayer layer,
            ref bool isCurrentlyModal,
            bool isModal)
        {
            if (layer == null || isCurrentlyModal == isModal)
                return;

            isCurrentlyModal = isModal;
            if (isModal)
            {
                layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
                layer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(layer);
            }
            else
            {
                layer.IsFocusLayer = false;
                layer.InputRestrictions.ResetInputRestrictions();
                ScreenManager.TryLoseFocus(layer);
            }
        }
    }
}
