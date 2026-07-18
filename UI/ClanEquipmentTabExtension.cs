using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;
using System.IO;
using System.Reflection;
using System.Xml;

namespace CompanionGearUpgrades.UI
{
    /// <summary>
    /// Inserts the equipment button beside Native's existing Clan tabs.
    /// The XPath targets the existing Income tab, so the new tab is appended
    /// to the same ListPanel and uses the same Native tab brushes.
    /// </summary>
    [PrefabExtension("ClanScreen", "descendant::ButtonWidget[@Command.Click='SetSelectedCategory' and @CommandParameter.Click='3']")]
    internal sealed class ClanEquipmentTabExtension : PrefabExtensionInsertAsSiblingPatch
    {
        public override InsertType Type
        {
            get { return InsertType.Append; }
        }

        public override string Id
        {
            get { return "ClanEquipmentTab"; }
        }

        public override XmlDocument GetPrefabExtension()
        {
            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string moduleDirectory = assemblyDirectory;
            if (!string.IsNullOrEmpty(assemblyDirectory))
            {
                var binDirectory = new DirectoryInfo(assemblyDirectory);
                if (binDirectory.Parent != null && binDirectory.Parent.Parent != null)
                    moduleDirectory = binDirectory.Parent.Parent.FullName;
            }

            var document = new XmlDocument();
            string extensionPath = Path.Combine(moduleDirectory, "GUI", "PrefabExtensions", "ClanEquipmentTab.xml");
            document.Load(extensionPath);
            return document;
        }
    }
}
