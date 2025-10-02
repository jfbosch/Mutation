using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CognitiveSupport;
using Mutation.Ui.Services;

namespace Mutation.Ui.ViewModels;

public sealed class SettingsDialogViewModel : INotifyPropertyChanged
{
        private readonly Settings _settings;
        private readonly ISettingsManager _settingsManager;
        private readonly Action<Settings> _onSettingsSaved;
        private CategoryViewModel? _selectedCategory;
        private bool _isSaving;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<CategoryViewModel> Categories { get; }

        public SettingsDialogViewModel(Settings settings, ISettingsManager settingsManager, Action<Settings> onSettingsSaved)
        {
                _settings = settings ?? throw new ArgumentNullException(nameof(settings));
                _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
                _onSettingsSaved = onSettingsSaved ?? throw new ArgumentNullException(nameof(onSettingsSaved));

                Categories = new ObservableCollection<CategoryViewModel>
                {
                        BuildGeneralCategory()
                };

                SelectedCategory = Categories.FirstOrDefault();

                SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
                CancelCommand = new RelayCommand(_ => { }, _ => true);
                ResetDefaultsCommand = new RelayCommand(_ => ResetToDefaults(), _ => true);
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetDefaultsCommand { get; }

        public CategoryViewModel? SelectedCategory
        {
                get => _selectedCategory;
                set
                {
                        if (_selectedCategory != value)
                        {
                                _selectedCategory = value;
                                OnPropertyChanged();
                        }
                }
        }

        public bool IsSaving
        {
                get => _isSaving;
                private set
                {
                        if (_isSaving != value)
                        {
                                _isSaving = value;
                                OnPropertyChanged();
                        }
                }
        }

        public bool HasValidationErrors => Categories.SelectMany(c => c.Rows).Any(r => r.ValidationState == ValidationState.Error);

        public bool IsDirty => Categories.SelectMany(c => c.Rows).Any(r => r.IsDirty);

        private bool CanExecuteSave(object? parameter) => !IsSaving && !HasValidationErrors;

        private void ExecuteSave(object? parameter)
        {
                if (IsSaving)
                        return;

                try
                {
                        IsSaving = true;

                        foreach (var row in Categories.SelectMany(c => c.Rows))
                        {
                                row.Commit();
                        }

                        _settingsManager.SaveSettingsToFile(_settings);
                        _onSettingsSaved(_settings);
                }
                finally
                {
                        IsSaving = false;
                }
        }

        private void ResetToDefaults()
        {
                foreach (var row in Categories.SelectMany(c => c.Rows))
                {
                        row.Reset();
                }
                OnPropertyChanged(nameof(IsDirty));
        }

        private CategoryViewModel BuildGeneralCategory()
        {
                var category = new CategoryViewModel("General");

                category.Rows.Add(AttachRow(new SettingRowViewModel(
                        key: nameof(Settings.UserInstructions),
                        displayName: "User instructions",
                        description: "Settings can now be edited within the application.",
                        editorType: SettingEditorType.Info,
                        originalValue: _settings.UserInstructions ?? string.Empty,
                        apply: value => _settings.UserInstructions = value as string)
                {
                        IsReadOnly = true,
                        Value = _settings.UserInstructions ?? string.Empty
                }));

                int maxLines = Math.Clamp(_settings.MainWindowUiSettings?.MaxTextBoxLineCount ?? 5, 1, 20);
                var maxLinesRow = new SettingRowViewModel(
                        key: nameof(MainWindowUiSettings.MaxTextBoxLineCount),
                        displayName: "Maximum transcript lines",
                        description: "Choose how many lines are visible in transcript editors.",
                        editorType: SettingEditorType.Number,
                        originalValue: maxLines,
                        apply: value =>
                        {
                                if (_settings.MainWindowUiSettings == null)
                                        _settings.MainWindowUiSettings = new MainWindowUiSettings();
                                _settings.MainWindowUiSettings.MaxTextBoxLineCount = Convert.ToInt32(value);
                        });
                maxLinesRow.Value = maxLines;
                maxLinesRow.SetValidationCallback(val =>
                {
                        if (val is double dbl)
                        {
                                if (dbl < 1 || dbl > 20)
                                        return (ValidationState.Error, "Enter a value between 1 and 20.");

                                return (ValidationState.Ok, string.Empty);
                        }

                        if (val is int lines)
                        {
                                if (lines < 1 || lines > 20)
                                        return (ValidationState.Error, "Enter a value between 1 and 20.");

                                return (ValidationState.Ok, string.Empty);
                        }

                        return (ValidationState.Error, "Value must be numeric.");
                });
                category.Rows.Add(AttachRow(maxLinesRow));

                category.Rows.Add(AttachRow(new SettingRowViewModel(
                        key: "ResetWindowLayout",
                        displayName: "Reset window layout",
                        description: "Re-center the window on next launch.",
                        editorType: SettingEditorType.Action,
                        originalValue: false,
                        apply: value =>
                        {
                                if (_settings.MainWindowUiSettings != null)
                                {
                                        _settings.MainWindowUiSettings.WindowLocation = new System.Drawing.Point();
                                        _settings.MainWindowUiSettings.WindowSize = new System.Drawing.Size();
                                }
                        })
                {
                        Value = false,
                        ActionLabel = "Restore"
                }));

                return category;
        }

        private SettingRowViewModel AttachRow(SettingRowViewModel row)
        {
                row.PropertyChanged += Row_PropertyChanged;
                return row;
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
                if (e.PropertyName == nameof(SettingRowViewModel.ValidationState) || e.PropertyName == nameof(SettingRowViewModel.IsDirty))
                {
                        OnPropertyChanged(nameof(HasValidationErrors));
                        OnPropertyChanged(nameof(IsDirty));
                        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                if (propertyName == nameof(HasValidationErrors) || propertyName == nameof(IsDirty) || propertyName == nameof(IsSaving))
                {
                        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
        }
}

public sealed class CategoryViewModel
{
        public string Name { get; }
        public ObservableCollection<SettingRowViewModel> Rows { get; } = new();

        public CategoryViewModel(string name)
        {
                Name = name;
        }
}

public enum SettingEditorType
{
        Text,
        Number,
        Toggle,
        Info,
        Action
}

public enum ValidationState
{
        Ok,
        Warning,
        Error
}

public sealed class SettingRowViewModel : INotifyPropertyChanged
{
        private object? _value;
        private ValidationState _validationState;
        private string _validationMessage = string.Empty;
        private Func<object?, (ValidationState State, string Message)>? _validationCallback;

        public event PropertyChangedEventHandler? PropertyChanged;

        public SettingRowViewModel(string key, string displayName, string description, SettingEditorType editorType, object? originalValue, Action<object?> apply)
        {
                Key = key;
                DisplayName = displayName;
                Description = description;
                EditorType = editorType;
                OriginalValue = originalValue;
                ApplyValue = apply;
                _validationState = ValidationState.Ok;
        }

        public string Key { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public SettingEditorType EditorType { get; }
        public object? OriginalValue { get; }
        public bool IsReadOnly { get; set; }
        public string? ActionLabel { get; set; }
        public Action<object?> ApplyValue { get; }

        public ValidationState ValidationState
        {
                get => _validationState;
                private set
                {
                        if (_validationState != value)
                        {
                                _validationState = value;
                                OnPropertyChanged(nameof(ValidationState));
                                OnPropertyChanged(nameof(IsValid));
                        }
                }
        }

        public string ValidationMessage
        {
                get => _validationMessage;
                private set
                {
                        if (_validationMessage != value)
                        {
                                _validationMessage = value;
                                OnPropertyChanged(nameof(ValidationMessage));
                        }
                }
        }

        public bool IsValid => ValidationState != ValidationState.Error;

        public bool IsDirty => !Equals(Value, OriginalValue);

        public object? Value
        {
                get => _value;
                set
                {
                        if (!Equals(_value, value))
                        {
                                _value = value;
                                Validate();
                                OnPropertyChanged(nameof(Value));
                                OnPropertyChanged(nameof(IsDirty));
                        }
                }
        }

        public void SetValidationCallback(Func<object?, (ValidationState State, string Message)> callback)
        {
                _validationCallback = callback;
                Validate();
        }

        public void Commit()
        {
                if (!IsValid)
                        throw new InvalidOperationException($"Setting '{DisplayName}' is invalid.");

                ApplyValue(Value);
        }

        public void Reset()
        {
                Value = OriginalValue;
                Validate();
        }

        private void Validate()
        {
                if (_validationCallback is null)
                {
                        ValidationState = ValidationState.Ok;
                        ValidationMessage = string.Empty;
                        return;
                }

                var result = _validationCallback(Value);
                ValidationState = result.State;
                ValidationMessage = result.Message;
        }

        private void OnPropertyChanged(string propertyName)
        {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
}

internal sealed class RelayCommand : ICommand
{
        private readonly Action<object?> _execute;
        private readonly Predicate<object?> _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?> canExecute)
        {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute ?? throw new ArgumentNullException(nameof(canExecute));
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute(parameter);

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
