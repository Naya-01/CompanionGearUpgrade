using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Domain;
using CompanionGearUpgrades.Services;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CompanionGearUpgrades.UI
{
    /// <summary>
    /// Read-only preset picker for applying a role/tier to one explicit hero.
    /// It deliberately delegates every payment and equipment mutation to
    /// <see cref="CompanionGearUpgradeService"/>.
    /// </summary>
    public sealed class ApplyGearPresetViewModel : ViewModel
    {
        private readonly CompanionGearUpgradeService _service;
        private readonly Action _requestClose;
        private readonly MBBindingList<ApplyGearPresetRoleOptionViewModel> _roles;
        private readonly MBBindingList<GearTierOptionViewModel> _tiers;
        private readonly MBBindingList<ApplyGearPresetEquipmentOptionViewModel> _equipment;
        private readonly ItemPreviewSession _previewSession;

        private Hero _target;
        private string _selectedRoleId;
        private int _selectedTier;
        private GearPresetSnapshot _selectedSnapshot;
        private int _selectedCost;
        private bool _isWindowOpen;
        private string _statusText;

        private string _previewItemName;
        private string _previewItemStringId;

        public ApplyGearPresetViewModel(
            CompanionGearUpgradeService service,
            Action requestClose)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _requestClose = requestClose;
            _roles = new MBBindingList<ApplyGearPresetRoleOptionViewModel>();
            _tiers = new MBBindingList<GearTierOptionViewModel>();
            _equipment = new MBBindingList<ApplyGearPresetEquipmentOptionViewModel>();
            _previewSession = new ItemPreviewSession(
                GearItemCatalog.FindById,
                CanRetryPreviewAfterClose,
                NotifyPreviewStateChanged,
                NotifyPreviewChanged,
                "Choose an equipment slot to preview it in 3D.",
                "3D preview is temporarily unavailable.");
            _statusText = "Choose a role and tier to preview this companion's upgrade.";
            _previewItemName = "No item selected";
            _previewItemStringId = string.Empty;
        }

        [DataSourceProperty]
        public MBBindingList<ApplyGearPresetRoleOptionViewModel> RoleOptions => _roles;

        [DataSourceProperty]
        public MBBindingList<GearTierOptionViewModel> TierOptions => _tiers;

        [DataSourceProperty]
        public MBBindingList<ApplyGearPresetEquipmentOptionViewModel> EquipmentOptions => _equipment;

        [DataSourceProperty]
        public ItemCollectionElementViewModel PreviewTableau => _previewSession.PreviewTableau;

        [DataSourceProperty]
        public bool IsWindowOpen
        {
            get { return _isWindowOpen; }
            private set
            {
                if (_isWindowOpen == value)
                    return;

                _isWindowOpen = value;
                OnPropertyChanged(nameof(IsWindowOpen));
                OnPropertyChanged(nameof(CanConfirm));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(ConfirmHint));
            }
        }

        [DataSourceProperty]
        public string WindowTitle => "Apply gear preset";

        [DataSourceProperty]
        public string TargetName
        {
            get
            {
                return _target == null || _target.Name == null
                    ? "No companion selected"
                    : _target.Name.ToString();
            }
        }

        [DataSourceProperty]
        public string TargetLabel => "Companion: " + TargetName;

        [DataSourceProperty]
        public string SelectedRoleName
        {
            get
            {
                foreach (ApplyGearPresetRoleOptionViewModel role in _roles)
                {
                    if (string.Equals(role.RoleId, _selectedRoleId, StringComparison.Ordinal))
                        return role.Name;
                }

                return "Choose a role";
            }
        }

        [DataSourceProperty]
        public string SelectedTierName => _selectedTier > 0 ? "Tier " + _selectedTier : "Choose a tier";

        [DataSourceProperty]
        public string SelectedPresetLabel => SelectedRoleName + " - " + SelectedTierName;

        [DataSourceProperty]
        public string SelectedTierCostText => _selectedSnapshot == null ? "-" : _selectedCost + " gold";

        [DataSourceProperty]
        public string PlayerGoldText
        {
            get
            {
                Hero player = Hero.MainHero;
                return "Player gold: " + (player == null ? "-" : player.Gold + " gold");
            }
        }

        [DataSourceProperty]
        public string EquipmentCountText => _equipment.Count + " equipment slots";

        [DataSourceProperty]
        public bool CanConfirm
        {
            get
            {
                return IsWindowOpen &&
                    _target != null &&
                    _service.IsHeroEligibleForPresetApplication(_target) &&
                    !string.IsNullOrEmpty(_selectedRoleId) &&
                    _service.IsRoleAvailableForApplication(_selectedRoleId) &&
                    _selectedTier > 0 &&
                    _selectedSnapshot != null &&
                    !HasUnavailableEquipment() &&
                    _service.CanPlayerAffordPreset(_selectedCost);
            }
        }

        [DataSourceProperty]
        public bool CanCancel => IsWindowOpen;

        [DataSourceProperty]
        public string StatusText
        {
            get { return _statusText; }
            private set
            {
                if (string.Equals(_statusText, value, StringComparison.Ordinal))
                    return;

                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        [DataSourceProperty]
        public string PreviewStateText => _previewSession.StateText;

        [DataSourceProperty]
        public bool HasPreviewItem => _previewSession.HasPreviewItem;

        [DataSourceProperty]
        public string PreviewItemName => _previewItemName;

        [DataSourceProperty]
        public string PreviewItemStringId => _previewItemStringId;

        [DataSourceProperty]
        public HintViewModel ConfirmHint
        {
            get
            {
                string text = CanConfirm
                    ? "Apply the selected preset and pay its gold price."
                    : HasUnavailableEquipment()
                        ? "Replace unavailable items in Gear Presets before applying this preset."
                        : "Choose an affordable role and tier before applying the preset.";
                return new HintViewModel(new TextObject(text), null);
            }
        }

        [DataSourceProperty]
        public HintViewModel CancelHint => new HintViewModel(
            new TextObject("Close without changing this companion's equipment."),
            null);

        /// <summary>
        /// Starts a fresh session for the supplied hero. The hero is explicit
        /// so callers never need to rely on a conversation-global target.
        /// </summary>
        public bool Open(Hero target)
        {
            if (target == null || !_service.IsHeroEligibleForPresetApplication(target))
            {
                StatusText = "This companion cannot receive a gear preset.";
                return false;
            }

            _target = target;
            _selectedRoleId = null;
            _selectedTier = 0;
            _selectedSnapshot = null;
            _selectedCost = 0;
            _tiers.Clear();
            _equipment.Clear();
            PreparePreviewSession();
            ReloadRoleOptions();
            IsWindowOpen = true;

            OnPropertyChanged(nameof(TargetName));
            OnPropertyChanged(nameof(TargetLabel));
            OnPropertyChanged(nameof(PlayerGoldText));
            OnPropertyChanged(nameof(EquipmentCountText));
            NotifySelectionChanged();

            if (_roles.Count == 0)
            {
                StatusText = "No saved gear roles are available.";
                return true;
            }

            SelectRole(_roles[0]);
            return true;
        }

        public void ExecuteConfirm()
        {
            if (!CanConfirm)
            {
                if (_target == null || !_service.IsHeroEligibleForPresetApplication(_target))
                    StatusText = "This companion can no longer receive a gear preset.";
                else if (string.IsNullOrEmpty(_selectedRoleId) ||
                    !_service.IsRoleAvailableForApplication(_selectedRoleId) ||
                    _selectedTier <= 0 || _selectedSnapshot == null)
                    StatusText = "Choose a valid role and tier first.";
                else if (HasUnavailableEquipment())
                    StatusText = "This preset contains an unavailable item. Choose a replacement in Gear Presets first.";
                else
                    StatusText = "You do not have enough gold for this preset.";
                return;
            }

            GearPresetApplicationResult result = _service.TryApplyTier(
                _target,
                _selectedRoleId,
                _selectedTier);

            if (result == null)
            {
                StatusText = "The preset could not be applied.";
                return;
            }

            StatusText = string.IsNullOrEmpty(result.Message)
                ? (result.IsSuccess ? "Gear preset applied." : "The preset could not be applied.")
                : result.Message;

            if (!result.IsSuccess)
            {
                OnPropertyChanged(nameof(PlayerGoldText));
                OnPropertyChanged(nameof(CanConfirm));
                OnPropertyChanged(nameof(ConfirmHint));
                return;
            }

            InformationManager.DisplayMessage(new InformationMessage(StatusText));
            CloseWindow();
        }

        public void ExecuteCancel()
        {
            CloseWindow();
        }

        /// <summary>
        /// Called by the owning global layer only after the Gauntlet preview
        /// host exists. Calling ItemPreviewVM.Open earlier can leave a native
        /// tableau allocated without a valid texture provider.
        /// </summary>
        public void OnGauntletTick(bool isPreviewHostReady, bool isPreviewTextureReady)
        {
            _previewSession.Tick(IsWindowOpen, isPreviewHostReady, isPreviewTextureReady);
        }

        /// <summary>
        /// Used when the host screen disappears. It avoids firing the close
        /// callback while the global layer is already releasing its movie.
        /// </summary>
        public void CloseFromHost()
        {
            IsWindowOpen = false;
            ReleasePreviewSession();
        }

        private void ReloadRoleOptions()
        {
            _roles.Clear();
            IReadOnlyList<GearRoleDefinition> definitions = _service.GetRoleDefinitions();
            if (definitions == null)
                return;

            foreach (GearRoleDefinition definition in definitions)
            {
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.Id) ||
                    string.IsNullOrWhiteSpace(definition.Name) ||
                    !_service.IsRoleAvailableForApplication(definition.Id))
                {
                    continue;
                }

                _roles.Add(new ApplyGearPresetRoleOptionViewModel(
                    definition.Id,
                    definition.Name,
                    SelectRole));
            }
        }

        private void SelectRole(ApplyGearPresetRoleOptionViewModel option)
        {
            if (option == null || !_service.IsRoleAvailableForApplication(option.RoleId))
            {
                StatusText = "The selected role is no longer available.";
                return;
            }

            _selectedRoleId = option.RoleId;
            foreach (ApplyGearPresetRoleOptionViewModel role in _roles)
                role.SetSelected(string.Equals(role.RoleId, _selectedRoleId, StringComparison.Ordinal));

            _tiers.Clear();
            for (int tier = 1; tier <= GearPresetRepository.TierCount; tier++)
                _tiers.Add(new GearTierOptionViewModel(tier, _service.GetEffectiveCost(_selectedRoleId, tier), SelectTier));

            _selectedTier = 0;
            _selectedSnapshot = null;
            _selectedCost = 0;
            _equipment.Clear();
            SetPreviewItem(null, null);
            NotifySelectionChanged();

            if (_tiers.Count > 0)
                SelectTier(_tiers[0]);
        }

        private void SelectTier(GearTierOptionViewModel option)
        {
            if (option == null || string.IsNullOrEmpty(_selectedRoleId))
                return;

            GearPresetSnapshot snapshot;
            int cost;
            string error;
            if (!_service.TryGetPresetForApplication(_selectedRoleId, option.Tier, out snapshot, out cost, out error))
            {
                _selectedTier = 0;
                _selectedSnapshot = null;
                _selectedCost = 0;
                _equipment.Clear();
                SetPreviewItem(null, null);
                StatusText = string.IsNullOrEmpty(error) ? "The selected preset is unavailable." : error;
                NotifySelectionChanged();
                return;
            }

            _selectedTier = option.Tier;
            _selectedSnapshot = snapshot;
            _selectedCost = cost;
            foreach (GearTierOptionViewModel tier in _tiers)
            {
                tier.SetCost(_service.GetEffectiveCost(_selectedRoleId, tier.Tier));
                tier.SetSelected(tier.Tier == _selectedTier);
            }

            RebuildEquipmentOptions();
            StatusText = "Review the equipment preview, then confirm to apply this preset.";
            NotifySelectionChanged();
            SelectFirstPreviewableEquipment();
        }

        private void RebuildEquipmentOptions()
        {
            _equipment.Clear();
            if (_selectedSnapshot == null)
                return;

            foreach (EquipmentIndex slot in GearSlotCatalog.EditableSlots)
            {
                string itemId;
                _selectedSnapshot.Slots.TryGetValue(slot, out itemId);
                ItemObject item = GearItemCatalog.FindById(itemId);
                bool isEmpty = string.IsNullOrEmpty(itemId);
                _equipment.Add(new ApplyGearPresetEquipmentOptionViewModel(
                    slot,
                    GearSlotCatalog.GetDisplayName(slot),
                    isEmpty ? "(empty)" : (item != null ? item.Name.ToString() : "Unavailable item"),
                    itemId,
                    item != null,
                    SelectEquipment));
            }

            OnPropertyChanged(nameof(EquipmentCountText));
        }

        private bool HasUnavailableEquipment()
        {
            foreach (ApplyGearPresetEquipmentOptionViewModel option in _equipment)
            {
                if (option.IsUnavailable)
                    return true;
            }

            return false;
        }

        private void SelectFirstPreviewableEquipment()
        {
            foreach (ApplyGearPresetEquipmentOptionViewModel option in _equipment)
            {
                if (option.IsAvailable)
                {
                    SelectEquipment(option);
                    return;
                }
            }

            SetPreviewItem(null, null);
        }

        private void SelectEquipment(ApplyGearPresetEquipmentOptionViewModel option)
        {
            if (option == null)
                return;

            foreach (ApplyGearPresetEquipmentOptionViewModel equipment in _equipment)
                equipment.SetSelected(ReferenceEquals(equipment, option));

            ItemObject item = GearItemCatalog.FindById(option.ItemId);
            if (item == null)
            {
                SetPreviewItem(null, option);
                return;
            }

            SetPreviewItem(item, option);
        }

        private void SetPreviewItem(ItemObject item, ApplyGearPresetEquipmentOptionViewModel option)
        {
            _previewItemName = option == null
                ? "No item selected"
                : option.SlotName + ": " + option.ItemName;
            _previewItemStringId = option == null || string.IsNullOrEmpty(option.ItemId)
                ? string.Empty
                : option.ItemId;
            OnPropertyChanged(nameof(PreviewItemName));
            OnPropertyChanged(nameof(PreviewItemStringId));

            _previewSession.SetItem(
                item,
                option != null && option.IsUnavailable
                    ? "This item is unavailable for 3D preview."
                    : "This equipment slot is empty.");
        }

        private void PreparePreviewSession()
        {
            _previewSession.Prepare("Choose an equipment slot to preview it in 3D.");
        }

        private void ReleasePreviewSession()
        {
            _previewSession.Release("Preview is closed.");
        }

        private bool CanRetryPreviewAfterClose()
        {
            return IsWindowOpen;
        }

        private void NotifyPreviewStateChanged()
        {
            OnPropertyChanged(nameof(PreviewStateText));
        }

        private void NotifyPreviewChanged()
        {
            OnPropertyChanged(nameof(HasPreviewItem));
        }

        private void NotifySelectionChanged()
        {
            OnPropertyChanged(nameof(SelectedRoleName));
            OnPropertyChanged(nameof(SelectedTierName));
            OnPropertyChanged(nameof(SelectedPresetLabel));
            OnPropertyChanged(nameof(SelectedTierCostText));
            OnPropertyChanged(nameof(PlayerGoldText));
            OnPropertyChanged(nameof(CanConfirm));
            OnPropertyChanged(nameof(ConfirmHint));
        }

        private void CloseWindow()
        {
            if (!IsWindowOpen)
                return;

            IsWindowOpen = false;
            ReleasePreviewSession();
            _requestClose?.Invoke();
        }

        public override void OnFinalize()
        {
            ReleasePreviewSession();
            _previewSession.FinalizeSession();

            base.OnFinalize();
        }
    }
}
