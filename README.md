# Companion Gear Upgrades

Module solo pour **Mount & Blade II: Bannerlord** permettant de définir, sauvegarder et acheter des presets d’équipement pour les compagnons et les héros du clan du joueur.

Le module sépare strictement les presets fournis par défaut, les modifications du joueur et l’état temporaire de l’éditeur. La même configuration est accessible depuis **Clan > Equipment** et depuis le dialogue d’un compagnon ; ces deux entrées ouvrent exactement la même fenêtre Gauntlet et utilisent les mêmes données sauvegardées.

## Fonctionnalités

- Trois familles de presets : `Infantry`, `Archer` et `Lancer`.
- Trois tiers indépendants par rôle, avec équipement et coût propres.
- Éditeur Gauntlet : rôle → tier → catégorie → slot → objet.
- Catalogue d’objets du jeu et des mods compatibles, avec filtres, recherche et tri par prix.
- Icônes, tooltip de statistiques, comparaison optionnelle et prévisualisation 3D natives.
- Prix personnalisable ou calculable à partir de l’équipement du tier.
- Sauvegarde des overrides dans la sauvegarde de campagne via `SyncData`.
- Les slots retirés sont réellement vidés lors de l’upgrade ; un slot vide ne réapparaît pas avec l’objet par défaut.
- Achat depuis le dialogue : contrôle de l’or, transfert des anciens objets à l’inventaire du groupe, puis application de l’équipement.

## Presets fournis

Les valeurs et les `StringId` d’objets par défaut sont centralisés dans [Data/GearPresetRepository.cs](Data/GearPresetRepository.cs).

| Rôle | Tier 1 | Tier 2 | Tier 3 |
| --- | ---: | ---: | ---: |
| Infantry | 2 500 or | 9 000 or | 22 000 or |
| Archer | 3 000 or | 8 000 or | 20 000 or |
| Lancer | 6 000 or | 12 000 or | 25 000 or |

Les coûts affichés dans le dialogue et dans l’éditeur sont les coûts effectifs : l’override sauvegardé est utilisé s’il existe, sinon le coût du dépôt de presets est utilisé.

## Prérequis et installation

Le projet cible **.NET Framework 4.7.2**. Il est construit contre l’installation Bannerlord configurée dans [CompanionGearUpgrade.csproj](CompanionGearUpgrade.csproj).

Le manifeste [SubModule.xml](SubModule.xml) déclare les dépendances suivantes :

- Native, SandBoxCore, Sandbox, CustomBattle et StoryMode ;
- Bannerlord.Harmony `v2.2.2` ;
- Bannerlord.UIExtenderEx `v2.13.2`.

Harmony et UIExtenderEx doivent être installés et activés avec le module. Les chemins de compilation actuels utilisent les contenus Workshop suivants :

Les références externes sont volontairement en `Private=False`. Ne copiez jamais `0Harmony.dll`, `Bannerlord.Harmony.dll` ou `Bannerlord.UIExtenderEx.dll` dans le dossier du module.

Pour développer sur une autre machine, adaptez uniquement les `HintPath` locaux du `.csproj` à votre installation de Bannerlord et de Workshop, sans changer les valeurs `Private=False`.

## Organisation du projet

```text
CompanionGearUpgrade/
├─ Behaviors/                  Cycle de vie de campagne et persistance
├─ Data/                       Presets, snapshots et overrides sauvegardés
├─ Dialog/                     Dialogue d’achat et point d’entrée du configurateur
├─ Domain/                     Types métier simples : rôle et preset
├─ Services/                   Règles d’application d’un upgrade
├─ UI/                         Hôte Gauntlet, navigation et ViewModel principal
│  └─ ViewModels/              Un petit ViewModel par type de liste UI
├─ GUI/Prefabs/                Fenêtre Gauntlet principale
├─ GUI/PrefabExtensions/       Bouton injecté dans l’écran Clan
├─ SubModule.cs                Initialisation UIExtenderEx, Harmony et comportement
└─ SubModule.xml               Déclaration du module Bannerlord
```

