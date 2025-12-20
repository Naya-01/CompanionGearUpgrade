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

namespace CompanionGearUpgrades
{
    public enum GearRole
    {
        Infantry,
        Archer,
        Lancer
    }

    public sealed class CompanionGearUpgradeBehaviorV2 : CampaignBehaviorBase
    {
        private GearRole _selectedRole;

        // Default (hardcoded) presets: same behavior as your first working version
        private readonly Dictionary<(GearRole role, int tier), GearPreset> _defaultPresets;

        // User overrides (saved in the savegame). We store only primitives to avoid save-system issues.
        // Key formats:
        //  - cost: "<roleInt>:<tier>"
        //  - slot: "<roleInt>:<tier>:<slotInt>"
        private Dictionary<string, int> _overrideCosts;
        private Dictionary<string, string> _overrideSlots;

        // Effective presets = defaults + overrides (overrides replace defaults)
        private Dictionary<(GearRole role, int tier), GearPreset> _effectivePresets;

        // Config UI state
        private GearRole _cfgRole;
        private int _cfgTier;

        public CompanionGearUpgradeBehaviorV2()
        {
            _defaultPresets = BuildPresets();

            _overrideCosts = new Dictionary<string, int>();
            _overrideSlots = new Dictionary<string, string>();

            RebuildEffectivePresets();
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Persist ONLY overrides. If the user never configures anything, the dictionaries stay empty,
            // and your defaults are used.
            dataStore.SyncData("_cgu_override_costs_v1", ref _overrideCosts);
            dataStore.SyncData("_cgu_override_slots_v1", ref _overrideSlots);

            if (_overrideCosts == null) _overrideCosts = new Dictionary<string, int>();
            if (_overrideSlots == null) _overrideSlots = new Dictionary<string, string>();

            RebuildEffectivePresets();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddDialogs(starter);
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            // Main entry
            starter.AddPlayerLine(
                "cgu_open",
                "hero_main_options",
                "cgu_role_npc",
                "{=cgu_open}Upgrade your equipment",
                IsTalkingToPlayerCompanion,
                null,
                100);

            // Optional: open the simple config UI from conversation
            starter.AddPlayerLine(
                "cgu_open_config",
                "hero_main_options",
                "cgu_config_npc",
                "{=cgu_cfg}Configure upgrade presets",
                IsTalkingToPlayerCompanion,
                null,
                99);

            starter.AddDialogLine(
                "cgu_config_npc_line",
                "cgu_config_npc",
                "hero_main_options",
                "{=cgu_cfg_ack}Sure.",
                null,
                OpenConfigEditor);

            // Role question -> role menu
            starter.AddDialogLine(
                "cgu_role_npc_line",
                "cgu_role_npc",
                "cgu_role_player",
                "{=cgu_role}What equipment style?",
                null,
                null);

            // Role choices -> go to tier NPC
            starter.AddPlayerLine(
                "cgu_role_infantry",
                "cgu_role_player",
                "cgu_tier_npc",
                "{=cgu_role_infantry}Soldier (infantry)",
                () => true,
                () => _selectedRole = GearRole.Infantry);

            starter.AddPlayerLine(
                "cgu_role_archer",
                "cgu_role_player",
                "cgu_tier_npc",
                "{=cgu_role_archer}Archer",
                () => true,
                () => _selectedRole = GearRole.Archer);

            starter.AddPlayerLine(
                "cgu_role_lancer",
                "cgu_role_player",
                "cgu_tier_npc",
                "{=cgu_role_lancer}Lancer (cavalry)",
                () => true,
                () => _selectedRole = GearRole.Lancer);

            // Back from role -> main options
            starter.AddPlayerLine(
                "cgu_back_from_role",
                "cgu_role_player",
                "cgu_back_main_npc",
                "{=cgu_back}Back",
                () => true,
                null);

            starter.AddDialogLine(
                "cgu_back_main_npc_line",
                "cgu_back_main_npc",
                "hero_main_options",
                "{=cgu_back_main}Alright.",
                null,
                null);

            // Tier intro
            starter.AddDialogLine(
                "cgu_tier_npc_line",
                "cgu_tier_npc",
                "cgu_tier_player",
                "{=cgu_tier}Choose a tier (you pay in gold).",
                null,
                null);

            // Tier choices (simple: 3 lines) + show cost before click
            starter.AddPlayerLine(
                "cgu_tier_1",
                "cgu_tier_player",
                "cgu_apply_npc",
                "{=cgu_t1}Tier 1 ({COST_T1} gold)",
                () => SetTierCostVar(1, "COST_T1"),
                () => TryApplyTier(1));

            starter.AddPlayerLine(
                "cgu_tier_2",
                "cgu_tier_player",
                "cgu_apply_npc",
                "{=cgu_t2}Tier 2 ({COST_T2} gold)",
                () => SetTierCostVar(2, "COST_T2"),
                () => TryApplyTier(2));

            starter.AddPlayerLine(
                "cgu_tier_3",
                "cgu_tier_player",
                "cgu_apply_npc",
                "{=cgu_t3}Tier 3 ({COST_T3} gold)",
                () => SetTierCostVar(3, "COST_T3"),
                () => TryApplyTier(3));

            // NPC ack after applying -> return to main options
            starter.AddDialogLine(
                "cgu_apply_npc_line",
                "cgu_apply_npc",
                "hero_main_options",
                "{=cgu_done}Okay.",
                null,
                null);

            // Back from tier -> role menu
            starter.AddPlayerLine(
                "cgu_back_from_tier",
                "cgu_tier_player",
                "cgu_back_role_npc",
                "{=cgu_back2}Back",
                () => true,
                null);

            starter.AddDialogLine(
                "cgu_back_role_npc_line",
                "cgu_back_role_npc",
                "cgu_role_player",
                "{=cgu_back_role}Alright.",
                null,
                null);
        }

