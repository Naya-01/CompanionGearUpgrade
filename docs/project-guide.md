# Companion Gear Upgrades — Project Guide

> English version. [UI tutorial](../README.md) · [Version française](project-guide.fr.md) · [Tutoriel UI en français](README.fr.md)

Single-player module for **Mount & Blade II: Bannerlord** that lets the player define, save, and buy equipment presets for companions and heroes in the player's clan.

The module strictly separates default presets, player changes, and the editor's temporary state. The same configuration is available from **Clan > Equipment** and a companion dialogue; both entry points open the exact same Gauntlet window and use the same saved data.

## Features

- Three preset families: `Infantry`, `Archer`, and `Lancer`.
- Three independent tiers per role, each with its own equipment and cost.
- Gauntlet editor: role → tier → category → slot → item.
- Catalog of compatible game and mod items, with filters, search, and price sorting.
- Built-in icons, statistics tooltip, optional comparison, and 3D preview.
- Customizable tier price or price calculated from tier equipment.
- Override persistence in the campaign save through `SyncData`.
- Removed slots are actually cleared during upgrade; an empty slot does not restore its default item.
- Dialogue purchase flow: gold validation, return of prior items to party inventory, then equipment application.

## Included presets

Default item `StringId` values and costs are centralized in [Data/GearPresetRepository.cs](../Data/GearPresetRepository.cs).

| Role | Tier 1 | Tier 2 | Tier 3 |
| --- | ---: | ---: | ---: |
| Infantry | 2,500 gold | 9,000 gold | 22,000 gold |
| Archer | 3,000 gold | 8,000 gold | 20,000 gold |
| Lancer | 6,000 gold | 12,000 gold | 25,000 gold |

Costs shown in dialogue and the editor are effective costs: a persisted override is used when present; otherwise the default preset cost is used.

## Prerequisites and installation

The project targets **.NET Framework 4.7.2**. It is built against the Bannerlord installation configured in [CompanionGearUpgrade.csproj](../CompanionGearUpgrade.csproj).

The [SubModule.xml](../SubModule.xml) manifest declares these dependencies:

- Native, SandBoxCore, Sandbox, CustomBattle, and StoryMode;
- Bannerlord.Harmony `v2.2.2`;
- Bannerlord.UIExtenderEx `v2.13.2`.

Harmony and UIExtenderEx must be installed and enabled with the module. The current build paths use these Workshop locations:

| Dependency | Current reference path |
| --- | --- |
| Harmony | `E:\SteamLibrary\steamapps\workshop\content\261550\2859188632` |
| UIExtenderEx | `E:\SteamLibrary\steamapps\workshop\content\261550\2859222409` |

External references deliberately use `Private=False`. Never copy `0Harmony.dll`, `Bannerlord.Harmony.dll`, or `Bannerlord.UIExtenderEx.dll` into the module folder.

To develop on another machine, update only the local `HintPath` values in the `.csproj` for your Bannerlord and Workshop installations, while keeping `Private=False` unchanged.

## Project organization

```text
CompanionGearUpgrade/
├─ Behaviors/                  Campaign lifecycle and persistence
├─ Data/                       Presets, snapshots, and persisted overrides
├─ Dialog/                     Purchase dialogue and configuration entry point
├─ Domain/                     Simple domain types: role and preset
├─ Services/                   Rules for applying an upgrade
├─ UI/                         Gauntlet host, navigation, and root ViewModel
│  └─ ViewModels/              One small ViewModel for each UI list type
├─ GUI/Prefabs/                Main Gauntlet window
├─ GUI/PrefabExtensions/       Button injected into the Clan screen
├─ SubModule.cs                UIExtenderEx, Harmony, and behavior initialization
└─ SubModule.xml               Bannerlord module declaration
```

