using CompanionGearUpgrades.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using TaleWorlds.Core;

namespace CompanionGearUpgrades.Data
{
    /// <summary>
    /// Versioned, save-independent JSON representation of role presets.
    /// Role identifiers deliberately are not exported: names make a preset
    /// portable between campaigns while the destination campaign creates or
    /// reuses its own stable custom-role identifiers.
    /// </summary>
    [DataContract]
    public sealed class GearPresetTransferDocument
    {
        public const int CurrentSchemaVersion = 1;

        [DataMember(Name = "schemaVersion", Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Name = "modVersion", Order = 2)]
        public string ModVersion { get; set; }

        [DataMember(Name = "roles", Order = 3)]
        public List<GearPresetTransferRole> Roles { get; set; }
    }

    [DataContract]
    public sealed class GearPresetTransferRole
    {
        [DataMember(Name = "name", Order = 1)]
        public string Name { get; set; }

        [DataMember(Name = "tiers", Order = 2)]
        public List<GearPresetTransferTier> Tiers { get; set; }
    }

    [DataContract]
    public sealed class GearPresetTransferTier
    {
        [DataMember(Name = "tier", Order = 1)]
        public int Tier { get; set; }

        [DataMember(Name = "price", Order = 2)]
        public int Price { get; set; }

        // The serializer is configured to use a JSON object, for example:
        // "slots": { "Weapon0": "sword_id", "Head": null }
        [DataMember(Name = "slots", Order = 3)]
        public Dictionary<string, string> Slots { get; set; }
    }

    /// <summary>
    /// Stable JSON names for the equipment slots we intentionally support.
    /// This avoids serializing the numeric enum values used by a savegame.
    /// </summary>
    public static class GearPresetTransferSlots
    {
        public static bool TryGetSlot(string name, out EquipmentIndex slot)
        {
            return GearSlotCatalog.TryGetSlot(name, out slot);
        }

        public static string GetSlotName(EquipmentIndex slot)
        {
            return GearSlotCatalog.GetStableName(slot);
        }
    }

    /// <summary>
    /// JSON read/write and structural validation. Validation happens before a
    /// document reaches the current campaign, so an invalid share file never
    /// partially changes role or preset data.
    /// </summary>
    public static class GearPresetTransferJson
    {
        private const long MaximumFileSizeBytes = 4L * 1024L * 1024L;

        public static bool TryRead(
            string path,
            out GearPresetTransferDocument document,
            out string error)
        {
            document = null;
            error = null;

            string fullPath;
            if (!TryGetExistingPath(path, out fullPath, out error))
                return false;

            try
            {
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length > MaximumFileSizeBytes)
                {
                    error = "The JSON file is too large.";
                    return false;
                }

                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    document = CreateSerializer().ReadObject(stream) as GearPresetTransferDocument;
                }
            }
            catch (Exception exception)
            {
                error = "The JSON file could not be read: " + exception.Message;
                return false;
            }

