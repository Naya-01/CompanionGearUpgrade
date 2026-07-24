using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using PrefabSetAttributesPatch = Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionSetAttributePatch;

namespace CompanionGearUpgrades.UI
{
    /// <summary>
    /// Keeps all five Clan actions inside Native's 887-pixel tab bar.
    /// </summary>
    [PrefabExtension("ClanScreen", "descendant::ButtonWidget[@Command.Click='SetSelectedCategory']")]
    internal sealed class ClanTabWidthExtension : PrefabSetAttributesPatch
    {
        public override List<PrefabSetAttributesPatch.Attribute> Attributes
        {
            get
            {
                return new List<PrefabSetAttributesPatch.Attribute>
                {
                    new PrefabSetAttributesPatch.Attribute("SuggestedWidth", "176")
                };
            }
        }
    }

    /// <summary>
    /// Turns Native's current right-edge Income tab into a center tab so the
    /// injected Gear Presets action can become the new right edge.
    /// </summary>
    [PrefabExtension("ClanScreen", "descendant::ButtonWidget[@Command.Click='SetSelectedCategory' and @CommandParameter.Click='3']")]
    internal sealed class ClanIncomeTabBrushExtension : PrefabSetAttributesPatch
    {
        public override List<PrefabSetAttributesPatch.Attribute> Attributes
        {
            get
            {
                return new List<PrefabSetAttributesPatch.Attribute>
                {
                    new PrefabSetAttributesPatch.Attribute("Brush", "Header.Tab.Center"),
                    new PrefabSetAttributesPatch.Attribute("SuggestedWidth", "176"),
                    new PrefabSetAttributesPatch.Attribute("SuggestedHeight", "!Header.Tab.Center.Height.Scaled"),
                    new PrefabSetAttributesPatch.Attribute("PositionYOffset", "6")
                };
            }
        }
    }

    /// <summary>
    /// Inserts the Gear Presets button beside Native's existing Clan tabs.
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
            get { return "CGU_ClanGearPresetsTab"; }
        }

        public override XmlDocument GetPrefabExtension()
        {
            return ClanPrefabExtensionLoader.Load("ClanEquipmentTab.xml");
        }
    }

    /// <summary>
    /// Inserts the dedicated action immediately after the companion name.
    /// Its widget tree lives entirely in its own prefab extension file.
    /// </summary>
    [PrefabExtension("ClanLordTuple", "descendant::TextWidget[@Text='@Name']")]
    internal sealed class ClanCompanionGearPresetButtonExtension : PrefabExtensionInsertAsSiblingPatch
    {
        public override InsertType Type
        {
            get { return InsertType.Append; }
        }

        public override string Id
        {
            get { return "CGU_ClanCompanionGearPresetButton"; }
        }

        public override XmlDocument GetPrefabExtension()
        {
            return ClanPrefabExtensionLoader.Load("ClanCompanionGearPresetButton.xml");
        }
    }

    internal static class ClanPrefabExtensionLoader
    {
        internal static XmlDocument Load(string fileName)
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
            document.Load(Path.Combine(moduleDirectory, "GUI", "PrefabExtensions", fileName));
            return document;
        }
    }
}