| Component | Responsibility |
| --- | --- |
| [SubModule.cs](../SubModule.cs) | Enables UIExtenderEx, applies Harmony patches, adds the campaign behavior, and releases UI at game end. |
| [CompanionGearUpgradeBehavior.cs](../Behaviors/CompanionGearUpgradeBehavior.cs) | Owns saved dictionaries, synchronizes them through `SyncData`, builds session services, and retains the one shared configuration view. |
| [GearPresetRepository.cs](../Data/GearPresetRepository.cs) | Defines the nine default presets. |
| [GearPresetOverrides.cs](../Data/GearPresetOverrides.cs) | Merges defaults with overrides and persists only differences. |
| [GearPresetSnapshot.cs](../Data/GearPresetSnapshot.cs) | Isolated copy of the tier being edited. |
| [CompanionGearUpgradeService.cs](../Services/CompanionGearUpgradeService.cs) | Provides compatible items and applies a tier to a hero. |
| [CompanionGearUpgradeDialog.cs](../Dialog/CompanionGearUpgradeDialog.cs) | Adds dialogue lines for purchasing and opening configuration. |
| [EquipmentConfigView.cs](../UI/EquipmentConfigView.cs) | Hosts the Gauntlet movie, focus, lifecycle, and preview preparation. |
| [GearPresetConfigViewModel.cs](../UI/GearPresetConfigViewModel.cs) and partial files | Window state, navigation, catalog, snapshot, tooltip, and preview. |
| [EquipmentConfigWindow.xml](../GUI/Prefabs/EquipmentConfigWindow.xml) | Configuration window prefab. |

### Root ViewModel split by responsibility

`GearPresetConfigViewModel` is one Gauntlet class deliberately split into partial files to avoid a monolithic file:

- [GearPresetConfigViewModel.cs](../UI/GearPresetConfigViewModel.cs): root state, Save/Exit commands, price, and snapshot.
- [GearPresetConfigViewModel.Navigation.cs](../UI/GearPresetConfigViewModel.Navigation.cs): role, tier, category, and slot.
- [GearPresetConfigViewModel.Items.cs](../UI/GearPresetConfigViewModel.Items.cs): filters, search, sorting, selection, and inspection.
- [GearPresetConfigViewModel.State.cs](../UI/GearPresetConfigViewModel.State.cs): page changes, notifications, and dirty-state detection.
- [GearPresetConfigViewModel.Preview.cs](../UI/GearPresetConfigViewModel.Preview.cs): 3D preview lifecycle.
- [GearPresetConfigViewModel.Formatting.cs](../UI/GearPresetConfigViewModel.Formatting.cs): names, categories, and display helpers.

Role, tier, category, slot, filter, sort, item, and tooltip options each have their own file in [UI/ViewModels](../UI/ViewModels). That keeps each class focused and makes targeted changes easier.

## Module lifecycle

1. `SubModule.OnSubModuleLoad()` creates and enables UIExtenderEx, then applies assembly Harmony patches.
2. `SubModule.OnGameStart()` adds `CompanionGearUpgradeBehavior` to a campaign.
3. At session launch, `CompanionGearUpgradeBehavior.OnSessionLaunched()`:
   - builds default presets;
   - connects save-restored dictionaries to `GearPresetOverrides`;
   - creates one `CompanionGearUpgradeService`;
   - registers dialogue lines;
   - initializes one `EquipmentConfigView`.
4. At game end, the view is released. When the module unloads, UIExtenderEx is deregistered and module patches are removed.

Clan and dialogue entries therefore share the exact same service, overrides, and window; there is no legacy second configurator to maintain.

## Accessing configuration

### From Clan > Equipment

The module does not modify Native or SandBox XML.

1. [ClanEquipmentTabExtension.cs](../UI/ClanEquipmentTabExtension.cs) loads [ClanEquipmentTab.xml](../GUI/PrefabExtensions/ClanEquipmentTab.xml) through UIExtenderEx.
2. The prefab adds the **Equipment** button after native Clan tabs. It calls `SetSelectedCategory(4)`.
3. [ClanEquipmentTabHarmonyPatch.cs](../UI/ClanEquipmentTabHarmonyPatch.cs) intercepts that reserved category before Native.
4. The patch asks `CompanionGearUpgradeBehavior` to open the Gauntlet view, then prevents Native from handling category `4`.

Opening is accepted only from a `GauntletClanScreen`. The check also covers the derived Naval DLC Clan-screen variant.

### From a companion dialogue

The dialogue is available only for a player companion or a hero who belongs to the player's clan.

- **Upgrade your equipment** keeps the purchase path: role → tier → preset application.
- **Configure upgrade presets** opens the same Gauntlet view as **Clan > Equipment**.

From dialogue, opening is deliberately postponed until the next global-layer tick. Bannerlord can finish `DoOptionContinue` before the modal layer takes focus, so the player does not need to leave or reload dialogue.

## Gauntlet window and navigation

`EquipmentConfigView` retains a global `GauntletLayer` with priority `1000`, named `CompanionGearUpgradeEquipmentConfig`. The `EquipmentConfigWindow` movie is loaded only when configuration is opened.

