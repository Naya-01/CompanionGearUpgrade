using CompanionGearUpgrades.Data;
using System;
using TaleWorlds.Library;

namespace CompanionGearUpgrades.UI
{
    public sealed class EquipmentConfigViewModel : ViewModel
    {
        private readonly MBBindingList<EquipmentClassViewModel> _classConfigurations;
        private readonly Action<bool> _windowStateChanged;
        private bool _isWindowOpen;
        private bool _isClanScreenVisible;
        private string _statusText;

        public EquipmentConfigViewModel(Action<bool> windowStateChanged = null)
        {
            _windowStateChanged = windowStateChanged;
            _classConfigurations = new MBBindingList<EquipmentClassViewModel>();
            _statusText = "Changes are stored in ModuleData.";
            ReloadFromDisk();
        }

        public void SetClanScreenVisible(bool visible)
        {
            IsClanScreenVisible = visible;
        }

        [DataSourceProperty]
        public MBBindingList<EquipmentClassViewModel> ClassConfigurations
        {
            get { return _classConfigurations; }
        }

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
                _windowStateChanged?.Invoke(value);
            }
        }

        [DataSourceProperty]
        public bool IsClanScreenVisible
        {
            get { return _isClanScreenVisible; }
            private set
            {
                if (_isClanScreenVisible == value)
                    return;

                _isClanScreenVisible = value;
                OnPropertyChanged(nameof(IsClanScreenVisible));

                if (!value)
                    IsWindowOpen = false;
            }
        }

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

        public void ExecuteOpenConfiguration()
        {
            ReloadFromDisk();
            StatusText = "Changes are stored in ModuleData.";
            IsWindowOpen = true;
        }

        public void ExecuteSave()
        {
            try
            {
                var file = new EquipmentConfigFile();
                file.Classes = new System.Collections.Generic.List<EquipmentClassConfig>();

                foreach (EquipmentClassViewModel viewModel in ClassConfigurations)
                    file.Classes.Add(viewModel.ToConfig());

                EquipmentConfigStore.Save(file);
                StatusText = "Saved.";
            }
            catch (Exception exception)
            {
                StatusText = "Save failed: " + exception.Message;
            }
        }

        public void ExecuteClose()
        {
            IsWindowOpen = false;
        }

        private void ReloadFromDisk()
        {
            EquipmentConfigFile file = EquipmentConfigStore.Load();
            if (file == null || file.Classes == null || file.Classes.Count == 0)
                file = EquipmentConfigStore.CreateDefaults();

            _classConfigurations.Clear();
            foreach (EquipmentClassConfig config in file.Classes)
                _classConfigurations.Add(new EquipmentClassViewModel(config));
        }
    }

    public sealed class EquipmentClassViewModel : ViewModel
    {
        private readonly MBBindingList<EquipmentChoiceViewModel> _weaponOptions;
        private readonly MBBindingList<EquipmentChoiceViewModel> _armorOptions;
        private string _className;
        private int _selectedWeaponIndex;
        private bool _useShield;
        private int _selectedArmorIndex;

        public EquipmentClassViewModel(EquipmentClassConfig config)
        {
            config = config ?? new EquipmentClassConfig();
            _className = string.IsNullOrEmpty(config.ClassId) ? "Class" : config.ClassId;

            _weaponOptions = new MBBindingList<EquipmentChoiceViewModel>();
            _weaponOptions.Add(new EquipmentChoiceViewModel("Arming Sword"));
            _weaponOptions.Add(new EquipmentChoiceViewModel("Longbow"));
            _weaponOptions.Add(new EquipmentChoiceViewModel("Lance"));
            _weaponOptions.Add(new EquipmentChoiceViewModel("Two-handed Axe"));

            _armorOptions = new MBBindingList<EquipmentChoiceViewModel>();
            _armorOptions.Add(new EquipmentChoiceViewModel("Mail and Gambeson"));
            _armorOptions.Add(new EquipmentChoiceViewModel("Ranger Mail"));
            _armorOptions.Add(new EquipmentChoiceViewModel("Lamellar Harness"));
            _armorOptions.Add(new EquipmentChoiceViewModel("Padded Leather"));

            _selectedWeaponIndex = FindIndex(_weaponOptions, config.PrimaryWeapon);
            _useShield = config.UseShield;
            _selectedArmorIndex = FindIndex(_armorOptions, config.Armor);
        }

        [DataSourceProperty]
        public string ClassName
        {
            get { return _className; }
        }

        [DataSourceProperty]
        public MBBindingList<EquipmentChoiceViewModel> WeaponOptions
        {
            get { return _weaponOptions; }
        }

        [DataSourceProperty]
        public MBBindingList<EquipmentChoiceViewModel> ArmorOptions
        {
            get { return _armorOptions; }
        }

        [DataSourceProperty]
        public int SelectedWeaponIndex
        {
            get { return _selectedWeaponIndex; }
            set
            {
                int normalized = NormalizeIndex(value, _weaponOptions.Count);
                if (_selectedWeaponIndex == normalized)
                    return;

                _selectedWeaponIndex = normalized;
                OnPropertyChanged(nameof(SelectedWeaponIndex));
                OnPropertyChanged(nameof(SelectedWeapon));
            }
        }

        [DataSourceProperty]
        public string SelectedWeapon
        {
            get { return GetSelectedText(_weaponOptions, _selectedWeaponIndex); }
        }

        [DataSourceProperty]
        public bool UseShield
        {
            get { return _useShield; }
            set
            {
                if (_useShield == value)
                    return;

                _useShield = value;
                OnPropertyChanged(nameof(UseShield));
            }
        }

        [DataSourceProperty]
        public int SelectedArmorIndex
        {
            get { return _selectedArmorIndex; }
            set
            {
                int normalized = NormalizeIndex(value, _armorOptions.Count);
                if (_selectedArmorIndex == normalized)
                    return;

                _selectedArmorIndex = normalized;
                OnPropertyChanged(nameof(SelectedArmorIndex));
                OnPropertyChanged(nameof(SelectedArmor));
            }
        }

        [DataSourceProperty]
        public string SelectedArmor
        {
            get { return GetSelectedText(_armorOptions, _selectedArmorIndex); }
        }

        public EquipmentClassConfig ToConfig()
        {
            return new EquipmentClassConfig
            {
                ClassId = ClassName,
                PrimaryWeapon = SelectedWeapon,
                UseShield = UseShield,
                Armor = SelectedArmor
            };
        }

        private static int FindIndex(MBBindingList<EquipmentChoiceViewModel> options, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < options.Count; i++)
                {
                    if (string.Equals(options[i].StringItem, value, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            return 0;
        }

        private static int NormalizeIndex(int value, int count)
        {
            if (count <= 0)
                return 0;
            return Math.Max(0, Math.Min(value, count - 1));
        }

        private static string GetSelectedText(MBBindingList<EquipmentChoiceViewModel> options, int index)
        {
            if (options.Count == 0)
                return string.Empty;

            return options[NormalizeIndex(index, options.Count)].StringItem;
        }
    }

    public sealed class EquipmentChoiceViewModel : ViewModel
    {
        private readonly string _stringItem;

        public EquipmentChoiceViewModel(string stringItem)
        {
            _stringItem = stringItem;
        }

        [DataSourceProperty]
        public string StringItem
        {
            get { return _stringItem; }
        }
    }
}
