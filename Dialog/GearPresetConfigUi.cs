using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Domain;
using CompanionGearUpgrades.Services;
using Helpers;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace CompanionGearUpgrades.Dialog
{
    /// <summary>
    /// Small dynamic configuration UI using only Inquiry windows (no custom Gauntlet).
    /// Flow: Role -> Tier -> Editor (Categories) -> Pick slot -> Inventory picker -> back to same category.
    /// Exit -> Save / Cancel -> return to conversation root.
    /// </summary>
    public sealed class GearPresetConfigUi
    {
        private enum InventoryItemType
        {
            HeadArmor,
            BodyArmor,
            Cape,
            Gloves,
            LegArmor,
            WeaponOrAmmo,
            Horse,
            HorseHarness
        }

        private readonly CompanionGearUpgradeService _service;
        private readonly GearPresetOverrides _overrides;

        // Called when we fully exit the config UI (after Save/Cancel, or Cancel from Role selection).
        private readonly Action _returnToConversationRoot;

        private GearRole _role;
        private int _tier;

        // Session state (Cancel discards, Save commits)
        private GearPresetSnapshot _working;

        // If you came from a category menu (Armors/Weapons/Horse), we store where to go back after the inventory picker closes.
        private Action _returnAfterPicker;

        // Cache to avoid rebuilding huge lists every time
        private readonly Dictionary<InventoryItemType, List<ItemObject>> _cachedItemsByType =
            new Dictionary<InventoryItemType, List<ItemObject>>();

        private static readonly EquipmentIndex[] _allEditableSlots =
        {
            EquipmentIndex.Weapon0,
            EquipmentIndex.Weapon1,
            EquipmentIndex.Weapon2,
            EquipmentIndex.Weapon3,
            EquipmentIndex.Head,
            EquipmentIndex.Body,
            EquipmentIndex.Cape,
            EquipmentIndex.Gloves,
            EquipmentIndex.Leg,
            EquipmentIndex.Horse,
            EquipmentIndex.HorseHarness,
        };

        public GearPresetConfigUi(CompanionGearUpgradeService service, GearPresetOverrides overrides, Action returnToConversationRoot)
        {
            _service = service;
            _overrides = overrides;
            _returnToConversationRoot = returnToConversationRoot;
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
                "Pick a slot (you will choose the item in the Inventory screen).",
                options,
                true,
                1,
                1,
                "Select",
                "Back",
                selected =>
                {
                    // Remember category so we return here after inventory picker closes.
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
                "Pick a slot (you will choose the item in the Inventory screen).",
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
                "Pick a slot (you will choose the item in the Inventory screen).",
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
            GearPreset defaultPreset = _service.GetDefaultPresetOrNull(_role, _tier);
            if (defaultPreset == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[CGU] Missing preset."));
                return;
            }

            _working = new GearPresetSnapshot(defaultPreset.Cost,
                                  new Dictionary<EquipmentIndex, string>(defaultPreset.Slots)
                                  );
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
            if (_working == null)
                return;

            OpenInventoryPickerForSlot(slot, () =>
            {
                Action back = _returnAfterPicker ?? (Action)ShowEditMenu;
                back();
            });
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

        private void OpenInventoryPickerForSlot(EquipmentIndex slot, Action onClosed)
        {
            if (_working == null)
            {
                onClosed?.Invoke();
                return;
            }

            MobileParty party = MobileParty.MainParty;
            ItemRoster partyRoster = party != null ? party.ItemRoster : null;
            if (partyRoster == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[CGU] Main party inventory not available."));
                onClosed?.Invoke();
                return;
            }

            List<ItemObject> candidates = GetItemsForSlot(slot);
            if (candidates == null || candidates.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage("[CGU] No items available for this slot."));
                onClosed?.Invoke();
                return;
            }

            // Left side: a catalog of valid items (1x each). The player must move ONE item to their side,
            // then press Done. We rollback inventory & equipment changes after closing (no free items / no equip).
            ItemRoster leftRoster = new ItemRoster();
            foreach (ItemObject it in candidates)
                leftRoster.AddToCounts(new EquipmentElement(it), 1);

            Dictionary<string, int> beforeInv = CaptureRosterCounts(partyRoster);
            Equipment beforeBattle = Hero.MainHero.BattleEquipment.Clone();
            Equipment beforeCivil = Hero.MainHero.CivilianEquipment.Clone();

            string title = $"CGU - Pick {slot}";
            InventoryScreenHelper.OpenScreenAsReceiveItems(leftRoster, new TextObject(title), () =>
            {
                // Detect selection (either moved to inventory OR equipped during the screen)
                string selectedId = TryDetectSelectedItemId(slot, beforeInv, beforeBattle);

                // Rollback everything to avoid exploits / accidental changes
                RestoreEquipmentToSnapshot(Hero.MainHero.BattleEquipment, beforeBattle);
                RestoreEquipmentToSnapshot(Hero.MainHero.CivilianEquipment, beforeCivil);
                RestoreRosterCountsToSnapshot(partyRoster, beforeInv);

                if (!string.IsNullOrEmpty(selectedId))
                {
                    _working.Slots[slot] = selectedId;
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage("[CGU] No item selected. Move one item from the left, then press Done."));
                }

                onClosed?.Invoke();
            });
        }

        private static void RestoreEquipmentToSnapshot(Equipment target, Equipment snapshot)
        {
            if (target == null || snapshot == null)
                return;

            foreach (EquipmentIndex slot in _allEditableSlots)
                target.AddEquipmentToSlotWithoutAgent(slot, snapshot.GetEquipmentFromSlot(slot));
        }

        private static Dictionary<string, int> CaptureRosterCounts(ItemRoster roster)
        {
            var dict = new Dictionary<string, int>(StringComparer.Ordinal);
            if (roster == null)
                return dict;

            for (int i = 0; i < roster.Count; i++)
            {
                ItemObject item = roster.GetItemAtIndex(i);
                if (item == null || string.IsNullOrEmpty(item.StringId))
                    continue;

                dict[item.StringId] = roster.GetElementNumber(i);
            }

            return dict;
        }

        private static void RestoreRosterCountsToSnapshot(ItemRoster roster, Dictionary<string, int> before)
        {
            if (roster == null || before == null)
                return;

            Dictionary<string, int> now = CaptureRosterCounts(roster);
            var keys = new HashSet<string>(before.Keys, StringComparer.Ordinal);
            keys.UnionWith(now.Keys);

            foreach (string id in keys)
            {
                int beforeCount = before.TryGetValue(id, out int b) ? b : 0;
                int nowCount = now.TryGetValue(id, out int n) ? n : 0;
                int diff = nowCount - beforeCount;
                if (diff == 0)
                    continue;

                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(id);
                if (item == null)
                    continue;

                // Apply opposite delta to revert to 'before'
                roster.AddToCounts(new EquipmentElement(item), -diff);
            }
        }

        private string TryDetectSelectedItemId(EquipmentIndex slot, Dictionary<string, int> beforeInv, Equipment beforeBattle)
        {
            HashSet<ItemObject.ItemTypeEnum> allowedTypes = GetAllowedItemTypesForSlot(slot);

            // 1) Detect items moved from the left roster into player inventory (diff > 0)
            ItemRoster partyRoster = MobileParty.MainParty?.ItemRoster;
            if (partyRoster != null)
            {
                Dictionary<string, int> afterInv = CaptureRosterCounts(partyRoster);
                foreach (var kv in afterInv)
                {
                    int beforeCount = beforeInv != null && beforeInv.TryGetValue(kv.Key, out int b) ? b : 0;
                    if (kv.Value <= beforeCount)
                        continue;

                    ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(kv.Key);
                    if (item != null && allowedTypes.Contains(item.ItemType))
                        return kv.Key;
                }
            }

            // 2) If the player equipped something directly, detect by slot change
            EquipmentElement beforeEl = beforeBattle != null ? beforeBattle.GetEquipmentFromSlot(slot) : default;
            EquipmentElement afterEl = Hero.MainHero.BattleEquipment.GetEquipmentFromSlot(slot);

            string beforeId = (!beforeEl.IsEmpty && beforeEl.Item != null) ? beforeEl.Item.StringId : null;
            string afterId = (!afterEl.IsEmpty && afterEl.Item != null) ? afterEl.Item.StringId : null;

            if (!string.IsNullOrEmpty(afterId) && !string.Equals(beforeId, afterId, StringComparison.Ordinal))
            {
                ItemObject item = afterEl.Item;
                if (item != null && allowedTypes.Contains(item.ItemType))
                    return afterId;
            }

            return null;
        }

        private List<ItemObject> GetItemsForSlot(EquipmentIndex slot)
        {
            InventoryItemType type = GetInventoryItemTypeForSlot(slot);
            if (_cachedItemsByType.TryGetValue(type, out var cached))
                return cached;

            HashSet<ItemObject.ItemTypeEnum> allowed = GetAllowedItemTypesForSlot(slot);
            var list = new List<ItemObject>();

            foreach (ItemObject item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                if (item == null || string.IsNullOrEmpty(item.StringId))
                    continue;

                if (!allowed.Contains(item.ItemType))
                    continue;

                list.Add(item);
            }

            list.Sort((a, b) =>
            {
                int n = string.Compare(a.Name.ToString(), b.Name.ToString(), StringComparison.OrdinalIgnoreCase);
                return n != 0 ? n : string.Compare(a.StringId, b.StringId, StringComparison.OrdinalIgnoreCase);
            });

            _cachedItemsByType[type] = list;
            return list;
        }

        private static InventoryItemType GetInventoryItemTypeForSlot(EquipmentIndex slot)
        {
            switch (slot)
            {
                case EquipmentIndex.Head: return InventoryItemType.HeadArmor;
                case EquipmentIndex.Body: return InventoryItemType.BodyArmor;
                case EquipmentIndex.Cape: return InventoryItemType.Cape;
                case EquipmentIndex.Gloves: return InventoryItemType.Gloves;
                case EquipmentIndex.Leg: return InventoryItemType.LegArmor;
                case EquipmentIndex.Horse: return InventoryItemType.Horse;
                case EquipmentIndex.HorseHarness: return InventoryItemType.HorseHarness;
                default: return InventoryItemType.WeaponOrAmmo;
            }
        }

        private static HashSet<ItemObject.ItemTypeEnum> GetAllowedItemTypesForSlot(EquipmentIndex slot)
        {
            switch (slot)
            {
                case EquipmentIndex.Head:
                    return new HashSet<ItemObject.ItemTypeEnum> { ItemObject.ItemTypeEnum.HeadArmor };
                case EquipmentIndex.Body:
                    return new HashSet<ItemObject.ItemTypeEnum> { ItemObject.ItemTypeEnum.BodyArmor };
                case EquipmentIndex.Cape:
                    return new HashSet<ItemObject.ItemTypeEnum> { ItemObject.ItemTypeEnum.Cape };
                case EquipmentIndex.Gloves:
                    return new HashSet<ItemObject.ItemTypeEnum> { ItemObject.ItemTypeEnum.HandArmor };
                case EquipmentIndex.Leg:
                    return new HashSet<ItemObject.ItemTypeEnum> { ItemObject.ItemTypeEnum.LegArmor };
                case EquipmentIndex.Horse:
                    return new HashSet<ItemObject.ItemTypeEnum> { ItemObject.ItemTypeEnum.Horse };
                case EquipmentIndex.HorseHarness:
                    return new HashSet<ItemObject.ItemTypeEnum> { ItemObject.ItemTypeEnum.HorseHarness };
                default:
                    return new HashSet<ItemObject.ItemTypeEnum>
                    {
                        ItemObject.ItemTypeEnum.OneHandedWeapon,
                        ItemObject.ItemTypeEnum.TwoHandedWeapon,
                        ItemObject.ItemTypeEnum.Polearm,
                        ItemObject.ItemTypeEnum.Bow,
                        ItemObject.ItemTypeEnum.Crossbow,
                        ItemObject.ItemTypeEnum.Thrown,
                        ItemObject.ItemTypeEnum.Shield,
                        ItemObject.ItemTypeEnum.Arrows,
                        ItemObject.ItemTypeEnum.Bolts,
                        ItemObject.ItemTypeEnum.Banner,
                    };
            }
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
