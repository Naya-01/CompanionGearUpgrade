using System;
using TaleWorlds.ScreenSystem;

namespace CompanionGearUpgrades.UI
{
    internal sealed class EquipmentConfigGlobalLayer : GlobalLayer
    {
        private readonly Action _onTick;

        public EquipmentConfigGlobalLayer(ScreenLayer layer, Action onTick)
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
