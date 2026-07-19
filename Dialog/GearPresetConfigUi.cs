using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Domain;
using CompanionGearUpgrades.Services;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace CompanionGearUpgrades.Dialog
{
    /// <summary>
    /// Legacy dialogue configuration UI. It uses the same service and
    /// overrides as the Clan Gauntlet editor and edits only a temporary snapshot.
    /// Flow: Role -> Tier -> Editor (Categories) -> Pick slot -> item inquiry.
    /// Exit -> Save / Cancel -> return to conversation root.
    /// </summary>
    public sealed class GearPresetConfigUi
    {
        private readonly CompanionGearUpgradeService _service;
        private readonly GearPresetOverrides _overrides;

        // Called when we fully exit the config UI (after Save/Cancel, or Cancel from Role selection).
        private readonly Action _returnToConversationRoot;

        private GearRole _role;
        private int _tier;

        // Session state (Cancel discards, Save commits)
        private GearPresetSnapshot _working;

        // If you came from a category menu, store where to go back after the item inquiry closes.
        private Action _returnAfterPicker;

        public GearPresetConfigUi(CompanionGearUpgradeService service, GearPresetOverrides overrides, Action returnToConversationRoot)
        {
            _service = service;
            _overrides = overrides;
            _returnToConversationRoot = returnToConversationRoot;
        }

        // Shared by the dialogue and Clan Gauntlet editors so both paths have
        // exactly the same reset and price-validation behavior.
        internal static GearPresetSnapshot CreateDefaultTierSnapshot(CompanionGearUpgradeService service, GearRole role, int tier)
        {
            GearPreset defaultPreset = service != null ? service.GetDefaultPresetOrNull(role, tier) : null;
            return defaultPreset == null
                ? null
                : new GearPresetSnapshot(defaultPreset.Cost, new Dictionary<EquipmentIndex, string>(defaultPreset.Slots));
        }

        internal static bool TrySetSnapshotPrice(GearPresetSnapshot snapshot, string text, out GearPresetSnapshot updatedSnapshot)
        {
            updatedSnapshot = snapshot;
            int value;
            if (snapshot == null || !int.TryParse(text, out value) || value < 0)
                return false;

            updatedSnapshot = new GearPresetSnapshot(value, snapshot.Slots);
            return true;
        }

        public void Open()
        {
            ShowRoleSelection();
        }

        private void ExitToConversationRoot()
        {
            _working = null;
            _returnAfterPicker = null;
            _returnToConversationRoot?.Invoke();
        }

        /* ============================================================
         * ROLE / TIER
         * ============================================================ */

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
                        // Cancel -> back to conversation root
                        ExitToConversationRoot();
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

            _returnAfterPicker = null;
            string summary =
                $"Role: {_role} | Tier: {_tier} | Cost: {_working.Cost}\n" +
                "Choose what to edit.\n" +
                "Exit -> Save / Cancel";

            var options = new List<InquiryElement>
            {
                new InquiryElement("cost", "Set price (gold)", null),
                new InquiryElement("armors", "Armors", null),
                new InquiryElement("weapons", "Weapons", null),
                new InquiryElement("reset", "Reset this tier to default", null), // keep it the last one !
            };

            if (_role == GearRole.Lancer)
            {
                options[options.Count - 1] = new InquiryElement("horse", "Horse", null);
                options.Add(new InquiryElement("reset", "Reset this tier to default", null));
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "CGU - Edit Preset",
                summary,
                options,
                true,   // isExitShown
                0,      // minSelectableOptionCount (allow exit without selection)
                1,      // maxSelectableOptionCount
                "Select",
                "Exit",
                list =>
                {
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
                        if (s == "armors") { ShowArmorMenu(); return; }
                        if (s == "weapons") { ShowWeaponMenu(); return; }
                        if (s == "horse") { ShowHorseMenu(); return; }
                        if (s == "reset") { ResetWorkingToDefault(); ShowEditMenu(); return; }
                    }

                    ShowEditMenu();
                },
                _ =>
                {
                    // Exit / X -> show Save / Cancel, and then return to conversation root.
                    ShowSaveCancelDialog();
                },
                "",
                false // search not needed here
            ));
        }

        private void ShowArmorMenu()
        {
            if (_working == null)
                return;

            var options = new List<InquiryElement>
            {
                new InquiryElement(EquipmentIndex.Head, $"Head: {FormatItem(GetSlotIdOrEmpty(EquipmentIndex.Head))}", null),
                new InquiryElement(EquipmentIndex.Body, $"Body: {FormatItem(GetSlotIdOrEmpty(EquipmentIndex.Body))}", null),
                new InquiryElement(EquipmentIndex.Cape, $"Cape: {FormatItem(GetSlotIdOrEmpty(EquipmentIndex.Cape))}", null),
                new InquiryElement(EquipmentIndex.Gloves, $"Gloves: {FormatItem(GetSlotIdOrEmpty(EquipmentIndex.Gloves))}", null),
                new InquiryElement(EquipmentIndex.Leg, $"Legs: {FormatItem(GetSlotIdOrEmpty(EquipmentIndex.Leg))}", null),
            };

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "CGU - Armors",
                "Pick a slot (you will choose an item in a simple inquiry).",
                options,
                true,
                1,
                1,
                "Select",
                "Back",
                selected =>
                {
                    // Remember category so we return here after the item inquiry closes.
                    _returnAfterPicker = ShowArmorMenu;
                    PromptSetItem_FromEditMenu((EquipmentIndex)selected[0].Identifier);
                },
                _ =>
                {
                    _returnAfterPicker = null;
                    ShowEditMenu();
                },
                "",
                true // search is useful here (names)
            ));
        }

        private void ShowWeaponMenu()
        {
            if (_working == null)
                return;

            var options = new List<InquiryElement>
            {
                new InquiryElement(EquipmentIndex.Weapon0, $"Weapon0: {FormatItem(GetSlotIdOrEmpty(EquipmentIndex.Weapon0))}", null),
                new InquiryElement(EquipmentIndex.Weapon1, $"Weapon1: {FormatItem(GetSlotIdOrEmpty(EquipmentIndex.Weapon1))}", null),
                new InquiryElement(EquipmentIndex.Weapon2, $"Weapon2: {FormatItem(GetSlotIdOrEmpty(EquipmentIndex.Weapon2))}", null),
                new InquiryElement(EquipmentIndex.Weapon3, $"Weapon3: {FormatItem(GetSlotIdOrEmpty(EquipmentIndex.Weapon3))}", null),
            };

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "CGU - Weapons",
                "Pick a slot (you will choose an item in a simple inquiry).",
                options,
                true,
                1,
                1,
                "Select",
                "Back",
                selected =>
                {
                    _returnAfterPicker = ShowWeaponMenu;
                    PromptSetItem_FromEditMenu((EquipmentIndex)selected[0].Identifier);
                },
                _ =>
                {
                    _returnAfterPicker = null;
                    ShowEditMenu();
                },
                "",
                true
            ));
        }

        private void ShowHorseMenu()
        {
            if (_working == null)
                return;

            var options = new List<InquiryElement>
            {
                new InquiryElement(EquipmentIndex.Horse, $"Horse: {FormatItem(GetSlotIdOrEmpty(EquipmentIndex.Horse))}", null),
                new InquiryElement(EquipmentIndex.HorseHarness, $"Harness: {FormatItem(GetSlotIdOrEmpty(EquipmentIndex.HorseHarness))}", null),
            };

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "CGU - Horse",
                "Pick a slot (you will choose an item in a simple inquiry).",
                options,
                true,
                1,
                1,
                "Select",
                "Back",
                selected =>
                {
                    _returnAfterPicker = ShowHorseMenu;
                    PromptSetItem_FromEditMenu((EquipmentIndex)selected[0].Identifier);
                },
                _ =>
                {
                    _returnAfterPicker = null;
                    ShowEditMenu();
                },
                "",
                true
            ));
        }

        private string GetSlotIdOrEmpty(EquipmentIndex slot)
        {
            if (_working == null || _working.Slots == null)
                return "";

            return _working.Slots.TryGetValue(slot, out var id) ? (id ?? "") : "";
        }

        private void ResetWorkingToDefault()
        {
            GearPresetSnapshot defaultSnapshot = CreateDefaultTierSnapshot(_service, _role, _tier);
            if (defaultSnapshot == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[CGU] Missing preset."));
                return;
            }

            _working = defaultSnapshot;
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
                    GearPresetSnapshot updatedSnapshot;
                    if (!TrySetSnapshotPrice(_working, text, out updatedSnapshot))
                    {
                        InformationManager.DisplayMessage(new InformationMessage("[CGU] Invalid number."));
                        ShowEditMenu();
                        return;
                    }

                    _working = updatedSnapshot;
                    ShowEditMenu();
                },
                ShowEditMenu
            ));
        }

        private void PromptSetItem_FromEditMenu(EquipmentIndex slot)
        {
            if (_working == null)
                return;

            List<ItemObject> candidates = _service.GetCompatibleItems(slot);
            var options = new List<InquiryElement>
            {
                new InquiryElement(GearPresetOverrides.EmptySlotMarker, "(empty slot)", null)
            };

            foreach (ItemObject item in candidates)
                options.Add(new InquiryElement(item.StringId, $"{item.Name} [{item.StringId}]", null));

            Action back = _returnAfterPicker ?? (Action)ShowEditMenu;
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                $"CGU - Pick {slot}",
                "Choose an item. The selection changes only the temporary snapshot.",
                options,
                true,
                1,
                1,
                "Select",
                "Back",
                selected =>
                {
                    object identifier = selected[0].Identifier;
                    string id = identifier as string;
                    _working.Slots[slot] = string.Equals(id, GearPresetOverrides.EmptySlotMarker, StringComparison.Ordinal)
                        ? null
                        : id;
                    back();
                },
                _ => back(),
                "",
                true
            ));
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

        private void ShowSaveCancelDialog()
        {
            if (_working == null)
            {
                ExitToConversationRoot();
                return;
            }

            InformationManager.ShowInquiry(new InquiryData(
                "CGU - Save changes",
                $"Save changes for role '{_role}' / tier {_tier}?",
                true,
                true,
                "Save",
                "Cancel",
                () =>
                {
                    CommitAndClose();
                    ExitToConversationRoot();
                },
                () =>
                {
                    // Cancel -> discard the working session
                    _working = null;
                    ExitToConversationRoot();
                }
            ));
        }

        private static string FormatItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return "(empty)";

            ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            string name = item != null ? item.Name.ToString() : "?";
            return $"{name}";
        }
    }
}
