using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Domain;
using CompanionGearUpgrades.Services;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator.FaceGenVM;

namespace CompanionGearUpgrades.Dialog
{
    /// <summary>
    /// Small dynamic configuration UI using only Inquiry windows (no custom Gauntlet).
    /// The flow is: Role -> Tier -> Editor menu (Price / Armors / Weapons / Horse) -> Save/Cancel.
    /// </summary>
    public sealed class GearPresetConfigUi
    {
        private readonly CompanionGearUpgradeService _service;
        private readonly GearPresetOverrides _overrides;

        private GearRole _role;
        private int _tier;

        // Session state (Cancel discards, Save commits)
        private GearPresetSnapshot _working;

        public GearPresetConfigUi(CompanionGearUpgradeService service, GearPresetOverrides overrides)
        {
            _service = service;
            _overrides = overrides;
        }

        public void Open()
        {
            ShowRoleSelection();
        }

        private void ShowRoleSelection()
        {
            var options = new List<InquiryElement>
            {
                new InquiryElement(GearRole.Infantry, "Infantry", null, true, ""),
                new InquiryElement(GearRole.Archer, "Archer", null, true, ""),
                new InquiryElement(GearRole.Lancer, "Lancer (cavalry)", null, true, ""),
            };

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "CGU - Configure upgrade presets",
                    "Choose a role to configure.",
                    options,
                    true,   // isExitShown
                    1,      // minSelectableOptionCount
                    1,      // maxSelectableOptionCount
                    "Select",
                    "Cancel",
                    selected =>
                    {
                        var element = selected[0];
                        _role = (GearRole)element.Identifier;
                        ShowTierSelection();
                    },
                    _ =>
                    {
                        // Cancel -> close
                    },
                    "",
                    false
                )
            );
        }

        private void ShowTierSelection()
        {
            var options = new List<InquiryElement>();
            for (int tier = 1; tier <= 3; tier++)
            {
                int cost = _service.GetEffectiveCost(_role, tier);
                options.Add(new InquiryElement(tier, $"Tier {tier} ({cost} gold)", null, true, ""));
            }

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "CGU - Configure upgrade presets",
                    $"Role: {_role}. Choose a tier.",
                    options,
                    true,   // isExitShown
                    1,      // min
                    1,      // max
                    "Select",
                    "Back",
                    selected =>
                    {
                        var element = selected[0];
                        _tier = (int)element.Identifier;
                        BeginSession();
                        ShowEditMenu();
                    },
                    _ =>
                    {
                        ShowRoleSelection();
                    },
                    "",
                    false
                )
            );
        }

        private void BeginSession()
        {
            GearPreset defaultPreset = _service.GetDefaultPresetOrNull(_role, _tier);
            if (defaultPreset == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[CGU] Missing preset."));
                return;
            }

            _working = _service.BuildEffectiveSnapshot(_role, _tier, defaultPreset);
        }

        private void ShowEditMenu()
        {
            if (_working == null)
                return;

            string summary =
                $"Role: {_role}\n" +
                $"Tier: {_tier}\n" +
                $"Cost: {_working.Cost}\n\n" +
                BuildSlotSummary(_working) +
                "\n\nExit -> Save / Cancel";

            var options = new List<InquiryElement>
    {
        new InquiryElement("cost", "Set price (gold)", null),

        new InquiryElement(EquipmentIndex.Head, "Set Head item id", null),
        new InquiryElement(EquipmentIndex.Body, "Set Body item id", null),
        new InquiryElement(EquipmentIndex.Cape, "Set Cape item id", null),
        new InquiryElement(EquipmentIndex.Gloves, "Set Gloves item id", null),
        new InquiryElement(EquipmentIndex.Leg, "Set Legs item id", null),

        new InquiryElement(EquipmentIndex.Weapon0, "Set Weapon0 item id", null),
        new InquiryElement(EquipmentIndex.Weapon1, "Set Weapon1 item id", null),
        new InquiryElement(EquipmentIndex.Weapon2, "Set Weapon2 item id", null),
        new InquiryElement(EquipmentIndex.Weapon3, "Set Weapon3 item id", null),

        new InquiryElement(EquipmentIndex.Horse, "Set Horse item id", null),
        new InquiryElement(EquipmentIndex.HorseHarness, "Set HorseHarness item id", null),

        new InquiryElement("reset", "Reset this tier to default", null),
    };

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "CGU - Edit Preset",
                summary,
                options,
                true,  // isExitShown -> affiche le X en haut à droite
                0,     // minSelectableOptionCount -> IMPORTANT: permet de quitter sans sélection
                1,     // maxSelectableOptionCount
                "Select",
                "Exit",
                list =>
                {
                    // Comme min=0, on valide nous-mêmes
                    if (list == null || list.Count == 0)
                    {
                        InformationManager.DisplayMessage(new InformationMessage("[CGU] Select an option first."));
                        ShowEditMenu();
                        return;
                    }

                    object id = list[0].Identifier;

                    if (id is string s)
                    {
                        if (s == "cost") { PromptSetPrice_FromEditMenu(); return; }
                        if (s == "reset") { ResetWorkingToDefault(); ShowEditMenu(); return; }
                    }

                    PromptSetItem_FromEditMenu((EquipmentIndex)id);
                },
                _ =>
                {
                    // Exit / X -> on affiche enfin Save / Cancel
                    ShowSaveCancelDialog();
                },
                "",
                true // isSeachAvailable
            ));
        }

        private string BuildSlotSummary(GearPresetSnapshot snap)
        {
            if (snap.Slots == null || snap.Slots.Count == 0)
                return "Slots: (none)\n";

            string lines = "Slots:\n";
            foreach (var kv in snap.Slots)
                lines += $" - {kv.Key}: {kv.Value}\n";
            return lines;
        }

        private void ResetWorkingToDefault()
        {
            GearPreset defaultPreset = _service.GetDefaultPresetOrNull(_role, _tier);
            if (defaultPreset == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[CGU] Missing preset."));
                return;
            }

            _working = _service.BuildEffectiveSnapshot(_role, _tier, defaultPreset); 
        }

        private void PromptSetPrice_FromEditMenu()
        {
            InformationManager.ShowTextInquiry(new TextInquiryData(
                "CGU - Set price",
                "Enter the price in gold (number):",
                true,
                true,
                "OK",
                "Cancel",
                text =>
                {
                    if (!int.TryParse(text, out int value) || value < 0)
                    {
                        InformationManager.DisplayMessage(new InformationMessage("[CGU] Invalid number."));
                        ShowEditMenu();
                        return;
                    }

                    _working = new GearPresetSnapshot(value, _working.Slots);
                    ShowEditMenu();
                },
                ShowEditMenu
            ));
        }

        private void PromptSetItem_FromEditMenu(EquipmentIndex slot)
        {
            _working.Slots.TryGetValue(slot, out var current);

            InformationManager.ShowTextInquiry(new TextInquiryData(
                "CGU - Set item",
                $"Slot: {slot}\nCurrent: {FormatItem(current)}\n\nEnter the new itemId (e.g. 'noble_bow'):",
                true,
                true,
                "OK",
                "Cancel",
                text =>
                {
                    string itemId = (text ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(itemId))
                    {
                        InformationManager.DisplayMessage(new InformationMessage("[CGU] Empty itemId."));
                        ShowEditMenu();
                        return;
                    }

                    var item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                    if (item == null)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"[CGU] Item not found: '{itemId}'."));
                        ShowEditMenu();
                        return;
                    }

                    _working.Slots[slot] = itemId;
                    ShowEditMenu();
                },
                ShowEditMenu
            ));
        }

        private void ShowSaveCancelDialog()
        {
            InformationManager.ShowInquiry(new InquiryData(
                "CGU - Save changes?",
                "Do you want to save the changes for this preset?\n\nSave commits changes.\nCancel discards them.",
                true,
                true,
                "Save",
                "Cancel",
                () =>
                {
                    CommitAndClose();          // commit overrides
                    ShowTierSelection();       // ou ferme juste
                },
                () =>
                {
                    _working = null;           // discard snapshot
                    InformationManager.DisplayMessage(new InformationMessage("[CGU] Changes discarded."));
                    ShowTierSelection();
                }
            ));
        }

        private void ShowSlotSelection(List<EquipmentIndex> slots, string title)
        {
            if (_working == null)
                return;

            var options = new List<InquiryElement>();
            foreach (var slot in slots)
            {
                _working.Slots.TryGetValue(slot, out var current);
                string label = $"{slot}: {FormatItem(current)}";
                options.Add(new InquiryElement(slot, label, null, true, ""));
            }

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    $"CGU - {title}",
                    "Select a slot to edit.",
                    options,
                    true,   // isExitShown
                    1,      // min
                    1,      // max
                    "Select",
                    "Back",
                    selected =>
                    {
                        var element = selected[0];
                        var slot = (EquipmentIndex)element.Identifier;
                        PromptSetItem(slot, title);
                    },
                    _ =>
                    {
                        ShowEditMenu();
                    },
                    "",
                    false
                )
            );
        }

        private void PromptSetItem(EquipmentIndex slot, string title)
        {
            string current;
            _working.Slots.TryGetValue(slot, out current);

            InformationManager.ShowTextInquiry(new TextInquiryData(
                "CGU - Set item",
                $"Slot: {slot}\nCurrent: {FormatItem(current)}\n\nEnter the new itemId (e.g. 'noble_bow'):",
                true,
                true,
                "OK",
                "Cancel",
                text =>
                {
                    string itemId = (text ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(itemId))
                    {
                        InformationManager.DisplayMessage(new InformationMessage("[CGU] Empty itemId."));
                        ShowSlotSelection(GetSlotsForTitle(title), title);
                        return;
                    }

                    var item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                    if (item == null)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"[CGU] Item not found: '{itemId}'."));
                        ShowSlotSelection(GetSlotsForTitle(title), title);
                        return;
                    }

                    _working.Slots[slot] = itemId;
                    ShowSlotSelection(GetSlotsForTitle(title), title);
                },
                () => ShowSlotSelection(GetSlotsForTitle(title), title)));
        }

        private void CommitAndClose()
        {
            if (_working == null)
                return;

            GearPreset defaultPreset = _service.GetDefaultPresetOrNull(_role, _tier);
            if (defaultPreset == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[CGU] Missing preset."));
                return;
            }

            _overrides.CommitSnapshot(_role, _tier, defaultPreset, _working);
            InformationManager.DisplayMessage(new InformationMessage("[CGU] Preset saved."));
            _working = null;
        }

        private static List<EquipmentIndex> GetArmorSlots()
        {
            return new List<EquipmentIndex>
            {
                EquipmentIndex.Head,
                EquipmentIndex.Body,
                EquipmentIndex.Cape,
                EquipmentIndex.Gloves,
                EquipmentIndex.Leg,
            };
        }

        private static List<EquipmentIndex> GetWeaponSlots()
        {
            return new List<EquipmentIndex>
            {
                EquipmentIndex.Weapon0,
                EquipmentIndex.Weapon1,
                EquipmentIndex.Weapon2,
                EquipmentIndex.Weapon3,
            };
        }

        private static List<EquipmentIndex> GetHorseSlots()
        {
            return new List<EquipmentIndex>
            {
                EquipmentIndex.Horse,
                EquipmentIndex.HorseHarness,
            };
        }

        private static List<EquipmentIndex> GetSlotsForTitle(string title)
        {
            switch (title)
            {
                case "Armors": return GetArmorSlots();
                case "Weapons": return GetWeaponSlots();
                case "Horse": return GetHorseSlots();
                default: return new List<EquipmentIndex>();
            }
        }

        private static string FormatItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return "(empty)";

            ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            string name = item != null ? item.Name.ToString() : "?";
            return $"{name} [{itemId}]";
        }

    }
}
