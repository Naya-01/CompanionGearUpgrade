using CompanionGearUpgrades.Data;
using CompanionGearUpgrades.Domain;
using CompanionGearUpgrades.Services;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
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
            OnPropertyChanged(nameof(IsSaveExitVisible));
            OnPropertyChanged(nameof(IsSaveCancelVisible));
            OnPropertyChanged(nameof(IsBackVisible));
            OnPropertyChanged(nameof(Breadcrumb));
            OnPropertyChanged(nameof(CurrentTierCostText));
            NotifyTransferActionState();
        }

        /// <summary>
        /// Starts a fresh role-editing session from the persisted catalogue.
        /// The list remains local until ExecuteSave commits it, so Exit can
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
                    ExecuteDeleteRole,
                    ExecuteExportRoleFromOption);
                option.SetSelected(RoleIdsEqual(role.Id, _role));
                _roles.Add(option);
            }

            OnPropertyChanged(nameof(RoleOptions));
            OnPropertyChanged(nameof(RoleCountText));
            OnPropertyChanged(nameof(CanAddRole));
            OnPropertyChanged(nameof(IsAddRoleDisabled));
            OnPropertyChanged(nameof(AddRoleHint));
            OnPropertyChanged(nameof(Breadcrumb));
            NotifyTransferActionState();
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

        private void CloseConfiguration()
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
            ClearPendingImport();
            ReloadStagedRoles();
            IsWindowOpen = false;
            ReleasePreviewSession();
        }

        /// <summary>
        /// Transfer actions deliberately operate on the persisted campaign
        /// state.  Keeping them disabled while the configurator has a local
        /// snapshot prevents an import from silently overwriting an edit that
        /// still needs Save or Exit.
        /// </summary>
        private void NotifyTransferActionState()
        {
            OnPropertyChanged(nameof(CanExportRole));
            OnPropertyChanged(nameof(CanExportAll));
            OnPropertyChanged(nameof(CanImport));
            OnPropertyChanged(nameof(ExportRoleHint));
            OnPropertyChanged(nameof(ExportAllHint));
            OnPropertyChanged(nameof(ImportHint));
        }

        private void ExportRolesToFile(
            string path,
            IEnumerable<string> roleIds,
            string description)
        {
            if (HasUnsavedChanges)
            {
                StatusText = "Save pending changes before exporting roles.";
                return;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                StatusText = "Enter a destination file path for the JSON export.";
                return;
            }

            List<GearRoleDefinition> rolesToExport;
            string roleError;
            if (!TryGetPersistedExportRoles(roleIds, out rolesToExport, out roleError))
            {
                StatusText = roleError;
                return;
            }

            GearPresetTransferDocument document = _service.CreateTransferDocument(rolesToExport);
            if (document == null || document.Roles == null || document.Roles.Count == 0)
            {
                StatusText = "The selected preset data could not be exported.";
                return;
            }

            string writtenPath;
            string error;
            if (!GearPresetTransferJson.TryWrite(path, document, out writtenPath, out error))
            {
                StatusText = string.IsNullOrEmpty(error)
                    ? "The JSON export could not be written."
                    : error;
                return;
            }

            string finalPath = string.IsNullOrEmpty(writtenPath) ? path.Trim() : writtenPath;
            StatusText = $"Exported {description} to '{finalPath}'.";
            InformationManager.DisplayMessage(new InformationMessage(
                $"[CGU] Export completed: {finalPath}"));
        }

        private bool TryGetPersistedExportRoles(
            IEnumerable<string> roleIds,
            out List<GearRoleDefinition> roles,
            out string error)
        {
            roles = new List<GearRoleDefinition>();
            error = null;

            IReadOnlyList<GearRoleDefinition> persistedRoles = _service.GetRoleDefinitions();
            if (persistedRoles == null || persistedRoles.Count == 0)
            {
                error = "No persisted roles are available to export.";
                return false;
            }

            if (roleIds == null)
            {
                foreach (GearRoleDefinition role in persistedRoles)
                {
                    if (role != null)
                        roles.Add(role);
                }

                return roles.Count > 0;
            }

            var exportedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string roleId in roleIds)
            {
                if (string.IsNullOrEmpty(roleId) || !exportedIds.Add(roleId))
                    continue;

                GearRoleDefinition role = FindRoleById(persistedRoles, roleId);
                if (role == null)
                {
                    error = "The selected role is no longer available to export.";
                    roles.Clear();
                    return false;
                }

                roles.Add(role);
            }

            if (roles.Count == 0)
                error = "Select a role before exporting it.";

            return roles.Count > 0;
        }

        private void BeginImportFromFile(string path)
        {
            if (!CanImport)
            {
                StatusText = HasUnsavedChanges
                    ? "Save or discard pending changes before importing a JSON file."
                    : _isImportInProgress
                        ? "Resolve or cancel the current import before starting another one."
                        : "Import is only available from the role list.";
                return;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                StatusText = "Enter the path of a JSON file to import.";
                return;
            }

            GearPresetTransferDocument document;
            string error;
            if (!TryReadAndValidateTransferDocument(path, out document, out error))
            {
                StatusText = string.IsNullOrEmpty(error)
                    ? "The selected JSON file is not a valid preset export."
                    : error;
                return;
            }

            StartPendingImport(document);
        }

        private bool TryReadAndValidateTransferDocument(
            string path,
            out GearPresetTransferDocument document,
            out string error)
        {
            document = null;
            error = null;

            if (!GearPresetTransferJson.TryRead(path, out document, out error))
                return false;

            return GearPresetTransferJson.Validate(document, out error);
        }

        private void StartPendingImport(GearPresetTransferDocument document)
        {
            ClearPendingImport();
            if (document == null || document.Roles == null || document.Roles.Count == 0)
            {
                StatusText = "The JSON file contains no roles to import.";
                return;
            }

            _pendingImportDocument = document;
            _isImportInProgress = true;

            IReadOnlyList<GearRoleDefinition> persistedRoles = _service.GetRoleDefinitions();
            foreach (GearPresetTransferRole importedRole in document.Roles)
            {
                GearRoleDefinition existingRole = FindRoleByName(persistedRoles, importedRole.Name);
                _pendingImportRoles.Add(new PendingImportRole(importedRole, existingRole));
            }

            // New names do not need a player decision.  Draft their stable
            // custom IDs now, but do not commit them to the campaign until all
            // conflicts have been resolved.
            foreach (PendingImportRole pendingRole in _pendingImportRoles)
            {
                if (pendingRole.ExistingRole != null)
                    continue;

                GearRoleDefinition draftRole;
                string error;
                if (!TryCreateImportRoleDraft(pendingRole.Source.Name, out draftRole, out error))
                {
                    CancelPendingImport(string.IsNullOrEmpty(error)
                        ? "The imported role could not be added."
                        : error);
                    return;
                }

                pendingRole.TargetRole = draftRole;
            }

            _pendingImportConflictIndex = 0;
            NotifyTransferActionState();
            ShowNextImportConflict();
        }

        private void ShowNextImportConflict()
        {
            if (!_isImportInProgress || _pendingImportDocument == null)
                return;

            while (_pendingImportConflictIndex < _pendingImportRoles.Count &&
                _pendingImportRoles[_pendingImportConflictIndex].TargetRole != null)
            {
                _pendingImportConflictIndex++;
            }

            if (_pendingImportConflictIndex >= _pendingImportRoles.Count)
            {
                ApplyPendingImport();
                return;
            }

            PendingImportRole pendingRole = _pendingImportRoles[_pendingImportConflictIndex];
            if (pendingRole.ExistingRole == null)
            {
                CancelPendingImport("The import conflict could not be resolved.");
                return;
            }

            string existingRoleName = pendingRole.ExistingRole.Name;
            InformationManager.ShowInquiry(new InquiryData(
                "CGU - Import conflict",
                $"A role named '{existingRoleName}' already exists. Replace its three tier presets, or continue to import a separate copy or cancel?",
                true,
                true,
                "Replace",
                "Copy or cancel",
                () => RequestImportReplacement(pendingRole),
                () => ShowImportCopyOrCancel(pendingRole)
            ));
        }

        private void RequestImportReplacement(PendingImportRole pendingRole)
        {
            if (!IsCurrentImportConflict(pendingRole))
                return;

            if (!pendingRole.ExistingRole.IsDefaultRole)
            {
                CompleteImportReplacement(pendingRole);
                return;
            }

            InformationManager.ShowInquiry(new InquiryData(
                "CGU - Replace default role",
                $"'{pendingRole.ExistingRole.Name}' is a protected default role. Replace only its imported preset configuration? The role itself will not be removed.",
                true,
                true,
                "Replace configuration",
                "Go back",
                () => CompleteImportReplacement(pendingRole),
                () => ShowNextImportConflict()
            ));
        }

        private void CompleteImportReplacement(PendingImportRole pendingRole)
        {
            if (!IsCurrentImportConflict(pendingRole))
                return;

            pendingRole.TargetRole = pendingRole.ExistingRole;
            _pendingImportConflictIndex++;
            ShowNextImportConflict();
        }

        private void ShowImportCopyOrCancel(PendingImportRole pendingRole)
        {
            if (!IsCurrentImportConflict(pendingRole))
                return;

            InformationManager.ShowInquiry(new InquiryData(
                "CGU - Import conflict",
                $"Import '{pendingRole.Source.Name}' as a separately named custom role, or cancel this import without changing the current save?",
                true,
                true,
                "Import as copy",
                "Cancel import",
                () => ImportConflictAsCopy(pendingRole),
                () => CancelPendingImport("Import cancelled. No changes were made to this save.")
            ));
        }

        private void ImportConflictAsCopy(PendingImportRole pendingRole)
        {
            if (!IsCurrentImportConflict(pendingRole))
                return;

            GearRoleDefinition draftRole;
            string error;
            if (!TryCreateImportCopyDraft(pendingRole.Source.Name, out draftRole, out error))
            {
                StatusText = string.IsNullOrEmpty(error)
                    ? "A copy of this role could not be created."
                    : error;
                ShowNextImportConflict();
                return;
            }

            pendingRole.TargetRole = draftRole;
            _pendingImportConflictIndex++;
            ShowNextImportConflict();
        }

        private bool IsCurrentImportConflict(PendingImportRole pendingRole)
        {
            return _isImportInProgress &&
                pendingRole != null &&
                _pendingImportConflictIndex >= 0 &&
                _pendingImportConflictIndex < _pendingImportRoles.Count &&
                ReferenceEquals(_pendingImportRoles[_pendingImportConflictIndex], pendingRole) &&
                pendingRole.TargetRole == null;
        }

        private bool TryCreateImportRoleDraft(
            string name,
            out GearRoleDefinition draftRole,
            out string error)
        {
            return _service.TryCreateCustomRoleDraft(
                name,
                GetImportDraftRoleDefinitions(),
                out draftRole,
                out error);
        }

        private bool TryCreateImportCopyDraft(
            string sourceName,
            out GearRoleDefinition draftRole,
            out string error)
        {
            draftRole = null;
            error = null;

            string baseName = (sourceName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(baseName))
            {
                error = "The imported role has no valid name.";
                return false;
            }

            for (int copyNumber = 1; copyNumber <= 1000; copyNumber++)
            {
                string suffix = copyNumber == 1 ? " (copy)" : $" (copy {copyNumber})";
                string candidateName = baseName + suffix;
                if (IsImportRoleNameInUse(candidateName))
                    continue;

                return TryCreateImportRoleDraft(candidateName, out draftRole, out error);
            }

            error = "A unique name could not be generated for the imported copy.";
            return false;
        }

        private bool IsImportRoleNameInUse(string name)
        {
            foreach (GearRoleDefinition role in GetImportDraftRoleDefinitions())
            {
                if (role != null && string.Equals(role.Name, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private List<GearRoleDefinition> GetImportDraftRoleDefinitions()
        {
            var roles = new List<GearRoleDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            IReadOnlyList<GearRoleDefinition> persistedRoles = _service.GetRoleDefinitions();
            if (persistedRoles != null)
            {
                foreach (GearRoleDefinition role in persistedRoles)
                {
                    if (role != null && !string.IsNullOrEmpty(role.Id) && ids.Add(role.Id))
                        roles.Add(role);
                }
            }

            foreach (PendingImportRole pendingRole in _pendingImportRoles)
            {
                GearRoleDefinition targetRole = pendingRole.TargetRole;
                if (targetRole != null && !string.IsNullOrEmpty(targetRole.Id) && ids.Add(targetRole.Id))
                    roles.Add(targetRole);
            }

            return roles;
        }

        private void ApplyPendingImport()
        {
            if (!_isImportInProgress || _pendingImportDocument == null)
                return;

            var finalCustomRoles = new List<GearRoleDefinition>();
            var customRoleIds = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<GearRoleDefinition> currentCustomRoles = _service.GetCustomRoles();
            if (currentCustomRoles != null)
            {
                foreach (GearRoleDefinition customRole in currentCustomRoles)
                {
                    if (customRole != null && !customRole.IsDefaultRole &&
                        !string.IsNullOrEmpty(customRole.Id) && customRoleIds.Add(customRole.Id))
                    {
                        finalCustomRoles.Add(customRole);
                    }
                }
            }

            var configurations = new Dictionary<string, GearPresetTransferRole>(StringComparer.Ordinal);
            foreach (PendingImportRole pendingRole in _pendingImportRoles)
            {
                if (pendingRole.TargetRole == null || string.IsNullOrEmpty(pendingRole.TargetRole.Id))
                {
                    CancelPendingImport("The import did not resolve every role destination.");
                    return;
                }

                if (!pendingRole.TargetRole.IsDefaultRole && customRoleIds.Add(pendingRole.TargetRole.Id))
                    finalCustomRoles.Add(pendingRole.TargetRole);

                if (configurations.ContainsKey(pendingRole.TargetRole.Id))
                {
                    CancelPendingImport("The import contains duplicate role destinations.");
                    return;
                }

                configurations.Add(pendingRole.TargetRole.Id, pendingRole.Source);
            }

            GearPresetImportResult result;
            string error;
            if (!_service.TryApplyImportedConfiguration(finalCustomRoles, configurations, out result, out error))
            {
                CancelPendingImport(string.IsNullOrEmpty(error)
                    ? "The imported configuration could not be saved."
                    : error);
                return;
            }

            ClearPendingImport();
            ResetConfigurationAfterImport();
            ShowImportResult(result);
        }

        private void ResetConfigurationAfterImport()
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
            _items.Clear();
            _allItems.Clear();
            _filters.Clear();
            ClearItemInspection();
            ReloadStagedRoles();
            SetPage(Page.Roles);
        }

        private void ShowImportResult(GearPresetImportResult result)
        {
            int importedRoleCount = result == null ? 0 : result.ImportedRoleCount;
            int importedItemCount = result == null ? 0 : result.ImportedItemCount;
            int missingItemCount = result == null || result.MissingItemIds == null
                ? 0
                : result.MissingItemIds.Count;
            int missingSlotCount = result == null ? 0 : result.MissingSlotCount;
            bool hasWarnings = result != null && result.HasWarnings;

            if (!hasWarnings)
            {
                StatusText = $"Import completed: {importedRoleCount} roles and {importedItemCount} items imported.";
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[CGU] {StatusText}"));
                return;
            }

            string summary =
                "Import completed with warnings:\n" +
                $"- {importedItemCount} items imported\n" +
                $"- {missingItemCount} modded items unavailable\n" +
                $"- {missingSlotCount} slots left empty";

            string missingIdSummary = BuildMissingItemIdSummary(result.MissingItemIds);
            if (!string.IsNullOrEmpty(missingIdSummary))
                summary += "\nUnavailable StringIds: " + missingIdSummary;

            StatusText = $"Import completed with warnings: {missingItemCount} unavailable items left {missingSlotCount} slots empty.";
            InformationManager.ShowInquiry(new InquiryData(
                "CGU - Import completed with warnings",
                summary,
                true,
                false,
                "OK",
                string.Empty,
                () => { },
                null
            ));
        }

        private static string BuildMissingItemIdSummary(IReadOnlyList<string> missingItemIds)
        {
            if (missingItemIds == null || missingItemIds.Count == 0)
                return null;

            var visibleIds = new List<string>();
            int count = Math.Min(missingItemIds.Count, 5);
            for (int index = 0; index < count; index++)
            {
                string itemId = missingItemIds[index];
                if (!string.IsNullOrEmpty(itemId))
                    visibleIds.Add(itemId);
            }

            string summary = string.Join(", ", visibleIds);
            return missingItemIds.Count > count
                ? summary + ", ..."
                : summary;
        }

        private void CancelPendingImport(string message)
        {
            ClearPendingImport();
            StatusText = message;
        }

        private void ClearPendingImport()
        {
            _pendingImportRoles.Clear();
            _pendingImportDocument = null;
            _pendingImportConflictIndex = 0;
            _isImportInProgress = false;
            NotifyTransferActionState();
        }

        private static GearRoleDefinition FindRoleById(
            IEnumerable<GearRoleDefinition> roles,
            string roleId)
        {
            if (roles == null || string.IsNullOrEmpty(roleId))
                return null;

            foreach (GearRoleDefinition role in roles)
            {
                if (role != null && string.Equals(role.Id, roleId, StringComparison.Ordinal))
                    return role;
            }

            return null;
        }

        private static GearRoleDefinition FindRoleByName(
            IEnumerable<GearRoleDefinition> roles,
            string name)
        {
            if (roles == null || string.IsNullOrWhiteSpace(name))
                return null;

            string normalizedName = name.Trim();
            foreach (GearRoleDefinition role in roles)
            {
                if (role != null && !string.IsNullOrWhiteSpace(role.Name) &&
                    string.Equals(role.Name.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase))
                {
                    return role;
                }
            }

            return null;
        }

        private sealed class PendingImportRole
        {
            public PendingImportRole(GearPresetTransferRole source, GearRoleDefinition existingRole)
            {
                Source = source;
                ExistingRole = existingRole;
            }

            public GearPresetTransferRole Source { get; private set; }
            public GearRoleDefinition ExistingRole { get; private set; }
            public GearRoleDefinition TargetRole { get; set; }
        }

    }
}