| Élément | Responsabilité |
| --- | --- |
| [SubModule.cs](SubModule.cs) | Active UIExtenderEx, applique les patches Harmony, ajoute le comportement de campagne et libère l’UI à la fin de partie. |
| [CompanionGearUpgradeBehavior.cs](Behaviors/CompanionGearUpgradeBehavior.cs) | Possède les dictionnaires sauvegardés, les synchronise via `SyncData`, construit les services de session et garde l’unique vue de configuration partagée. |
| [GearPresetRepository.cs](Data/GearPresetRepository.cs) | Définit les neuf presets par défaut. |
| [GearPresetOverrides.cs](Data/GearPresetOverrides.cs) | Fusionne defaults et overrides, puis ne sauvegarde que les différences. |
| [GearPresetSnapshot.cs](Data/GearPresetSnapshot.cs) | Copie isolée du tier en cours d’édition. |
| [CompanionGearUpgradeService.cs](Services/CompanionGearUpgradeService.cs) | Fournit les objets compatibles et applique un tier à un héros. |
| [CompanionGearUpgradeDialog.cs](Dialog/CompanionGearUpgradeDialog.cs) | Ajoute les lignes de dialogue d’achat et l’accès au configurateur. |
| [EquipmentConfigView.cs](UI/EquipmentConfigView.cs) | Héberge le movie Gauntlet, son focus, son cycle de vie et la préparation de la preview. |
| [GearPresetConfigViewModel.cs](UI/GearPresetConfigViewModel.cs) et ses fichiers partiels | État, navigation, catalogue, snapshot, tooltip et preview de la fenêtre. |
| [EquipmentConfigWindow.xml](GUI/Prefabs/EquipmentConfigWindow.xml) | Prefab de la fenêtre de configuration. |

### ViewModel principal réparti par responsabilité

`GearPresetConfigViewModel` est une seule classe Gauntlet, volontairement répartie en fichiers partiels pour éviter un fichier monolithique :

- [GearPresetConfigViewModel.cs](UI/GearPresetConfigViewModel.cs) : état racine, commandes Save/Exit, prix et snapshot ;
- [GearPresetConfigViewModel.Navigation.cs](UI/GearPresetConfigViewModel.Navigation.cs) : rôle, tier, catégorie et slot ;
- [GearPresetConfigViewModel.Items.cs](UI/GearPresetConfigViewModel.Items.cs) : filtres, recherche, tri, sélection et inspection ;
- [GearPresetConfigViewModel.State.cs](UI/GearPresetConfigViewModel.State.cs) : changement de page, notifications et détection des modifications ;
- [GearPresetConfigViewModel.Preview.cs](UI/GearPresetConfigViewModel.Preview.cs) : cycle de vie de la prévisualisation 3D ;
- [GearPresetConfigViewModel.Formatting.cs](UI/GearPresetConfigViewModel.Formatting.cs) : noms, catégories et helpers de présentation.

Les options de rôle, tier, catégorie, slot, filtre, tri, objet et tooltip ont chacune leur propre fichier dans [UI/ViewModels](UI/ViewModels). Cela limite les responsabilités de chaque classe et facilite les modifications ciblées.

## Cycle de vie du module

1. `SubModule.OnSubModuleLoad()` crée et active UIExtenderEx, puis applique les patches Harmony de l’assembly.
2. `SubModule.OnGameStart()` ajoute `CompanionGearUpgradeBehavior` à une campagne.
3. Au lancement de session, `CompanionGearUpgradeBehavior.OnSessionLaunched()` :
   - construit les presets par défaut ;
   - relie les dictionnaires restaurés de la sauvegarde à `GearPresetOverrides` ;
   - crée un unique `CompanionGearUpgradeService` ;
   - enregistre les dialogues ;
   - initialise une unique `EquipmentConfigView`.
4. À la fin de la partie, la vue est libérée. Au déchargement du module, UIExtenderEx est désenregistré et les patches du module sont retirés.

Les entrées Clan et dialogue partagent donc exactement le même service, les mêmes overrides et la même fenêtre ; il n’existe pas de second configurateur historique à maintenir.

## Accès à la configuration

### Depuis Clan > Equipment

Le module ne modifie aucun XML Native ou SandBox.

1. [ClanEquipmentTabExtension.cs](UI/ClanEquipmentTabExtension.cs) charge [ClanEquipmentTab.xml](GUI/PrefabExtensions/ClanEquipmentTab.xml) via UIExtenderEx.
2. Le prefab ajoute le bouton **Equipment** après les onglets Clan natifs. Il appelle `SetSelectedCategory(4)`.
3. [ClanEquipmentTabHarmonyPatch.cs](UI/ClanEquipmentTabHarmonyPatch.cs) intercepte cette catégorie réservée avant Native.
4. Le patch demande à `CompanionGearUpgradeBehavior` d’ouvrir la vue Gauntlet, puis empêche Native de traiter la catégorie `4`.