            return Validate(document, out error);
        }

        // Explicit File aliases keep the view-model intent clear and make
        // callers read as an external transfer rather than savegame I/O.
        public static bool TryReadFile(
            string path,
            out GearPresetTransferDocument document,
            out string error)
        {
            return TryRead(path, out document, out error);
        }

        /// <summary>
        /// Writes a validated document. Invalid data is never written and a
        /// missing .json extension is added for the player.
        /// </summary>
        public static bool TryWrite(
            string path,
            GearPresetTransferDocument document,
            out string writtenPath,
            out string error)
        {
            writtenPath = null;
            if (!Validate(document, out error))
                return false;

            string fullPath;
            if (!TryGetWritePath(path, out fullPath, out error))
                return false;

            try
            {
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    CreateSerializer().WriteObject(stream, document);
                }

                writtenPath = fullPath;
                return true;
            }
            catch (Exception exception)
            {
                error = "The JSON file could not be written: " + exception.Message;
                return false;
            }
        }

        public static bool TryWriteFile(
            string path,
            GearPresetTransferDocument document,
            out string writtenPath,
            out string error)
        {
            return TryWrite(path, document, out writtenPath, out error);
        }

        public static void Write(string path, GearPresetTransferDocument document)
        {
            string writtenPath;
            string error;
            if (!TryWrite(path, document, out writtenPath, out error))
                throw new InvalidOperationException(error);
        }

        public static bool Validate(GearPresetTransferDocument document, out string error)
        {
            error = null;
            if (document == null)
            {
                error = "The JSON document is empty.";
                return false;
            }

            if (document.SchemaVersion != GearPresetTransferDocument.CurrentSchemaVersion)
            {
                error = "This JSON schema version is not supported.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(document.ModVersion))
            {
                error = "The JSON document has no mod version.";
                return false;
            }

            document.ModVersion = document.ModVersion.Trim();
            if (document.Roles == null || document.Roles.Count == 0 ||
                document.Roles.Count > GearPresetRepository.MaxRoleCount)
            {
                error = "The JSON document must contain between 1 and 10 roles.";
                return false;
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GearPresetTransferRole role in document.Roles)
            {
                if (role == null)
                {
                    error = "The JSON document contains an invalid role.";
                    return false;
                }

                string normalizedName;
                string nameError;
                if (!GearPresetRepository.TryNormalizeCustomRoleName(role.Name, out normalizedName, out nameError))
                {
                    error = nameError;
                    return false;
                }

                if (!names.Add(normalizedName))
                {
                    error = "Role names in the JSON document must be unique.";
                    return false;
                }

                role.Name = normalizedName;
                if (!ValidateTiers(role, out error))
                    return false;
            }

            return true;
        }

        public static bool TryValidate(
            GearPresetTransferDocument document,
            out GearPresetTransferValidationResult result)
        {
            string error;
            bool isValid = Validate(document, out error);
            result = new GearPresetTransferValidationResult(isValid, error);
            return isValid;
        }

        private static bool ValidateTiers(GearPresetTransferRole role, out string error)
        {
            error = null;
            if (role.Tiers == null || role.Tiers.Count != GearPresetRepository.TierCount)
            {
                error = "Each role must contain exactly three tiers.";
                return false;
            }

            var tierNumbers = new HashSet<int>();
            foreach (GearPresetTransferTier tier in role.Tiers)
            {
                if (tier == null ||
                    !GearPresetRepository.IsValidTier(tier.Tier) ||
                    !GearPresetPricePolicy.IsValid(tier.Price))
                {
                    error = "An imported tier has an invalid number or price.";
                    return false;
                }

                if (!tierNumbers.Add(tier.Tier))
                {
                    error = "Each role must contain tiers 1, 2 and 3 only once.";
                    return false;
                }

                if (tier.Slots == null || tier.Slots.Count != GearSlotCatalog.EditableSlots.Count)
                {
                    error = "Each tier must explicitly contain every supported equipment slot.";
                    return false;
                }

                var slots = new HashSet<EquipmentIndex>();
                foreach (KeyValuePair<string, string> entry in tier.Slots)
                {
                    EquipmentIndex slot;
                    if (!GearPresetTransferSlots.TryGetSlot(entry.Key, out slot) || !slots.Add(slot))
                    {
                        error = "The JSON document contains an unsupported equipment slot.";
                        return false;
                    }

                    // Null is an intentional empty slot. A non-null StringId
                    // must be meaningful; unavailable mod items are resolved
                    // later by the service as warnings, not errors.
                    if (entry.Value != null && string.IsNullOrWhiteSpace(entry.Value))
                    {
                        error = "An equipment StringId cannot be empty.";
                        return false;
                    }
                }

                if (slots.Count != GearSlotCatalog.EditableSlots.Count)
                {
                    error = "Each tier must explicitly contain every supported equipment slot.";
                    return false;
                }
            }

            if (tierNumbers.Count != GearPresetRepository.TierCount ||
                !tierNumbers.Contains(1) || !tierNumbers.Contains(2) || !tierNumbers.Contains(3))
            {
                error = "Each role must contain tiers 1, 2 and 3.";
                return false;
            }

            return true;
        }

        private static DataContractJsonSerializer CreateSerializer()
        {
            return new DataContractJsonSerializer(
                typeof(GearPresetTransferDocument),
                new DataContractJsonSerializerSettings
                {
                    MaxItemsInObjectGraph = 10000,
                    UseSimpleDictionaryFormat = true
                });
        }

        private static bool TryGetExistingPath(string path, out string fullPath, out string error)
        {
            fullPath = null;
            error = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Enter the path of a JSON file to import.";
                return false;
            }

            try
            {
                fullPath = Path.GetFullPath(path.Trim().Trim('"'));
                if (!File.Exists(fullPath))
                {
                    error = "The selected JSON file does not exist.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = "The JSON file path is invalid: " + exception.Message;
                return false;
            }
        }

        private static bool TryGetWritePath(string path, out string fullPath, out string error)
        {
            fullPath = null;
            error = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Enter a destination path for the JSON file.";
                return false;
            }

            try
            {
                string requestedPath = path.Trim().Trim('"');
                if (!string.Equals(Path.GetExtension(requestedPath), ".json", StringComparison.OrdinalIgnoreCase))
                    requestedPath += ".json";

                fullPath = Path.GetFullPath(requestedPath);
                return true;
            }
            catch (Exception exception)
            {
                error = "The destination path is invalid: " + exception.Message;
                return false;
            }
        }
    }

    /// <summary>
    /// A small UI-friendly validation result.  The document is only usable
    /// when <see cref="IsValid"/> is true; missing mod equipment is checked
    /// separately while applying a valid document and is intentionally not a
    /// validation error.
    /// </summary>
    public sealed class GearPresetTransferValidationResult
    {
        public GearPresetTransferValidationResult(bool isValid, string error)
        {
            IsValid = isValid;
            Error = error;
        }

        public bool IsValid { get; private set; }
        public string Error { get; private set; }
    }
}