While the window is open and its host screen is valid, the layer is modal: it takes focus and blocks input to the screen underneath. If the Clan screen or conversation disappears, configuration closes. Unsaved changes are then discarded with an explicit message.

The path is fixed:

```text
Role → Tier → Category → Slot → Items
```

| Page | Contents | Main action |
| --- | --- | --- |
| Role | Infantry, Archer, Lancer | Choose the preset family. |
| Tier | Tiers 1 through 3 and their effective price | Load an editable tier snapshot. |
| Category | Weapons, Armors, Horse; tier actions | Change price, reset, or open a category. |
| Slot | Category slots and their temporarily configured item | Open the compatible catalog. |
| Items | Catalog, filters, search, preview, and tooltip | Select or remove an item in the snapshot. |

Categories group slots as follows:

| Category | Slots |
| --- | --- |
| Weapons | `Weapon0`, `Weapon1`, `Weapon2`, `Weapon3` |
| Armors | `Head`, `Body`, `Cape`, `Gloves`, `Leg` |
| Horse | `Horse`, `HorseHarness` |

**Back** goes up one page. From the Role page, it uses the same logic as **Exit**.

### Save, Exit, and temporary changes

Only one tier is edited at a time. Loading a new role/tier creates two independent copies:

- `_working`: editable temporary snapshot.
- `_savedSnapshot`: last saved version, used to detect unsaved changes.

**Select**, **Remove**, **Reset this tier to default**, **Set price (gold)**, and **Calculate from equipment** change only `_working`.

- **Save** is visible on the Category page. It calls `CommitSnapshot`, updates the saved reference, and **keeps the window open**.
- **Exit** closes immediately when no difference exists. Otherwise, a confirmation offers **Exit without saving** or **Keep editing**.
- Changing tier with unsaved changes also asks for confirmation before discarding the working snapshot.
- On the Items page, **Select** and **Remove** keep the page open so changes can be chained.

After Save, overrides exist in campaign memory. They become durable in the save file when Bannerlord saves the campaign.

## Data, snapshots, and persistence

### Data model

One default `GearPreset` associates a cost with a dictionary:

```text
EquipmentIndex → ItemObject.StringId
```

Original presets are not modified during editing. The view works with `GearPresetSnapshot`, which copies the cost and slot dictionary; `Clone()` ensures that the saved reference cannot be altered by a temporary action.

Editable slots, stable transfer names, groups, and compatible item types are defined once by `GearSlotCatalog`:

```text
Weapon0, Weapon1, Weapon2, Weapon3,
Head, Body, Cape, Gloves, Leg,
Horse, HorseHarness
```

### Persisted overrides

`CompanionGearUpgradeBehavior.SyncData()` stores only Bannerlord-save-compatible structures:

```csharp
Dictionary<string, int>    // CGU_CostOverrides
Dictionary<string, string> // CGU_ItemOverrides
```

Keys are stable:

```text
{Role}:{Tier}:cost
{Role}:{Tier}:{numeric EquipmentIndex}
```

| Saved slot state | Meaning |
| --- | --- |
| No override entry | Use the default preset item. |
| Item `StringId` | Replace the default item. |
| `__CGU_EMPTY_SLOT__` | The player deliberately removed the item; the slot must remain empty. |

**Remove** writes `null` into the snapshot. At commit time, that `null` becomes `__CGU_EMPTY_SLOT__`: it must never be confused with no override, which means “use the default.”

A cost is stored only if it differs from the default cost and is always clamped to zero or above. Item differences are stored only for slots present in the snapshot, preserving the difference between a slot absent from the default preset and a slot explicitly cleared.

## Item catalog

When a slot is selected, `CompanionGearUpgradeService.GetCompatibleItems()` enumerates `ItemObject` instances currently registered by Bannerlord. Items added by mods can therefore appear if they have a compatible type.

| Slot | Allowed types |
| --- | --- |
| `Weapon0` through `Weapon3` | OneHandedWeapon, TwoHandedWeapon, Polearm, Bow, Crossbow, Thrown, Shield, Arrows, Bolts, Banner |
| `Head` | HeadArmor |
| `Body` | BodyArmor |
| `Cape` | Cape |
| `Gloves` | HandArmor |
| `Leg` | LegArmor |
| `Horse` | Horse |
| `HorseHarness` | HorseHarness |

The Items page offers:

- **All**, then filters for types actually present.
- Case-insensitive search across name and `StringId`.
- Ascending or descending price sorting, then name and identifier for stable output.
- A built-in icon through `ItemImageIdentifierVM`.
- A fully clickable row with price, type, and identifier.

