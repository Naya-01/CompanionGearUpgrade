using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace CompanionGearUpgrades.Data
{
    /// <summary>
    /// Small module-level store for the world-view equipment configuration.
    /// This is intentionally separate from the save-game overrides used by the
    /// existing companion upgrade flow.
    /// </summary>
    public static class EquipmentConfigStore
    {
        private const string FileName = "EquipmentConfig.xml";

        public static string FilePath
        {
            get
            {
                string moduleDirectory = GetModuleDirectory();
                return Path.Combine(moduleDirectory, "ModuleData", FileName);
            }
        }

        public static EquipmentConfigFile Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return CreateDefaults();

                var serializer = new XmlSerializer(typeof(EquipmentConfigFile));
                using (var stream = File.OpenRead(FilePath))
                {
                    var data = serializer.Deserialize(stream) as EquipmentConfigFile;
                    return data ?? CreateDefaults();
                }
            }
            catch (Exception)
            {
                // A missing or malformed optional configuration must not prevent
                // the campaign from loading. The next Save will repair the file.
                return CreateDefaults();
            }
        }

        public static void Save(EquipmentConfigFile data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            string directory = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var serializer = new XmlSerializer(typeof(EquipmentConfigFile));
            using (var stream = File.Create(FilePath))
            {
                serializer.Serialize(stream, data);
            }
        }

        public static EquipmentConfigFile CreateDefaults()
        {
            return new EquipmentConfigFile
            {
                Classes = new List<EquipmentClassConfig>
                {
                    new EquipmentClassConfig
                    {
                        ClassId = "Infantry",
                        PrimaryWeapon = "Arming Sword",
                        UseShield = true,
                        Armor = "Mail and Gambeson"
                    },
                    new EquipmentClassConfig
                    {
                        ClassId = "Archer",
                        PrimaryWeapon = "Longbow",
                        UseShield = false,
                        Armor = "Ranger Mail"
                    },
                    new EquipmentClassConfig
                    {
                        ClassId = "Cavalry",
                        PrimaryWeapon = "Lance",
                        UseShield = true,
                        Armor = "Lamellar Harness"
                    }
                }
            };
        }

        private static string GetModuleDirectory()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(EquipmentConfigStore).Assembly.Location);
            if (!string.IsNullOrEmpty(assemblyDirectory))
            {
                var binDirectory = new DirectoryInfo(assemblyDirectory);
                if (binDirectory.Parent != null && binDirectory.Parent.Parent != null)
                    return binDirectory.Parent.Parent.FullName;
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }

    [Serializable]
    public sealed class EquipmentConfigFile
    {
        public List<EquipmentClassConfig> Classes { get; set; }
    }

    [Serializable]
    public sealed class EquipmentClassConfig
    {
        public string ClassId { get; set; }
        public string PrimaryWeapon { get; set; }
        public bool UseShield { get; set; }
        public string Armor { get; set; }
    }
}
