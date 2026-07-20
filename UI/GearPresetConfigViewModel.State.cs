using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Domain;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace CompanionGearUpgrades.UI
{
    public sealed partial class GearPresetConfigViewModel
    {
        private void SetPage(Page page)
        {
            if (_page == Page.Items && page != Page.Items)
                ClearItemInspection();

            _page = page;
            if (page == Page.Items)
                ArmPreviewHostInitialization();

            NotifyPageChanged();
        }

        private void NotifyPageChanged()
        {
            OnPropertyChanged(nameof(IsRoleSelectionVisible));
            OnPropertyChanged(nameof(IsTierSelectionVisible));
            OnPropertyChanged(nameof(IsCategorySelectionVisible));
            OnPropertyChanged(nameof(IsSlotSelectionVisible));
            OnPropertyChanged(nameof(IsItemSelectionVisible));
            OnPropertyChanged(nameof(IsSaveCancelVisible));
            OnPropertyChanged(nameof(IsBackVisible));
            OnPropertyChanged(nameof(Breadcrumb));
            OnPropertyChanged(nameof(CurrentTierCostText));
        }

        /// <summary>
        /// Starts a fresh role-editing session from the persisted catalogue.
        /// The list remains local until ExecuteSave commits it, so Cancel can
        /// safely discard added and deleted custom roles.
        /// </summary>
        private void ReloadStagedRoles()
        {
            _customRoles.Clear();
            IReadOnlyList<GearRoleDefinition> persistedCustomRoles = _service.GetCustomRoles();
            if (persistedCustomRoles != null)
            {
                foreach (GearRoleDefinition role in persistedCustomRoles)
                {
                    if (role != null && !role.IsDefaultRole)
                        _customRoles.Add(role);
                }
            }

            _savedCustomRoles = new List<GearRoleDefinition>(_customRoles);
            RebuildRoleOptions();
        }

        private void CreateCustomRoleDraft(string text)
        {
            if (!CanAddRole)
            {
                StatusText = "The 10 role limit has been reached.";
                return;
            }

            string name = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                StatusText = "A role name is required.";
                return;
            }

            GearRoleDefinition role;
            string error;
            if (!_service.TryCreateCustomRoleDraft(name, GetStagedRoleDefinitions(), out role, out error) || role == null)
            {
                StatusText = string.IsNullOrEmpty(error)
                    ? "That role name is already in use."
                    : error;
                return;
            }

            _customRoles.Add(role);
            RebuildRoleOptions();
            StatusText = $"Custom role '{role.Name}' was added. Save to keep it.";
        }

        private void DeleteCustomRoleDraft(GearRoleOptionViewModel option)
        {
            if (option == null || !option.IsCustomRole)
                return;

            int index = -1;
            for (int i = 0; i < _customRoles.Count; i++)
            {
                if (RoleIdsEqual(_customRoles[i].Id, option.RoleId))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                StatusText = "This role is no longer available.";
                return;
            }

            _customRoles.RemoveAt(index);
            ClearDeletedRoleState(option.RoleId);
            RebuildRoleOptions();
            StatusText = $"Custom role '{option.Name}' will be deleted when you save.";
        }

        private void RebuildRoleOptions()
        {
            _roles.Clear();
            foreach (GearRoleDefinition role in GetStagedRoleDefinitions())
            {
                var option = new GearRoleOptionViewModel(
                    role.Id,
                    role.Name,
                    role.IsDefaultRole,
                    SelectRole,
                    ExecuteDeleteRole);
                option.SetSelected(RoleIdsEqual(role.Id, _role));
                _roles.Add(option);
            }

            OnPropertyChanged(nameof(RoleOptions));
            OnPropertyChanged(nameof(RoleCountText));
            OnPropertyChanged(nameof(CanAddRole));
            OnPropertyChanged(nameof(IsAddRoleDisabled));
            OnPropertyChanged(nameof(AddRoleHint));
            OnPropertyChanged(nameof(Breadcrumb));
        }

        private List<GearRoleDefinition> GetStagedRoleDefinitions()
        {
            var roles = new List<GearRoleDefinition>();
            IReadOnlyList<GearRoleDefinition> allPersistedRoles = _service.GetRoleDefinitions();
            if (allPersistedRoles != null)
            {
                foreach (GearRoleDefinition role in allPersistedRoles)
                {
                    if (role != null && role.IsDefaultRole)
                        roles.Add(role);
                }
            }

            roles.Sort(CompareDefaultRoles);
            roles.AddRange(_customRoles);
            return roles;
        }

        private static int CompareDefaultRoles(GearRoleDefinition left, GearRoleDefinition right)
        {
            int leftOrder = GetDefaultRoleOrder(left != null ? left.Id : null);
            int rightOrder = GetDefaultRoleOrder(right != null ? right.Id : null);
            int order = leftOrder.CompareTo(rightOrder);
            return order != 0
                ? order
                : string.Compare(left != null ? left.Name : null, right != null ? right.Name : null, StringComparison.Ordinal);
        }

        private static int GetDefaultRoleOrder(string roleId)
        {
            if (string.Equals(roleId, "Archer", StringComparison.Ordinal))
                return 0;
            if (string.Equals(roleId, "Infantry", StringComparison.Ordinal))
                return 1;
            if (string.Equals(roleId, "Lancer", StringComparison.Ordinal))
                return 2;
            return 3;
        }

        private bool ContainsStagedRole(string roleId)
        {
            if (string.IsNullOrEmpty(roleId))
                return false;

            foreach (GearRoleDefinition role in GetStagedRoleDefinitions())
            {
                if (RoleIdsEqual(role.Id, roleId))
                    return true;
            }

            return false;
        }

        private bool ContainsCustomRole(string roleId)
        {
            foreach (GearRoleDefinition role in _customRoles)
            {
                if (RoleIdsEqual(role.Id, roleId))
                    return true;
            }

            return false;
        }

        private string GetRoleDisplayName(string roleId)
        {
            foreach (GearRoleDefinition role in GetStagedRoleDefinitions())
            {
                if (RoleIdsEqual(role.Id, roleId))
                    return role.Name;
            }

            return string.IsNullOrEmpty(roleId) ? "Role" : roleId;
        }

        private void ClearDeletedRoleState(string roleId)
        {
            if (!RoleIdsEqual(_role, roleId) && !RoleIdsEqual(_workingRole, roleId))
                return;

            if (RoleIdsEqual(_role, roleId))
            {
                _role = null;
                _tier = 0;
                _tiers.Clear();
                _categories.Clear();
                _slots.Clear();
            }

            if (RoleIdsEqual(_workingRole, roleId))
            {
                _working = null;
                _savedSnapshot = null;
                _workingRole = null;
                _workingTier = 0;
                _selectedCandidateId = null;
                ClearItemInspection();
            }

            NotifyPageChanged();
        }

        private static bool RoleIdsEqual(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static bool RoleDefinitionsEqual(
            IList<GearRoleDefinition> left,
            IList<GearRoleDefinition> right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                GearRoleDefinition leftRole = left[i];
                GearRoleDefinition rightRole = right[i];
                if (leftRole == null || rightRole == null)
                {
                    if (!ReferenceEquals(leftRole, rightRole))
                        return false;
                    continue;
                }

                if (!RoleIdsEqual(leftRole.Id, rightRole.Id) ||
                    !string.Equals(leftRole.Name, rightRole.Name, StringComparison.Ordinal) ||
                    leftRole.IsDefaultRole != rightRole.IsDefaultRole)
                    return false;
            }

            return true;
        }

        private void NotifyCurrentItemChanged()
        {
            OnPropertyChanged(nameof(CurrentItemStringIdLabel));
            OnPropertyChanged(nameof(CurrentItemDisplayName));
            OnPropertyChanged(nameof(CurrentItemNameHint));
        }

        private void NotifyCandidateChanged()
        {
            OnPropertyChanged(nameof(SelectedCandidateStringId));
            OnPropertyChanged(nameof(SelectedCandidateDisplayName));
            OnPropertyChanged(nameof(SelectedCandidateNameHint));
        }

        private void RefreshSlotLabels()
        {
            foreach (GearSlotOptionViewModel slot in _slots)
                slot.SetLabel(GetSlotLabel(slot.Slot));
        }

        private string GetSlotLabel(EquipmentIndex slot)
        {
            string id = GetWorkingSlotId(slot);
            ItemObject item = string.IsNullOrEmpty(id) ? null : FindItem(id);
            string itemName = item != null ? item.Name.ToString() : "(empty)";
            return $"{GetSlotName(slot)}: {itemName}";
        }

        private string GetWorkingSlotId(EquipmentIndex slot)
        {
            if (_working == null)
                return null;

            string id;
            return _working.Slots.TryGetValue(slot, out id) ? id : null;
        }

        private GearItemOptionViewModel FindCandidate(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (GearItemOptionViewModel item in _allItems)
            {
                if (string.Equals(item.ItemId, id, StringComparison.Ordinal))
                    return item;
            }

            return null;
        }

        private static ItemObject FindItem(string id)
        {
            return string.IsNullOrEmpty(id)
                ? null
                : MBObjectManager.Instance.GetObject<ItemObject>(id);
        }

        private static bool SnapshotsEqual(GearPresetSnapshot left, GearPresetSnapshot right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Cost != right.Cost)
                return false;

            foreach (EquipmentIndex slot in GearPresetOverrides.EditableSlots)
            {
                string leftId;
                string rightId;
                bool hasLeft = left.Slots.TryGetValue(slot, out leftId);
                bool hasRight = right.Slots.TryGetValue(slot, out rightId);
                if (hasLeft != hasRight || !string.Equals(leftId, rightId, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private void CloseWithoutSaving()
        {
            _role = null;
            _tier = 0;
            _working = null;
            _savedSnapshot = null;
            _workingRole = null;
            _workingTier = 0;
            _selectedCandidateId = null;
            _tiers.Clear();
            _categories.Clear();
            _slots.Clear();
            ClearItemInspection();
            ReloadStagedRoles();
            IsWindowOpen = false;
            ReleasePreviewSession();
        }

    }
}