Filters, sorting buttons, and item rows are real radio `ButtonWidget` instances. `IsSelected`, `DominantSelectedState`, and `UpdateChildrenStates` drive built-in visual states: normal, hovered, pressed, and selected. The active filter or item state remains visible until another selection.

## Tooltip and comparison

Inspection priority is always:

```text
hovered item → selected candidate → configured item → no tooltip
```

`HasInspectionItem` depends only on the actually inspected item. It does not depend on comparison or on a configured item already existing in the slot.

Consequences:

- An empty slot immediately shows the full sheet of a hovered or selected item.
- After **Remove**, the next hover or click still shows statistics.
- Hovering the same item as the selected candidate or configured item shows one sheet.
- Hovering an item different from the configured item can show a two-column comparison.
- When hover ends, the sheet returns automatically to the selected candidate, then to the configured item.

Comparison is additional information, never a prerequisite for displaying item statistics. `GearItemTooltipViewModel` creates rows with `ItemMenuTooltipPropertyVM`, without using `InventoryScreenHelper` or `SPInventoryVM`. It displays available type, tier, value, weight, and armor, horse, or weapon statistics.

## 3D preview

The window owns one `ItemPreviewVM` per movie. Preview uses the same priority as the tooltip: hovered item, then selected candidate, then configured item.

The `ItemTableauWidget` has the `CGUPreviewTableau` identifier. `EquipmentConfigView.OnGauntletTick()` allows `ItemPreviewVM.Open()` only when that widget:

1. is connected to the Gauntlet root;
2. is actually visible;
3. has a native `TextureProvider`.

After native opening, the ViewModel still waits for texture stabilization and a valid texture before declaring preview ready. On exception or texture timeout, it performs at most three attempts in total. This context wait is essential: calling `Open()` too early is the usual cause of a blank 3D preview on first opening.

When leaving Items or closing the window, the native preview is closed and the tableau is cleared. `ItemPreviewVM.OnFinalize()` is called only when `EquipmentConfigView` releases the movie; it then finalizes the preview ViewModel correctly.

## Applying an upgrade from dialogue

`CompanionGearUpgradeService.TryApplyTier(role, tier)` applies the selected tier to the conversation hero:

1. validates that the hero is a player companion or belongs to the player clan;
2. retrieves the preset and effective cost;
3. checks `Hero.MainHero` gold;
4. merges default preset and overrides;
5. resolves **all** `StringId` values before changing anything;
6. clones the hero’s battle equipment;
7. returns changed old items to `MobileParty.MainParty`, except quest or invalid items;
8. writes every editable slot, including missing or `null` slots that are explicitly cleared;
9. removes gold and assigns the new equipment.

If a `StringId` cannot be found, the operation is canceled before equipment or gold changes. This cleanly identifies a preset that became incompatible with the game, DLC, or a missing mod.

## Adding or changing content

### Change an existing preset

Edit [GearPresetRepository.cs](../Data/GearPresetRepository.cs): the cost and `EquipmentIndex → StringId` dictionary for the relevant role/tier.

Use `StringId` values that are actually present among loaded `ItemObject` instances. An invalid identifier deliberately blocks the upgrade with an explicit message.

### Add a role or tier

Adding a role or tier requires updates at all three levels:

1. [Domain/GearRole.cs](../Domain/GearRole.cs) and the preset repository.
2. Options built in `GearPresetConfigViewModel` (hardcoded `Infantry`, `Archer`, `Lancer`, and the `1..3` tier loop).
3. Purchase dialogue lines in [CompanionGearUpgradeDialog.cs](../Dialog/CompanionGearUpgradeDialog.cs).

The dialogue and UI do not discover a fourth tier automatically; that limit is explicit in the current code.

### Add a slot or category

To add a **slot**, define its order, stable JSON name, group, and compatible item type once in `GearSlotCatalog`, then update default presets and empty-slot tests as needed. Because schema v1 requires every supported slot explicitly, changing that set also requires a deliberate transfer-schema version change.

To add a **category**, also add:

1. a value to `GearPresetCategory` in [GearPresetConfigTypes.cs](../UI/GearPresetConfigTypes.cs);
2. its option in `LoadTier()`; the three current categories are hardcoded there;
3. an explicit case in `GetSlotsForCategory()`; its current `default` returns Horse slots;
4. the category brush in `GearCategoryOptionViewModel.GetIconBrush()`; its `default` uses the mounts brush.

