using CompanionGearUpgrades.Domain;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace CompanionGearUpgrades.Data
{
    public static class GearPresetRepository
    {
        public static Dictionary<(GearRole role, int tier), GearPreset> BuildPresets()
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
}