L’ouverture n’est acceptée que depuis un `GauntletClanScreen`. La vérification couvre aussi la variante Clan dérivée du DLC Naval.

### Depuis le dialogue d’un compagnon

Le dialogue n’est disponible que pour un compagnon du joueur ou un héros appartenant au clan du joueur.

- **Upgrade your equipment** conserve le parcours d’achat : rôle → tier → application du preset.
- **Configure upgrade presets** ouvre la même vue Gauntlet que **Clan > Equipment**.

Depuis un dialogue, l’ouverture est volontairement reportée au tick de couche suivant. Bannerlord peut ainsi terminer la conséquence `DoOptionContinue` avant que la fenêtre modale prenne le focus ; il n’est pas nécessaire de quitter ou de recharger le dialogue.

## Fenêtre Gauntlet et navigation

`EquipmentConfigView` maintient une `GauntletLayer` globale de priorité `1000`, nommée `CompanionGearUpgradeEquipmentConfig`. Le movie `EquipmentConfigWindow` n’est chargé que lors de l’ouverture de la configuration.

Tant que la fenêtre est ouverte et que son écran hôte est valide, la couche est modale : elle prend le focus et bloque les entrées de l’écran situé dessous. Si l’écran Clan ou la conversation disparaît, la configuration est fermée. Les modifications non sauvegardées sont alors abandonnées avec un message explicite.

Le parcours est fixe :

```text
Role → Tier → Category → Slot → Items
```

| Vue | Contenu | Action principale |
| --- | --- | --- |
| Role | Infantry, Archer, Lancer | Choisir la famille de presets. |
| Tier | Tiers 1 à 3 et leur prix effectif | Charger un snapshot éditable du tier. |
| Category | Weapons, Armors, Horse ; actions de tier | Régler le prix, réinitialiser ou ouvrir une catégorie. |
| Slot | Slots de la catégorie et objet temporairement configuré | Ouvrir le catalogue compatible. |
| Items | Catalogue, filtres, recherche, preview et tooltip | Choisir ou retirer un objet dans le snapshot. |

Les catégories regroupent les slots ainsi :

| Catégorie | Slots |
| --- | --- |
| Weapons | `Weapon0`, `Weapon1`, `Weapon2`, `Weapon3` |
| Armors | `Head`, `Body`, `Cape`, `Gloves`, `Leg` |
| Horse | `Horse`, `HorseHarness` |

Le bouton **Back** remonte d’une vue. Depuis la vue Role, il exécute la même logique que **Exit**.

### Save, Exit et modifications temporaires

Un seul tier est édité à la fois : lors du chargement d’un nouveau preset rôle/tier, deux copies indépendantes sont créées.

- `_working` : snapshot temporaire modifiable ;
- `_savedSnapshot` : dernière version sauvegardée, utilisée pour détecter l’état non enregistré.

Les actions **Select**, **Remove**, **Reset this tier to default**, **Set price (gold)** et **Calculate from equipment** ne modifient que `_working`.

- **Save** est visible dans la vue Category. Il appelle `CommitSnapshot`, met à jour la référence sauvegardée et **laisse la fenêtre ouverte**.
- **Exit** ferme immédiatement s’il n’y a pas de différence. Sinon, une confirmation propose **Exit without saving** ou **Keep editing**.
- Changer de tier alors qu’un snapshot contient des modifications demande également confirmation avant de l’abandonner.
- Dans la vue Items, **Select** et **Remove** conservent la vue ouverte afin de permettre d’enchaîner les changements.

Après un clic sur Save, les overrides sont présents en mémoire de campagne. Ils deviennent durables dans le fichier de sauvegarde lorsque Bannerlord enregistre la campagne.

## Données, snapshots et persistance

### Modèle de données

Un `GearPreset` par défaut associe un coût à un dictionnaire :

```text
EquipmentIndex → ItemObject.StringId
```

Les presets d’origine ne sont pas modifiés pendant l’édition. La vue travaille sur `GearPresetSnapshot`, qui copie le coût et le dictionnaire de slots ; `Clone()` garantit que la référence sauvegardée ne peut pas être altérée par une action temporaire.

Les slots éditables sont définis une seule fois par `GearPresetOverrides.EditableSlots` :