        private bool IsTalkingToPlayerCompanion()
        {
            Hero h = Hero.OneToOneConversationHero;
            return h != null && h.IsPlayerCompanion;
        }

        private bool SetTierCostVar(int tier, string varName)
        {
            GearPreset preset;
            if (_effectivePresets.TryGetValue((_selectedRole, tier), out preset))
            {
                MBTextManager.SetTextVariable(varName, preset.Cost);
                return true;
            }

            MBTextManager.SetTextVariable(varName, "-");
            return false;
        }

        private void TryApplyTier(int tier)
        {
            Hero target = Hero.OneToOneConversationHero;
            if (target == null || !target.IsPlayerCompanion)
                return;

            GearPreset preset;
            if (!_effectivePresets.TryGetValue((_selectedRole, tier), out preset))
            {
                InformationManager.DisplayMessage(new InformationMessage($"[CGU] Missing preset for {_selectedRole} T{tier}."));
                return;
            }

            int cost = preset.Cost;
            if (Hero.MainHero.Gold < cost)
            {
                InformationManager.DisplayMessage(new InformationMessage($"Not enough gold. You need {cost}."));
                return;
            }

            if (!TryBuildEquipment(target, preset, out Equipment newEquipment, out string error))
            {
                InformationManager.DisplayMessage(new InformationMessage(error));
                return;
            }

            Hero.MainHero.ChangeHeroGold(-cost);
            EquipmentHelper.AssignHeroEquipmentFromEquipment(target, newEquipment);

            InformationManager.DisplayMessage(new InformationMessage($"{target.Name}: equipment updated (Tier {tier}) for {cost} gold."));
        }

        private bool TryBuildEquipment(Hero target, GearPreset preset, out Equipment equipment, out string error)
        {
            error = null;

            // Clone current equipment to keep slots not handled by the preset
            equipment = target.BattleEquipment.Clone();

            // Player inventory (party item roster)
            MobileParty mainParty = MobileParty.MainParty;
            ItemRoster playerRoster = (mainParty != null) ? mainParty.ItemRoster : null;

            foreach (var kv in preset.Slots)
            {
                EquipmentIndex slot = kv.Key;
                string itemId = kv.Value;

                // Move currently equipped item (for that slot) to player inventory so it isn't lost
                if (playerRoster != null)
                {
                    EquipmentElement oldElement = target.BattleEquipment.GetEquipmentFromSlot(slot);

                    if (!oldElement.IsEmpty && !oldElement.IsQuestItem && !oldElement.IsInvalid())
                    {
                        try
                        {
                            playerRoster.AddToCounts(new EquipmentElement(oldElement), 1);
                        }
                        catch (Exception ex)
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                $"[CGU] Unable to transfer the old item from slot {slot}: {ex.Message}"
                            ));
                        }
                    }
                }

                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                if (item == null)
                {
                    error = $"[CGU] Item not found: '{itemId}'. Check the ID (vanilla/War Sails/mods).";
                    equipment = null;
                    return false;
                }