Missing any one of these points often creates an option that is visible but not correctly saved or applied.

### Change the interface

- The main prefab is [GUI/Prefabs/EquipmentConfigWindow.xml](../GUI/Prefabs/EquipmentConfigWindow.xml).
- The Clan extension is [GUI/PrefabExtensions/ClanEquipmentTab.xml](../GUI/PrefabExtensions/ClanEquipmentTab.xml).
- Do not modify Native or SandBox XML.
- Every property bound from XML must be exposed with `DataSourceProperty` from the appropriate ViewModel.
- Every new C# class must be added explicitly as `<Compile Include="…">` in the `.csproj`, because this legacy project does not automatically compile new files.

## Build and deployment

Run all commands in this section from the repository root.

### Validate XML before building

```powershell
@(
  'CompanionGearUpgrade.csproj',
  'SubModule.xml',
  'GUI\Prefabs\EquipmentConfigWindow.xml',
  'GUI\PrefabExtensions\ClanEquipmentTab.xml'
) | ForEach-Object {
  [xml](Get-Content -LiteralPath $_ -Raw) | Out-Null
  Write-Host "Valid XML: $_"
}
```

Build Release with:

```powershell
dotnet build CompanionGearUpgrade.csproj -c Release --no-restore
```

The Release build produces:

```text
bin\Release\CompanionGearUpgrade.dll
```

For this installation, the target module is:

```text
E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CompanionGearUpgrade
```

After a successful build, deploy only these four files:

```powershell
Copy-Item -LiteralPath 'bin\Release\CompanionGearUpgrade.dll' -Destination 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CompanionGearUpgrade\bin\Win64_Shipping_Client\CompanionGearUpgrade.dll' -Force
Copy-Item -LiteralPath 'SubModule.xml' -Destination 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CompanionGearUpgrade\SubModule.xml' -Force
Copy-Item -LiteralPath 'GUI\Prefabs\EquipmentConfigWindow.xml' -Destination 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CompanionGearUpgrade\GUI\Prefabs\EquipmentConfigWindow.xml' -Force
Copy-Item -LiteralPath 'GUI\PrefabExtensions\ClanEquipmentTab.xml' -Destination 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CompanionGearUpgrade\GUI\PrefabExtensions\ClanEquipmentTab.xml' -Force
```

Do not deploy the PDB or any Workshop dependency. The `.csproj` has no post-build copy target: deployment is deliberately explicit and limited to those four artifacts.

After copying, use `Get-FileHash -Algorithm SHA256` on each source and target. Every pair must have identical hashes.

## Recommended manual verification

After building and deploying:

1. Open **Clan > Equipment**: the window opens, but nothing opens automatically at campaign load.
2. Open a companion dialogue, choose **Configure upgrade presets**: the same window opens.
3. Change an item, then **Save**: the window stays open and the item/price remains displayed.
4. Change an item, then **Exit**: verify the discard confirmation.
5. Remove an item, save, buy the tier: the hero slot must be empty.
6. Test an empty slot: hovering or selecting an item must immediately display its statistics.
7. Test comparison: hover an item different from the configured item, without selecting that hovered item, and verify two columns; end hover and verify the simple sheet returns.
8. Open the first Items list: the 3D preview must load without navigating back or reloading.

## Troubleshooting

| Symptom | Useful checks |
| --- | --- |
| `[CGU] Preset configuration is not ready yet.` | The campaign session must have reached `OnSessionLaunched`. Verify that the module, Harmony, and UIExtenderEx are loaded; also confirm the current host context is valid. |
| Window does not open from Clan | Open the actual Clan screen; reserved category `4` is intercepted only in that context. |
| Window does not open from dialogue | Verify an active conversation with a companion or player-clan hero. Opening is deliberately delayed by one tick. |
| No items in the catalog | Check the slot, filter, search, and allowed item types. |
| `[CGU] Item not found: ...` | Correct the preset `StringId` or install the content that provides it. No partial equipment should be applied in this case. |
| 3D preview temporarily unavailable | Verify that `CGUPreviewTableau` remains intact in the prefab. Preview waits for the Gauntlet context and retries automatically; a new hover retries after failure. |
| Changes disappear after reload | Click Save in the configurator, then save the Bannerlord campaign. |

## License

The repository contains an MIT license in [LICENSE.txt](../LICENSE.txt). Its attribution fields (`[year]`, `[fullname]`) are still placeholders and should be filled before public distribution.