```text
Weapon0, Weapon1, Weapon2, Weapon3,
Head, Body, Cape, Gloves, Leg,
Horse, HorseHarness
```

### Overrides sauvegardés

`CompanionGearUpgradeBehavior.SyncData()` stocke uniquement des structures compatibles avec les sauvegardes Bannerlord :

```csharp
Dictionary<string, int>    // CGU_CostOverrides
Dictionary<string, string> // CGU_ItemOverrides
```

Les clés sont stables :

```text
{Role}:{Tier}:cost
{Role}:{Tier}:{EquipmentIndex numérique}
```

| État sauvegardé d’un slot | Signification |
| --- | --- |
| Aucune entrée d’override | Utiliser l’objet du preset par défaut. |
| `StringId` d’objet | Remplacer l’objet par défaut. |
| `__CGU_EMPTY_SLOT__` | Le joueur a volontairement retiré l’objet : le slot doit rester vide. |

`Remove` écrit `null` dans le snapshot. Lors du commit, ce `null` devient le marqueur `__CGU_EMPTY_SLOT__` : il ne doit jamais être confondu avec une absence d’override, qui signifierait « revenir au défaut ».

Un coût n’est sauvegardé que s’il est différent du coût par défaut ; il est toujours borné à zéro ou plus. Les différences d’objets sont sauvegardées seulement pour les slots présents dans le snapshot, ce qui préserve la distinction entre un slot absent du preset de base et un slot explicitement vidé.

## Catalogue d’objets

Lors de la sélection d’un slot, `CompanionGearUpgradeService.GetCompatibleItems()` parcourt les `ItemObject` actuellement enregistrés par Bannerlord. Les objets ajoutés par des mods peuvent donc apparaître s’ils ont un type compatible.

| Slot | Types acceptés |
| --- | --- |
| `Weapon0` à `Weapon3` | OneHandedWeapon, TwoHandedWeapon, Polearm, Bow, Crossbow, Thrown, Shield, Arrows, Bolts, Banner |
| `Head` | HeadArmor |
| `Body` | BodyArmor |
| `Cape` | Cape |
| `Gloves` | HandArmor |
| `Leg` | LegArmor |
| `Horse` | Horse |
| `HorseHarness` | HorseHarness |

La vue Items propose :

- **All**, puis les filtres correspondant aux types réellement présents ;
- une recherche insensible à la casse sur le nom et le `StringId` ;
- un tri par prix croissant ou décroissant, puis par nom et identifiant pour un ordre stable ;
- une icône native via `ItemImageIdentifierVM` ;
- une ligne entière cliquable, avec prix, type et identifiant.

Les filtres, les boutons de tri et les lignes d’objets sont de vrais `ButtonWidget` radio. `IsSelected`, `DominantSelectedState` et `UpdateChildrenStates` pilotent les états visuels natifs normal, survolé, pressé et sélectionné ; l’état du filtre ou de l’objet actif reste visible jusqu’à une nouvelle sélection.

## Tooltip et comparaison

La priorité d’inspection est toujours :

```text
objet survolé → objet sélectionné → objet configuré → aucun tooltip
```

`HasInspectionItem` dépend seulement de l’objet effectivement inspecté. Il ne dépend ni d’une comparaison, ni de la présence d’un objet déjà configuré dans le slot.

Conséquences :

- un slot vide affiche immédiatement la fiche complète d’un objet survolé ou sélectionné ;
- après **Remove**, le prochain survol ou clic affiche toujours ses statistiques ;
- survoler le même objet que celui sélectionné ou configuré affiche une seule fiche ;
- avec un objet configuré, survoler un objet différent de cet objet et du candidat sélectionné peut afficher une comparaison en deux colonnes ;
- à la fin du survol, la fiche revient automatiquement à l’objet sélectionné, puis à l’objet configuré.

La comparaison est donc une information supplémentaire, jamais une condition d’affichage des statistiques. `GearItemTooltipViewModel` construit les lignes avec `ItemMenuTooltipPropertyVM`, sans utiliser `InventoryScreenHelper` ni `SPInventoryVM`. Il affiche les informations disponibles : type, tier, valeur, poids, puis statistiques d’armure, de monture ou d’arme selon l’objet.

## Prévisualisation 3D

La fenêtre possède une seule `ItemPreviewVM` par movie. La preview suit la même priorité que le tooltip : objet survolé, puis sélectionné, puis configuré.