                equipment.AddEquipmentToSlotWithoutAgent(slot, new EquipmentElement(item));
            }

            return true;
        }

        // ----------------------------
        // Dynamic config UI (simple V1)
        // ----------------------------

        private void OpenConfigEditor()
        {
            ShowRoleSelection();
        }

        private void ShowRoleSelection()
        {
            var options = new List<InquiryElement>
            {
                new InquiryElement(GearRole.Infantry, "Infantry", null),
                new InquiryElement(GearRole.Archer, "Archer", null),
                new InquiryElement(GearRole.Lancer, "Lancer", null),
            };

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "CGU - Role",
                "Select a role to edit:",
                options,
                false,
                1,
                1,
                "Select",
                "Cancel",
                list =>
                {
                    _cfgRole = (GearRole)list[0].Identifier;
                    ShowTierSelection();
                },
                null));
        }

        private void ShowTierSelection()
        {
            var options = new List<InquiryElement>
            {
                new InquiryElement(1, "Tier 1", null),
                new InquiryElement(2, "Tier 2", null),
                new InquiryElement(3, "Tier 3", null),
            };

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "CGU - Tier",
                "Select a tier to edit:",
                options,
                false,
                1,
                1,
                "Select",
                "Back",
                list =>
                {
                    _cfgTier = (int)list[0].Identifier;
                    ShowEditMenu();
                },
                list => ShowRoleSelection()));
        }

        private void ShowEditMenu()
        {
            GearPreset preset = GetEffectivePreset(_cfgRole, _cfgTier);

            string summary =
                $"Role: {_cfgRole}\n" +
                $"Tier: {_cfgTier}\n" +
                $"Cost: {preset.Cost}\n\n" +
                BuildSlotSummary(preset) +
                "\nNote: changes are saved in the savegame once you save your game.";

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
                new InquiryElement("done", "Done", null),
            };

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "CGU - Edit Preset",
                summary,
                options,
                false,
                1,
                1,
                "Select",
                "Back",
                list =>
                {
                    object id = list[0].Identifier;

                    if (id is string s)
                    {
                        if (s == "cost") { PromptSetCost(); return; }
                        if (s == "reset") { ResetTierToDefault(_cfgRole, _cfgTier); ShowEditMenu(); return; }
                        if (s == "done") { InformationManager.DisplayMessage(new InformationMessage("[CGU] Config updated.")); return; }
                    }

                    PromptSetSlot((EquipmentIndex)id);
                },
                list => ShowTierSelection()));
        }

        private void PromptSetCost()
        {
            // FIX: Use InformationManager.ShowTextInquiry (MBInformationManager has no ShowTextInquiry in your version)
            InformationManager.ShowTextInquiry(new TextInquiryData(
                "CGU - Set Price",
                "Enter the price in gold (number):",
                true,
                true,
                "OK",
                "Cancel",
                text =>
                {
                    int value;
                    if (!int.TryParse(text, out value) || value < 0)
                    {
                        InformationManager.DisplayMessage(new InformationMessage("[CGU] Invalid number."));
                        ShowEditMenu();
                        return;
                    }

                    _overrideCosts[CostKey(_cfgRole, _cfgTier)] = value;
                    RebuildEffectivePresets();
                    ShowEditMenu();
                },
                null,
                false,
                input =>
                {
                    string v = (input ?? "").Trim();

                    // you can decide if empty is allowed or not
                    if (string.IsNullOrEmpty(v))
                        return Tuple.Create(false, "Please enter a number.");

                    int value;
                    if (!int.TryParse(v, out value))
                        return Tuple.Create(false, "Please enter a valid integer number.");

                    if (value < 0)
                        return Tuple.Create(false, "Price must be 0 or higher.");

                    return Tuple.Create(true, "");
                }));
        }

        private void PromptSetSlot(EquipmentIndex slot)
        {
            GearPreset current = GetEffectivePreset(_cfgRole, _cfgTier);
            string currentVal = current.Slots.ContainsKey(slot) ? current.Slots[slot] : "";

            // FIX: Use InformationManager.ShowTextInquiry (MBInformationManager has no ShowTextInquiry in your version)
            InformationManager.ShowTextInquiry(new TextInquiryData(
                "CGU - Set Item ID",
                "Enter the item id (example: noble_bow). Leave empty to revert to default.",
                true,
                true,
                "OK",
                "Cancel",
                text =>
                {
                    string trimmed = (text ?? "").Trim();
                    string key = SlotKey(_cfgRole, _cfgTier, slot);

                    if (string.IsNullOrWhiteSpace(trimmed))
                    {
                        _overrideSlots.Remove(key);
                        RebuildEffectivePresets();
                        ShowEditMenu();
                        return;
                    }

                    ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(trimmed);
                    if (item == null)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"[CGU] Item not found: '{trimmed}'"));
                        ShowEditMenu();
                        return;
                    }

                    _overrideSlots[key] = trimmed;
                    RebuildEffectivePresets();
                    ShowEditMenu();
                },
                null,
                false,
                input =>
                {
                    string v = (input ?? "").Trim();

                    // Empty is allowed (means revert)
                    if (string.IsNullOrEmpty(v))
                        return Tuple.Create(true, "");

                    // Validate item id
                    ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(v);
                    if (item == null)
                        return Tuple.Create(false, "[CGU] Item not found.");

                    return Tuple.Create(true, "");
                }));
        }

        private void ResetTierToDefault(GearRole role, int tier)
        {
            _overrideCosts.Remove(CostKey(role, tier));

            string prefix = SlotPrefix(role, tier);
            var toRemove = new List<string>();

            foreach (var k in _overrideSlots.Keys)
            {
                if (k.StartsWith(prefix, StringComparison.Ordinal))
                    toRemove.Add(k);
            }

            foreach (var k in toRemove)
                _overrideSlots.Remove(k);

            RebuildEffectivePresets();
            InformationManager.DisplayMessage(new InformationMessage("[CGU] Tier reset to default."));
        }

        private string BuildSlotSummary(GearPreset preset)
        {
            if (preset.Slots == null || preset.Slots.Count == 0)
                return "Slots: (none)\n";

            string lines = "Slots:\n";
            foreach (var kv in preset.Slots)
                lines += $" - {kv.Key}: {kv.Value}\n";
            return lines;
        }

        private GearPreset GetEffectivePreset(GearRole role, int tier)
        {
            GearPreset p;
            if (_effectivePresets.TryGetValue((role, tier), out p))
                return p;

            return new GearPreset(0, new Dictionary<EquipmentIndex, string>());
        }

        // ----------------------------
        // Preset merge (defaults + overrides)
        // ----------------------------

        private void RebuildEffectivePresets()
        {
            _effectivePresets = new Dictionary<(GearRole role, int tier), GearPreset>();

            foreach (var kv in _defaultPresets)
                _effectivePresets[kv.Key] = ClonePreset(kv.Value);

            foreach (var kv in _overrideCosts)
            {
                GearRole role;
                int tier;
                if (!TryParseCostKey(kv.Key, out role, out tier))
                    continue;

                var key = (role, tier);

                GearPreset basePreset;
                if (!_effectivePresets.TryGetValue(key, out basePreset))
                    basePreset = new GearPreset(0, new Dictionary<EquipmentIndex, string>());

                _effectivePresets[key] = new GearPreset(kv.Value, new Dictionary<EquipmentIndex, string>(basePreset.Slots));
            }

            foreach (var kv in _overrideSlots)
            {
                GearRole role;
                int tier;
                EquipmentIndex slot;
                if (!TryParseSlotKey(kv.Key, out role, out tier, out slot))
                    continue;

                var key = (role, tier);

                GearPreset basePreset;
                if (!_effectivePresets.TryGetValue(key, out basePreset))
                    basePreset = new GearPreset(0, new Dictionary<EquipmentIndex, string>());

                var slots = new Dictionary<EquipmentIndex, string>(basePreset.Slots);
                slots[slot] = kv.Value;

                _effectivePresets[key] = new GearPreset(basePreset.Cost, slots);
            }
        }

        private static GearPreset ClonePreset(GearPreset p)
        {
            return new GearPreset(p.Cost, new Dictionary<EquipmentIndex, string>(p.Slots));
        }

        private static string CostKey(GearRole role, int tier)
        {
            return ((int)role).ToString() + ":" + tier.ToString();
        }

        private static string SlotKey(GearRole role, int tier, EquipmentIndex slot)
        {
            return ((int)role).ToString() + ":" + tier.ToString() + ":" + ((int)slot).ToString();
        }

        private static string SlotPrefix(GearRole role, int tier)
        {
            return ((int)role).ToString() + ":" + tier.ToString() + ":";
        }

        private static bool TryParseCostKey(string key, out GearRole role, out int tier)
        {
            role = GearRole.Infantry;
            tier = 1;

            if (string.IsNullOrEmpty(key)) return false;

            string[] parts = key.Split(':');
            if (parts.Length != 2) return false;

            int r;
            int t;
            if (!int.TryParse(parts[0], out r)) return false;
            if (!int.TryParse(parts[1], out t)) return false;

            role = (GearRole)r;
            tier = t;
            return true;
        }

        private static bool TryParseSlotKey(string key, out GearRole role, out int tier, out EquipmentIndex slot)
        {
            role = GearRole.Infantry;
            tier = 1;
            slot = EquipmentIndex.Weapon0;

            if (string.IsNullOrEmpty(key)) return false;

            string[] parts = key.Split(':');
            if (parts.Length != 3) return false;

            int r;
            int t;
            int s;
            if (!int.TryParse(parts[0], out r)) return false;
            if (!int.TryParse(parts[1], out t)) return false;
            if (!int.TryParse(parts[2], out s)) return false;

            role = (GearRole)r;
            tier = t;
            slot = (EquipmentIndex)s;
            return true;
        }

        // ----------------------------
        // Default presets (your original behavior)
        // ----------------------------

        private static Dictionary<(GearRole, int), GearPreset> BuildPresets()
        {
            return new Dictionary<(GearRole, int), GearPreset>
            {
                // ARCHER
                [(GearRole.Archer, 1)] = new GearPreset(3000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "woodland_yew_bow",
                    [EquipmentIndex.Weapon1] = "default_arrows",
                    [EquipmentIndex.Weapon2] = "battania_sword_4_t4",
                    [EquipmentIndex.Head] = "battania_fur_helmet",
                    [EquipmentIndex.Body] = "ranger_mail",
                    [EquipmentIndex.Cape] = "battania_cloak",
                    [EquipmentIndex.Gloves] = "highland_gloves",
                    [EquipmentIndex.Leg] = "battania_leather_boots",
                }),

                [(GearRole.Archer, 2)] = new GearPreset(8000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "steppe_war_bow",
                    [EquipmentIndex.Weapon1] = "default_arrows",
                    [EquipmentIndex.Weapon2] = "aserai_sword_5_t4",
                    [EquipmentIndex.Head] = "desert_helmet_with_mail",
                    [EquipmentIndex.Body] = "desert_robe_over_mail",
                    [EquipmentIndex.Cape] = "wrapped_scarf",
                    [EquipmentIndex.Leg] = "khuzait_curved_boots",
                }),

                [(GearRole.Archer, 3)] = new GearPreset(20000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "noble_bow",
                    [EquipmentIndex.Weapon1] = "default_arrows",
                    [EquipmentIndex.Weapon2] = "aserai_sword_5_t4",
                    [EquipmentIndex.Weapon3] = "large_adarga",
                }),

                // INFANTRY
                [(GearRole.Infantry, 1)] = new GearPreset(2500, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "battania_sword_4_t4",
                    [EquipmentIndex.Weapon1] = "desert_round_shield",
                    [EquipmentIndex.Weapon2] = "northern_spear_3_t4",
                    [EquipmentIndex.Head] = "battania_fur_helmet",
                    [EquipmentIndex.Body] = "ranger_mail",
                    [EquipmentIndex.Cape] = "battania_cloak",
                    [EquipmentIndex.Gloves] = "highland_gloves",
                    [EquipmentIndex.Leg] = "battania_leather_boots",
                }),

                [(GearRole.Infantry, 2)] = new GearPreset(9000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "northern_spear_2_t3",
                    [EquipmentIndex.Weapon1] = "northern_round_shield",
                    [EquipmentIndex.Weapon2] = "sturgia_axe_3_t3",
                    [EquipmentIndex.Head] = "sturgian_helmet_base",
                    [EquipmentIndex.Body] = "northern_padded_gambeson",
                    [EquipmentIndex.Cape] = "wrapped_scarf",
                    [EquipmentIndex.Gloves] = "buttoned_leather_bracers",
                    [EquipmentIndex.Leg] = "highland_boots",
                }),

                [(GearRole.Infantry, 3)] = new GearPreset(22000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Weapon0] = "northern_spear_3_t4",
                    [EquipmentIndex.Weapon1] = "heavy_round_shield",
                    [EquipmentIndex.Weapon2] = "sturgia_axe_4_t4",
                    [EquipmentIndex.Weapon3] = "northern_throwing_axe_1_t1",
                    [EquipmentIndex.Head] = "sturgian_lord_helmet_b",
                    [EquipmentIndex.Body] = "northern_coat_of_plates",
                    [EquipmentIndex.Cape] = "battania_civil_cape",
                    [EquipmentIndex.Gloves] = "reinforced_leather_vambraces",
                    [EquipmentIndex.Leg] = "fine_town_boots",
                }),

                // CAVALRY (LANCER)
                [(GearRole.Lancer, 1)] = new GearPreset(6000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Horse] = "aserai_horse",
                    [EquipmentIndex.HorseHarness] = "aseran_village_harness",
                    [EquipmentIndex.Weapon0] = "eastern_spear_3_t3",
                    [EquipmentIndex.Weapon1] = "studded_bound_kite_shield",
                    [EquipmentIndex.Weapon2] = "aserai_sword_1_t2",
                    [EquipmentIndex.Weapon3] = "eastern_javelin_2_t3",
                    [EquipmentIndex.Head] = "trailed_desert_helmet",
                    [EquipmentIndex.Cape] = "wrapped_scarf",
                    [EquipmentIndex.Body] = "studded_leather_coat",
                    [EquipmentIndex.Gloves] = "buttoned_leather_bracers",
                    [EquipmentIndex.Leg] = "steppe_leather_boots",
                }),

                [(GearRole.Lancer, 2)] = new GearPreset(12000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Horse] = "t2_aserai_horse",
                    [EquipmentIndex.HorseHarness] = "chain_horse_harness",
                    [EquipmentIndex.Weapon0] = "eastern_spear_4_t4",
                    [EquipmentIndex.Weapon1] = "bound_adarga",
                    [EquipmentIndex.Weapon2] = "aserai_sword_3_t3",
                    [EquipmentIndex.Head] = "trailed_desert_helmet",
                    [EquipmentIndex.Cape] = "wrapped_scarf",
                    [EquipmentIndex.Body] = "belted_leather_cuirass",
                    [EquipmentIndex.Gloves] = "rough_tied_bracers",
                    [EquipmentIndex.Leg] = "strapped_mail_chausses",
                }),

                [(GearRole.Lancer, 3)] = new GearPreset(25000, new Dictionary<EquipmentIndex, string>
                {
                    [EquipmentIndex.Horse] = "t2_aserai_horse",
                    [EquipmentIndex.HorseHarness] = "chain_horse_harness",
                    [EquipmentIndex.Weapon0] = "eastern_spear_4_t4",
                    [EquipmentIndex.Weapon1] = "bound_adarga",
                    [EquipmentIndex.Weapon2] = "aserai_sword_4_t4",
                    [EquipmentIndex.Weapon3] = "eastern_javelin_3_t4",
                    [EquipmentIndex.Head] = "desert_helmet_with_mail",
                    [EquipmentIndex.Cape] = "wrapped_scarf",
                    [EquipmentIndex.Body] = "northern_lamellar_armor",
                    [EquipmentIndex.Gloves] = "reinforced_leather_vambraces",
                    [EquipmentIndex.Leg] = "eastern_leather_boots",
                }),
            };
        }
    }

    public sealed class GearPreset
    {
        public int Cost { get; }
        public Dictionary<EquipmentIndex, string> Slots { get; }

        public GearPreset(int cost, Dictionary<EquipmentIndex, string> slots)
        {
            Cost = cost;
            Slots = slots;
        }
    }
}
