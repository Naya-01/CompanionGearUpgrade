using System;

namespace CompanionGearUpgrades.Domain
{
    /// <summary>
    /// Shared invariant for preset prices. Validation and save-data
    /// sanitization remain explicit because callers intentionally use
    /// different policies for invalid input.
    /// </summary>
    internal static class GearPresetPricePolicy
    {
        internal static bool IsValid(int price)
        {
            return price >= 0;
        }

        internal static int NormalizeForStorage(int price)
        {
            return Math.Max(0, price);
        }
    }
}