Le widget `ItemTableauWidget` porte l’identifiant `CGUPreviewTableau`. `EquipmentConfigView.OnGauntletTick()` ne laisse `ItemPreviewVM.Open()` s’exécuter que si ce widget :

1. est connecté à la racine Gauntlet ;
2. est réellement visible ;
3. possède son `TextureProvider` natif.

Après l’ouverture native, le ViewModel attend encore la stabilisation de la texture et une texture valide avant de déclarer la preview prête. En cas d’exception ou d’expiration du délai de texture, il effectue au maximum trois tentatives au total, essai initial compris. Cette attente de contexte est essentielle : appeler `Open()` trop tôt est la cause classique d’une preview vide lors de la première ouverture.

À la sortie de la vue Items ou à la fermeture de la fenêtre, la preview native est fermée et le tableau est vidé. `ItemPreviewVM.OnFinalize()` n’est appelé que lorsque `EquipmentConfigView` libère le movie ; il finalise alors proprement le ViewModel de preview.

## Application d’un upgrade depuis le dialogue

`CompanionGearUpgradeService.TryApplyTier(role, tier)` applique le tier choisi au héros de la conversation :

1. vérifie qu’il s’agit d’un compagnon du joueur ou d’un héros du clan du joueur ;
2. récupère le preset et son coût effectif ;
3. vérifie l’or de `Hero.MainHero` ;
4. fusionne le preset par défaut avec les overrides ;
5. résout **tous** les `StringId` avant de modifier quoi que ce soit ;
6. clone l’équipement de combat du héros ;
7. replace les anciens objets changés dans l’inventaire de `MobileParty.MainParty`, sauf objets de quête ou invalides ;
8. écrit tous les slots éditables, y compris les slots absents ou `null` qui sont explicitement vidés ;
9. retire l’or et assigne le nouvel équipement.

Si un `StringId` est introuvable, l’opération est annulée avant toute modification de l’équipement ou de l’or. Cela permet de détecter proprement un preset devenu incompatible avec le jeu, un DLC ou un mod manquant.

## Ajouter ou modifier du contenu

### Modifier un preset existant

Éditez [GearPresetRepository.cs](Data/GearPresetRepository.cs) : coût et dictionnaire `EquipmentIndex → StringId` du rôle/tier concerné.

Utilisez des `StringId` réellement présents dans les `ItemObject` chargés. Un identifiant invalide bloque volontairement l’application de l’upgrade avec un message explicite.

### Ajouter un rôle ou un tier

Ajouter un rôle ou un tier demande de mettre à jour les trois niveaux suivants :

1. [Domain/GearRole.cs](Domain/GearRole.cs) et le dépôt de presets ;
2. les options construites dans `GearPresetConfigViewModel` (`Infantry`, `Archer`, `Lancer` et la boucle des tiers `1..3`) ;
3. les lignes du dialogue d’achat dans [CompanionGearUpgradeDialog.cs](Dialog/CompanionGearUpgradeDialog.cs).

Le dialogue et l’UI ne découvrent pas automatiquement un quatrième tier ; cette limite est explicite dans le code actuel.

### Ajouter un slot ou une catégorie

Conservez la cohérence entre :

1. `GearPresetOverrides.EditableSlots` ;
2. `GetSlotsForCategory()` dans `GearPresetConfigViewModel.Formatting.cs` ;
3. `GetAllowedItemTypesForSlot()` dans le service ;
4. les presets par défaut ;
5. le nom lisible du slot dans `GetSlotName()`.

Ne remplacez pas le mécanisme `null`/marqueur vide par une suppression silencieuse de clé : ce serait une régression pour les slots volontairement vidés.

### Modifier l’interface

- Le prefab principal est [GUI/Prefabs/EquipmentConfigWindow.xml](GUI/Prefabs/EquipmentConfigWindow.xml).
- L’extension Clan est [GUI/PrefabExtensions/ClanEquipmentTab.xml](GUI/PrefabExtensions/ClanEquipmentTab.xml).
- Ne modifiez pas les XML des modules Native ou SandBox.
- Toute nouvelle propriété liée dans le XML doit être exposée avec `DataSourceProperty` dans le ViewModel approprié.
- Toute nouvelle classe C# doit être ajoutée explicitement à `<Compile Include="…">` dans le `.csproj`, car ce projet legacy ne compile pas automatiquement les nouveaux fichiers.

