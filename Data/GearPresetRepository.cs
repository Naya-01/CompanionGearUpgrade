using CompanionGearUpgrades.Domain;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace CompanionGearUpgrades.Data
{
    /// <summary>
    /// Immutable description of a role exposed by the configurator. Only the
    /// built-in roles are represented by <see cref="GearRole"/>; custom roles
    /// use a stable string identifier so old enum-based override keys remain
    /// valid in existing campaign saves.
    /// </summary>
    public sealed class GearRoleDefinition
    {
        public GearRoleDefinition(string id, string name, bool isDefaultRole)
        {
            Id = id;
            Name = name;
            IsDefaultRole = isDefaultRole;
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public bool IsDefaultRole { get; private set; }

        // Concise alias for Gauntlet-facing callers.
        public bool IsDefault => IsDefaultRole;
    }

    public static class GearPresetRepository
    {
        public const int TierCount = 3;
        public const int MaxRoleCount = 10;
        public const int DefaultRoleCount = 3;
        public const int MaxCustomRoleCount = MaxRoleCount - DefaultRoleCount;

        internal const string CustomRoleIdPrefix = "custom-";

        private static readonly GearRoleDefinition[] DefaultRoleDefinitions =
        {
            new GearRoleDefinition(nameof(GearRole.Archer), "Archer", true),
            new GearRoleDefinition(nameof(GearRole.Infantry), "Infantry", true),
            new GearRoleDefinition(nameof(GearRole.Lancer), "Lancer", true)
        };

        /// <summary>
        /// Returns a fresh list so the UI can stage changes without mutating
        /// the campaign-backed custom-role dictionary.
        /// </summary>
        public static List<GearRoleDefinition> GetDefaultRoles()
        {
            return new List<GearRoleDefinition>(DefaultRoleDefinitions);
        }

        public static List<GearRoleDefinition> GetRoles(Dictionary<string, string> customRoleNames)
        {
            var roles = GetDefaultRoles();
            roles.AddRange(GetCustomRoles(customRoleNames));
            return roles;
        }

        public static List<GearRoleDefinition> GetCustomRoles(Dictionary<string, string> customRoleNames)
        {
            var roles = new List<GearRoleDefinition>();
            if (customRoleNames == null)
                return roles;

            foreach (KeyValuePair<string, string> pair in customRoleNames)
            {
                string normalizedName;
                string ignoredError;
                if (!IsCustomRoleId(pair.Key) ||
                    !TryNormalizeCustomRoleName(pair.Value, out normalizedName, out ignoredError))
                {
                    continue;
                }

                roles.Add(new GearRoleDefinition(pair.Key, normalizedName, false));
            }

            roles.Sort(CompareCustomRoles);
            return roles;
        }

        public static string GetRoleId(GearRole role)
        {
            return role.ToString();
        }

        public static bool TryGetDefaultRole(string roleId, out GearRole role)
        {
            if (string.Equals(roleId, nameof(GearRole.Archer), StringComparison.Ordinal))
            {
                role = GearRole.Archer;
                return true;
            }

            if (string.Equals(roleId, nameof(GearRole.Infantry), StringComparison.Ordinal))
            {
                role = GearRole.Infantry;
                return true;
            }

            if (string.Equals(roleId, nameof(GearRole.Lancer), StringComparison.Ordinal))
            {
                role = GearRole.Lancer;
                return true;
            }

            role = default(GearRole);
            return false;
        }

        public static bool IsDefaultRoleId(string roleId)
        {
            GearRole ignoredRole;
            return TryGetDefaultRole(roleId, out ignoredRole);
        }

        public static bool IsCustomRoleId(string roleId)
        {
            return !string.IsNullOrEmpty(roleId) &&
                roleId.StartsWith(CustomRoleIdPrefix, StringComparison.Ordinal) &&
                roleId.IndexOf(':') < 0;
        }

        public static bool IsValidTier(int tier)
        {
            return tier >= 1 && tier <= TierCount;
        }

        public static bool TryNormalizeCustomRoleName(string name, out string normalizedName, out string error)
        {
            normalizedName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            if (string.IsNullOrEmpty(normalizedName))
            {
                error = "A role name is required.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Makes data restored from a campaign save safe to use. It only
        /// accepts generated custom identifiers, normalizes names, keeps names
        /// unique (including built-ins), and enforces the ten-role cap.
        /// </summary>
        public static void NormalizeCustomRoles(Dictionary<string, string> customRoleNames)
        {
            if (customRoleNames == null)
                return;

            var entries = new List<KeyValuePair<string, string>>(customRoleNames);
            entries.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));

            var normalized = new Dictionary<string, string>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GearRoleDefinition defaultRole in DefaultRoleDefinitions)
                names.Add(defaultRole.Name);

            foreach (KeyValuePair<string, string> entry in entries)
            {
                if (normalized.Count >= MaxCustomRoleCount || !IsCustomRoleId(entry.Key))
                    continue;

                string normalizedName;
                string ignoredError;
                if (!TryNormalizeCustomRoleName(entry.Value, out normalizedName, out ignoredError) ||
                    !names.Add(normalizedName))
                {
                    continue;
                }

                normalized.Add(entry.Key, normalizedName);
            }

            customRoleNames.Clear();
            foreach (KeyValuePair<string, string> entry in normalized)
                customRoleNames.Add(entry.Key, entry.Value);
        }

        /// <summary>
        /// Validates the custom part of a staged role list. Defaults are always
        /// supplied by this repository and cannot be changed through this API.
        /// </summary>
        public static bool TryValidateCustomRoles(
            IEnumerable<GearRoleDefinition> customRoles,
            out List<GearRoleDefinition> validatedRoles,
            out string error)
        {
            validatedRoles = new List<GearRoleDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GearRoleDefinition defaultRole in DefaultRoleDefinitions)
                names.Add(defaultRole.Name);

            if (customRoles == null)
            {
                error = null;
                return true;
            }

            foreach (GearRoleDefinition role in customRoles)
            {
                if (role == null)
                {
                    error = "A custom role is invalid.";
                    return false;
                }

                if (role.IsDefaultRole || !IsCustomRoleId(role.Id))
                {
                    error = "Default roles cannot be changed or removed.";
                    return false;
                }

                string normalizedName;
                string nameError;
                if (!TryNormalizeCustomRoleName(role.Name, out normalizedName, out nameError))
                {
                    error = nameError;
                    return false;
                }

                if (!ids.Add(role.Id))
                {
                    error = "Each custom role must have a unique identifier.";
                    return false;
                }

                if (!names.Add(normalizedName))
                {
                    error = "Role names must be unique.";
                    return false;
                }

                if (validatedRoles.Count >= MaxCustomRoleCount)
                {
                    error = $"You can create at most {MaxRoleCount} roles.";
                    return false;
                }

                validatedRoles.Add(new GearRoleDefinition(role.Id, normalizedName, false));
            }

            error = null;
            return true;
        }

        private static int CompareCustomRoles(GearRoleDefinition left, GearRoleDefinition right)
        {
            int byName = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
            return byName != 0 ? byName : StringComparer.Ordinal.Compare(left.Id, right.Id);
        }

        public static Dictionary<(GearRole role, int tier), GearPreset> BuildPresets()
        {
            return new Dictionary<(GearRole, int), GearPreset>
            {
                // ARCHER
                [(GearRole.Archer, 1)] = new GearPreset(3000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "hunting_bow",
                    [EquipmentIndex.Weapon1] = "default_arrows",
                    [EquipmentIndex.Weapon2] = "simple_sabre_sword_t2",
                    [EquipmentIndex.Head] = "leather_studdedhelm_over_headcloth",
                    [EquipmentIndex.Body] = "sleeveless_studded_fur_armor",
                    [EquipmentIndex.Cape] = "battania_cloak_b",
                    [EquipmentIndex.Gloves] = "armwraps",
                    [EquipmentIndex.Leg] = "highland_boots",
                }),

                [(GearRole.Archer, 2)] = new GearPreset(8000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "woodland_yew_bow",
                    [EquipmentIndex.Weapon1] = "steppe_arrows",
                    [EquipmentIndex.Weapon2] = "celtic_dagger",
                    [EquipmentIndex.Head] = "battania_fur_helmet",
                    [EquipmentIndex.Body] = "fur_armor",
                    [EquipmentIndex.Cape] = "battania_woodland_cloak",
                    [EquipmentIndex.Gloves] = "highland_gloves",
                    [EquipmentIndex.Leg] = "highland_leg_wrappings",
                }),

                [(GearRole.Archer, 3)] = new GearPreset(20000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "lowland_yew_bow",
                    [EquipmentIndex.Weapon1] = "bodkin_arrows_c",
                    [EquipmentIndex.Weapon2] = "Broad_Skaen",
                    [EquipmentIndex.Head] = "roughscale_helmet",
                    [EquipmentIndex.Body] = "battania_light_armor_a",
                    [EquipmentIndex.Cape] = "fur_cloak_c",
                    [EquipmentIndex.Gloves] = "rough_tied_bracers",
                    [EquipmentIndex.Leg] = "turndown_leather_boots",
                }),

                // INFANTRY
                [(GearRole.Infantry, 1)] = new GearPreset(2500, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "sturgia_sword_1_t2",
                    [EquipmentIndex.Weapon1] = "sturgia_old_shield_b",
                    [EquipmentIndex.Head] = "northern_fur_cap",
                    [EquipmentIndex.Body] = "nordic_sloven_leather",
                    [EquipmentIndex.Cape] = "stitched_leather_shoulders",
                    [EquipmentIndex.Gloves] = "buttoned_leather_bracers",
                    [EquipmentIndex.Leg] = "sturgia_boots_a",
                }),

                [(GearRole.Infantry, 2)] = new GearPreset(9000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "sturgia_sword_2_t3",
                    [EquipmentIndex.Weapon1] = "northern_round_shield",
                    [EquipmentIndex.Head] = "open_mail_coif",
                    [EquipmentIndex.Body] = "sturgian_lamellar_gambeson",
                    [EquipmentIndex.Cape] = "fur_cloak_b",
                    [EquipmentIndex.Gloves] = "studded_leather_vambraces",
                    [EquipmentIndex.Leg] = "mail_cavalier_boots",
                }),

                [(GearRole.Infantry, 3)] = new GearPreset(22000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "sturgia_sword_4_t4",
                    [EquipmentIndex.Weapon1] = "sturgia_old_shield_a",
                    [EquipmentIndex.Head] = "spangenhelm_with_leather",
                    [EquipmentIndex.Body] = "decorated_nordic_hauberk",
                    [EquipmentIndex.Cape] = "fur_cloak_b",
                    [EquipmentIndex.Gloves] = "northern_plated_gloves",
                    [EquipmentIndex.Leg] = "mail_cavalier_boots",
                }),

                // CAVALRY (LANCER)
                [(GearRole.Lancer, 1)] = new GearPreset(6000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Horse] = "khuzait_horse",
                    [EquipmentIndex.HorseHarness] = "steppe_harness",
                    [EquipmentIndex.Weapon0] = "khuzait_lance_1_t3",
                    [EquipmentIndex.Weapon1] = "studded_adarga",
                    [EquipmentIndex.Weapon2] = "khuzait_sword_1_t2",
                    [EquipmentIndex.Head] = "nomad_padded_hood",
                    [EquipmentIndex.Body] = "belted_leather_cuirass",
                    [EquipmentIndex.Cape] = "reinforced_suede_shoulders",
                    [EquipmentIndex.Gloves] = "studded_leather_vambraces",
                    [EquipmentIndex.Leg] = "steppe_leather_boots",
                }),

                [(GearRole.Lancer, 2)] = new GearPreset(12000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Horse] = "t2_khuzait_horse",
                    [EquipmentIndex.HorseHarness] = "northern_noble_harness",
                    [EquipmentIndex.Weapon0] = "khuzait_lance_2_t4",
                    [EquipmentIndex.Weapon1] = "eastern_wicker_shield",
                    [EquipmentIndex.Weapon2] = "khuzait_sword_3_t3",
                    [EquipmentIndex.Head] = "trailed_desert_helmet",
                    [EquipmentIndex.Body] = "studded_steppe_leather",
                    [EquipmentIndex.Cape] = "bolted_leather_strips",
                    [EquipmentIndex.Gloves] = "steppe_leather_vambraces",
                    [EquipmentIndex.Leg] = "eastern_leather_boots",
                }),

                [(GearRole.Lancer, 3)] = new GearPreset(25000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Horse] = "t3_khuzait_horse",
                    [EquipmentIndex.HorseHarness] = "steppe_half_barding",
                    [EquipmentIndex.Weapon0] = "empire_lance_3_t5",
                    [EquipmentIndex.Weapon1] = "northern_scouts_shield",
                    [EquipmentIndex.Weapon2] = "broad_ild_sword_t3",
                    [EquipmentIndex.Head] = "plumed_fur_lined_helmet",
                    [EquipmentIndex.Body] = "desert_padded_cloth",
                    [EquipmentIndex.Cape] = "steppe_leather_shoulders",
                    [EquipmentIndex.Gloves] = "steppe_leather_vambraces",
                    [EquipmentIndex.Leg] = "mail_cavalier_boots",
                }),
            };
        }
    }
}