## Build et déploiement

### Vérifier le XML avant le build

```powershell
@(
  'CompanionGearUpgrade.csproj',
  'SubModule.xml',
  'GUI\Prefabs\EquipmentConfigWindow.xml',
  'GUI\PrefabExtensions\ClanEquipmentTab.xml'
) | ForEach-Object {
  [xml](Get-Content -LiteralPath $_ -Raw) | Out-Null
  Write-Host "XML valide : $_"
}
```

Depuis la racine du projet :

```powershell
dotnet build CompanionGearUpgrade.csproj -c Release --no-restore
```

Le build Release produit :

```text
bin\Release\CompanionGearUpgrade.dll
```

Pour cette installation, le module cible est :

```text
E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CompanionGearUpgrade
```

Après un build réussi, déployez uniquement les quatre fichiers suivants :

```powershell
Copy-Item -LiteralPath 'bin\Release\CompanionGearUpgrade.dll' -Destination 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CompanionGearUpgrade\bin\Win64_Shipping_Client\CompanionGearUpgrade.dll' -Force
Copy-Item -LiteralPath 'SubModule.xml' -Destination 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CompanionGearUpgrade\SubModule.xml' -Force
Copy-Item -LiteralPath 'GUI\Prefabs\EquipmentConfigWindow.xml' -Destination 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CompanionGearUpgrade\GUI\Prefabs\EquipmentConfigWindow.xml' -Force
Copy-Item -LiteralPath 'GUI\PrefabExtensions\ClanEquipmentTab.xml' -Destination 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CompanionGearUpgrade\GUI\PrefabExtensions\ClanEquipmentTab.xml' -Force
```

Ne déployez pas le PDB ni aucune dépendance Workshop. Le `.csproj` ne possède pas de post-build de copie : le déploiement est volontairement explicite et limité aux quatre artefacts ci-dessus.

Après la copie, vérifiez les fichiers déployés avec `Get-FileHash -Algorithm SHA256` sur la source et la cible. Les hashes de chaque paire doivent être identiques.

## Vérification manuelle recommandée

Après un build et un déploiement :

1. Ouvrir **Clan > Equipment** : la fenêtre doit s’ouvrir sans lancer automatiquement une configuration au chargement de partie.
2. Ouvrir un dialogue avec un compagnon, choisir **Configure upgrade presets** : la même fenêtre doit s’ouvrir.
3. Modifier un objet, puis **Save** : la fenêtre reste ouverte et le prix/objet reste affiché.
4. Modifier un objet puis **Exit** : vérifier la confirmation de perte des changements.
5. Retirer un objet, sauvegarder, acheter le tier : le slot du héros doit être vide.
6. Tester un slot vide : survoler et sélectionner un objet doit afficher immédiatement ses statistiques.
7. Tester une comparaison : avec un objet configuré et sans sélectionner le candidat survolé, survoler un objet différent doit afficher deux colonnes, puis revenir à la fiche simple en quittant le survol.
8. Ouvrir la première liste d’objets : la preview 3D doit se charger sans navigation ou rechargement supplémentaire.

## Dépannage

| Symptôme | Vérifications utiles |
| --- | --- |
| `[CGU] Preset configuration is not ready yet.` | La session de campagne doit avoir atteint `OnSessionLaunched`. Vérifier que le module, Harmony et UIExtenderEx sont chargés. |
| La fenêtre ne s’ouvre pas depuis Clan | Ouvrir l’écran Clan réel ; la catégorie réservée `4` n’est interceptée que depuis ce contexte. |
| La fenêtre ne s’ouvre pas depuis le dialogue | Vérifier qu’une conversation est active avec un compagnon ou un héros du clan. L’ouverture est différée d’un tick par conception. |
| Aucun objet dans le catalogue | Vérifier le slot, le filtre, la recherche et les types autorisés pour ce slot. |
| `[CGU] Item not found: ...` | Corriger le `StringId` dans le preset ou installer le contenu qui le fournit. Aucun équipement partiel ne doit être appliqué dans ce cas. |
| Preview 3D temporairement indisponible | Vérifier que `CGUPreviewTableau` est intact dans le prefab. La preview attend le contexte Gauntlet et réessaie automatiquement ; un nouveau survol relance une tentative après échec. |
| Les changements disparaissent après rechargement | Cliquer sur Save dans le configurateur, puis enregistrer la campagne Bannerlord. |

