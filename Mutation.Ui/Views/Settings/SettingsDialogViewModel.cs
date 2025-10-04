using CognitiveSupport;
using CoreAudio;
using Microsoft.UI.Xaml.Controls;
using Mutation.Ui.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Mutation.Ui.Views.Settings;

internal sealed class SettingsDialogViewModel : ObservableObject
{
    private static string _lastCategoryKey = SettingsCategoryViewModel.GeneralKey;

    private readonly Settings _settings;
    private readonly ISettingsManager _settingsManager;
    private readonly AudioDeviceManager _audioDeviceManager;

    private readonly SettingRowViewModel _maxLinesRow;
    private readonly SettingRowViewModel _resetWindowRow;
    private readonly SettingRowViewModel _microphoneRow;
    private readonly SettingRowViewModel _muteHotkeyRow;
    private readonly SettingRowViewModel _useCustomBeepsRow;
    private readonly IReadOnlyList<SettingRowViewModel> _customBeepRows;

    private bool _windowResetRequested;
    private SettingsCategoryViewModel? _selectedCategory;
    private SettingRowViewModel? _lastFocusedIssue;

    public SettingsDialogViewModel(Settings settings, ISettingsManager settingsManager, AudioDeviceManager audioDeviceManager)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _audioDeviceManager = audioDeviceManager ?? throw new ArgumentNullException(nameof(audioDeviceManager));

        Categories = new ObservableCollection<SettingsCategoryViewModel>();

        var general = BuildGeneralCategory();
        var audio = BuildAudioCategory();
        Categories.Add(general);
        Categories.Add(audio);
        Categories.Add(new SettingsCategoryViewModel("transcription", "Transcription") { IsEnabled = false });
        Categories.Add(new SettingsCategoryViewModel("integrations", "Integrations") { IsEnabled = false });

        foreach (var category in Categories)
        {
            foreach (var row in category.Rows)
            {
                row.Parent = category;
                row.PropertyChanged += Row_PropertyChanged;
                row.ValueChanged += Row_ValueChanged;
                row.UpdateValidation();
            }
        }

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanSave);
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(this, new SettingsDialogCloseRequestEventArgs(SettingsDialogCloseReason.Cancel, null)));
        ResetDefaultsCommand = new RelayCommand(_ => { }, _ => false);
        GoToNextIssueCommand = new RelayCommand(_ => MoveToNextIssue(), _ => HasValidationIssues);

        SelectedCategory = Categories.FirstOrDefault(c => c.Key == _lastCategoryKey && c.IsEnabled)
            ?? Categories.FirstOrDefault(c => c.IsEnabled);
        UpdateAggregateState();
    }

    public event EventHandler<SettingsDialogCloseRequestEventArgs>? RequestClose;
    public event EventHandler<SettingRowViewModel>? FocusRowRequested;

    public ObservableCollection<SettingsCategoryViewModel> Categories { get; }

    public SettingsCategoryViewModel? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                if (value != null && value.IsEnabled)
                {
                    _lastCategoryKey = value.Key;
                }
                OnPropertyChanged(nameof(SelectedCategoryRows));
            }
        }
    }

    public IEnumerable<SettingRowViewModel> SelectedCategoryRows => SelectedCategory?.Rows ?? Array.Empty<SettingRowViewModel>();

    public AsyncRelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ResetDefaultsCommand { get; }
    public RelayCommand GoToNextIssueCommand { get; }

    public bool CanSave => IsDirty && !HasErrors;

    private bool _hasErrors;
    public bool HasErrors
    {
        get => _hasErrors;
        private set
        {
            if (SetProperty(ref _hasErrors, value))
            {
                OnPropertyChanged(nameof(HasValidationIssues));
                SaveCommand.RaiseCanExecuteChanged();
                GoToNextIssueCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _hasWarnings;
    public bool HasWarnings
    {
        get => _hasWarnings;
        private set
        {
            if (SetProperty(ref _hasWarnings, value))
            {
                OnPropertyChanged(nameof(HasValidationIssues));
                GoToNextIssueCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasValidationIssues => HasErrors || HasWarnings;

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                SaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _isInfoBarOpen;
    public bool IsInfoBarOpen
    {
        get => _isInfoBarOpen;
        private set => SetProperty(ref _isInfoBarOpen, value);
    }

    private string? _infoBarMessage;
    public string? InfoBarMessage
    {
        get => _infoBarMessage;
        private set => SetProperty(ref _infoBarMessage, value);
    }

    private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;
    public InfoBarSeverity InfoBarSeverity
    {
        get => _infoBarSeverity;
        private set => SetProperty(ref _infoBarSeverity, value);
    }

    public Func<SettingRowViewModel, Task>? BrowseForFileAsync { get; set; }

    public void HandleDialogClosed(SettingsDialogCloseReason reason)
    {
        if (reason == SettingsDialogCloseReason.Save)
        {
            ResetDirtyTracking();
        }
        else
        {
            RevertToSnapshot();
        }
        _windowResetRequested = false;
        UpdateAggregateState();
    }

    private SettingsCategoryViewModel BuildGeneralCategory()
    {
        var category = new SettingsCategoryViewModel(SettingsCategoryViewModel.GeneralKey, "General");

        var instructions = new SettingRowViewModel("instructions", "User instructions", SettingEditorType.Info)
        {
            TextValue = string.IsNullOrWhiteSpace(_settings.UserInstructions)
                ? "Settings are now editable within the app. Changes apply immediately after you save."
                : _settings.UserInstructions,
            Description = "Settings previously edited in Mutation.json can be updated here.",
            IsReadOnly = true
        };
        category.Rows.Add(instructions);

        int maxLines = Math.Clamp(_settings.MainWindowUiSettings?.MaxTextBoxLineCount ?? 5, 1, 20);
        _maxLinesRow = new SettingRowViewModel("maxLines", "Max transcript lines", SettingEditorType.Number)
        {
            Description = "Controls the maximum number of lines shown for multi-line text boxes.",
            Min = 1,
            Max = 20,
            Step = 1,
            DoubleValue = maxLines,
            OriginalValue = (double)maxLines,
            Validator = row =>
            {
                double value = row.DoubleValue;
                if (value < 1 || value > 20)
                {
                    return SettingValidationResult.Error("Enter a value between 1 and 20.");
                }
                return SettingValidationResult.Ok();
            }
        };
        category.Rows.Add(_maxLinesRow);

        _resetWindowRow = new SettingRowViewModel("resetWindow", "Reset window position", SettingEditorType.None)
        {
            Description = "Re-center the window on next launch. Current session repositions immediately after saving.",
            PrimaryActionLabel = "Reset",
            PrimaryActionCommand = new RelayCommand(_ =>
            {
                _windowResetRequested = true;
                UpdateAggregateState();
            })
        };
        category.Rows.Add(_resetWindowRow);

        return category;
    }

    private SettingsCategoryViewModel BuildAudioCategory()
    {
        var category = new SettingsCategoryViewModel("audio", "Audio");

        var audioSettings = _settings.AudioSettings ?? new AudioSettings();
        _settings.AudioSettings ??= audioSettings;
        audioSettings.CustomBeepSettings ??= new AudioSettings.CustomBeepSettingsData();

        var micOptions = LoadMicrophoneOptions(out bool missingDevice);
        string? savedDevice = audioSettings.ActiveCaptureDeviceFullName;

        _microphoneRow = new SettingRowViewModel("defaultMic", "Default microphone", SettingEditorType.Combo)
        {
            Description = "Choose the capture device Mutation should monitor by default.",
            Choices = micOptions.Cast<object>().ToList(),
            TextValue = savedDevice,
            OriginalValue = savedDevice,
            PrimaryActionLabel = "Refresh",
            PrimaryActionCommand = new AsyncRelayCommand(async () => await RefreshMicrophonesAsync())
        };
        if (missingDevice && !string.IsNullOrWhiteSpace(savedDevice))
        {
            _microphoneRow.ValidationState = SettingValidationState.Warning;
            _microphoneRow.ValidationMessage = "The previously selected device is unavailable.";
        }
        else
        {
            _microphoneRow.UpdateValidation();
        }
        category.Rows.Add(_microphoneRow);

        string savedHotkey = audioSettings.MicrophoneToggleMuteHotKey ?? string.Empty;
        string initialHotkey = savedHotkey;
        _muteHotkeyRow = new SettingRowViewModel("muteHotkey", "Mute hotkey", SettingEditorType.Hotkey)
        {
            Description = "Press this shortcut to toggle microphone mute without leaving the app.",
            Placeholder = "Example: Alt+Q",
            TextValue = initialHotkey,
            OriginalValue = initialHotkey,
            Validator = row => ValidateHotkey(row.TextValue)
        };
        category.Rows.Add(_muteHotkeyRow);

        bool useCustomBeeps = audioSettings.CustomBeepSettings.UseCustomBeeps;
        _useCustomBeepsRow = new SettingRowViewModel("useCustomBeeps", "Use custom beeps", SettingEditorType.Toggle)
        {
            Description = "Play custom .wav files when recording starts, ends, or toggles mute.",
            BoolValue = useCustomBeeps,
            OriginalValue = useCustomBeeps,
        };
        category.Rows.Add(_useCustomBeepsRow);

        _customBeepRows = new List<SettingRowViewModel>
        {
            CreateBeepRow("beepStart", "Start recording", audioSettings.CustomBeepSettings.BeepStartFile),
            CreateBeepRow("beepSuccess", "Success", audioSettings.CustomBeepSettings.BeepSuccessFile),
            CreateBeepRow("beepFailure", "Failure", audioSettings.CustomBeepSettings.BeepFailureFile),
            CreateBeepRow("beepEnd", "Stop recording", audioSettings.CustomBeepSettings.BeepEndFile),
            CreateBeepRow("beepMute", "Mute", audioSettings.CustomBeepSettings.BeepMuteFile),
            CreateBeepRow("beepUnmute", "Unmute", audioSettings.CustomBeepSettings.BeepUnmuteFile)
        };
        foreach (var row in _customBeepRows)
        {
            row.IsEnabled = useCustomBeeps;
            category.Rows.Add(row);
        }

        _useCustomBeepsRow.ValueChanged += (_, __) => UpdateCustomBeepsEnabled();

        return category;
    }

    private SettingRowViewModel CreateBeepRow(string key, string displayName, string? initialValue)
    {
        var row = new SettingRowViewModel(key, displayName, SettingEditorType.FilePath)
        {
            Description = ".wav files load from disk. Leave blank to fall back to default tones.",
            TextValue = initialValue ?? string.Empty,
            OriginalValue = initialValue ?? string.Empty,
        };
        row.Validator = ValidateBeepRow;
        row.PrimaryActionLabel = "Browse…";
        row.PrimaryActionCommand = new AsyncRelayCommand(async () =>
        {
            if (BrowseForFileAsync != null)
            {
                await BrowseForFileAsync(row);
            }
        });
        return row;
    }

    private async Task RefreshMicrophonesAsync()
    {
        _audioDeviceManager.RefreshCaptureDevices();
        var options = LoadMicrophoneOptions(out bool missingDeviceAfterRefresh);
        _microphoneRow.Choices = options.Cast<object>().ToList();
        string? savedValue = _microphoneRow.TextValue;
        if (!string.IsNullOrWhiteSpace(savedValue) && options.All(o => !string.Equals(o.Id, savedValue, StringComparison.Ordinal)))
        {
            _microphoneRow.ValidationState = SettingValidationState.Warning;
            _microphoneRow.ValidationMessage = "The selected device is not available.";
        }
        else if (!missingDeviceAfterRefresh)
        {
            _microphoneRow.ValidationState = SettingValidationState.Ok;
            _microphoneRow.ValidationMessage = null;
        }
        _microphoneRow.RaiseChoicesChanged();
        await Task.CompletedTask;
    }

    private IReadOnlyList<AudioDeviceOption> LoadMicrophoneOptions(out bool missingDevice)
    {
        missingDevice = false;
        var devices = _audioDeviceManager.CaptureDevices.ToList();
        var options = new List<AudioDeviceOption>();
        foreach (var device in devices)
        {
            string name = GetDeviceFriendlyName(device);
            options.Add(new AudioDeviceOption(name, name, isAvailable: true));
        }

        string? saved = _settings.AudioSettings?.ActiveCaptureDeviceFullName;
        if (!string.IsNullOrWhiteSpace(saved) && options.All(o => !string.Equals(o.Id, saved, StringComparison.Ordinal)))
        {
            missingDevice = true;
            options.Add(new AudioDeviceOption(saved, $"{saved} (not available)", isAvailable: false));
        }

        return options;
    }

    private static string GetDeviceFriendlyName(CoreAudio.MMDevice device)
    {
#pragma warning disable CS0618
        string? name = device.DeviceFriendlyName;
        if (string.IsNullOrWhiteSpace(name))
            name = device.FriendlyName;
#pragma warning restore CS0618
        return name ?? string.Empty;
    }

    private SettingValidationResult ValidateHotkey(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return SettingValidationResult.Error("Enter a hotkey like Alt+Q.");
        }
        try
        {
            var normalized = HotkeyManager.NormalizeHotkey(text);
            if (!string.Equals(normalized, text, StringComparison.Ordinal))
            {
                _muteHotkeyRow.TextValue = normalized;
            }
            return SettingValidationResult.Ok();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[Settings] Hotkey validation failed: {ex.Message}");
            return SettingValidationResult.Error(ex.Message);
        }
    }

    private SettingValidationResult ValidateBeepRow(SettingRowViewModel row)
    {
        if (!_useCustomBeepsRow.BoolValue)
        {
            return SettingValidationResult.Ok();
        }

        string path = row.TextValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return SettingValidationResult.Error("Provide a .wav file path.");
        }
        if (!string.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase))
        {
            return SettingValidationResult.Error("Only .wav files are supported.");
        }

        string resolved = ResolveAudioFilePath(path);
        if (!File.Exists(resolved))
        {
            return SettingValidationResult.Error("File not found at the specified location.");
        }
        return SettingValidationResult.Ok();
    }

    private string ResolveAudioFilePath(string path)
    {
        var custom = _settings.AudioSettings?.CustomBeepSettings ?? new AudioSettings.CustomBeepSettingsData();
        return custom.ResolveAudioFilePath(path);
    }

    private void UpdateCustomBeepsEnabled()
    {
        bool isEnabled = _useCustomBeepsRow.BoolValue;
        foreach (var row in _customBeepRows)
        {
            row.IsEnabled = isEnabled;
            row.UpdateValidation();
        }
        UpdateAggregateState();
    }

    private void Row_ValueChanged(object? sender, EventArgs e)
    {
        UpdateAggregateState();
    }

    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingRowViewModel.ValidationState) ||
            e.PropertyName == nameof(SettingRowViewModel.ValidationMessage) ||
            e.PropertyName == nameof(SettingRowViewModel.IsDirty))
        {
            UpdateAggregateState();
        }
        if (e.PropertyName == nameof(SettingRowViewModel.Choices))
        {
            OnPropertyChanged(nameof(SelectedCategoryRows));
        }
    }

    private void UpdateAggregateState()
    {
        IsDirty = Categories.SelectMany(c => c.Rows).Any(r => r.IsDirty) || _windowResetRequested;
        HasErrors = Categories.SelectMany(c => c.Rows).Any(r => r.ValidationState == SettingValidationState.Error);
        HasWarnings = Categories.SelectMany(c => c.Rows).Any(r => r.ValidationState == SettingValidationState.Warning);

        var firstIssue = Categories
            .SelectMany(c => c.Rows)
            .FirstOrDefault(r => r.ValidationState == SettingValidationState.Error)
            ?? Categories.SelectMany(c => c.Rows).FirstOrDefault(r => r.ValidationState == SettingValidationState.Warning);

        if (firstIssue != null)
        {
            InfoBarSeverity = firstIssue.ValidationState == SettingValidationState.Error ? InfoBarSeverity.Error : InfoBarSeverity.Warning;
            InfoBarMessage = firstIssue.ValidationMessage ?? "Resolve validation issues before saving.";
            IsInfoBarOpen = true;
        }
        else
        {
            IsInfoBarOpen = false;
            InfoBarMessage = null;
        }
    }

    private async Task SaveAsync()
    {
        Trace.WriteLine("[Settings] Save requested from dialog.");
        ValidateAllRows();
        if (HasErrors)
        {
            Trace.WriteLine("[Settings] Save aborted due to validation errors.");
            return;
        }

        bool generalChanged = _maxLinesRow.IsDirty || _windowResetRequested;
        bool audioChanged = _microphoneRow.IsDirty || _muteHotkeyRow.IsDirty || _useCustomBeepsRow.IsDirty || _customBeepRows.Any(r => r.IsDirty);

        if (generalChanged)
        {
            ApplyGeneralSettings();
        }
        if (audioChanged)
        {
            ApplyAudioSettings();
        }

        _settingsManager.SaveSettingsToFile(_settings);

        var result = new SettingsDialogSaveResult
        {
            ApplyMultiLinePreferences = _maxLinesRow.IsDirty,
            RefreshHotkeyVisuals = _muteHotkeyRow.IsDirty,
            ReloadBeeps = _useCustomBeepsRow.IsDirty || _customBeepRows.Any(r => r.IsDirty),
            ResetWindowPosition = _windowResetRequested,
            RefreshMicrophoneSelection = _microphoneRow.IsDirty,
            ActiveMicrophoneName = _settings.AudioSettings?.ActiveCaptureDeviceFullName
        };

        RequestClose?.Invoke(this, new SettingsDialogCloseRequestEventArgs(SettingsDialogCloseReason.Save, result));
    }

    private void ApplyGeneralSettings()
    {
        var ui = _settings.MainWindowUiSettings ?? new MainWindowUiSettings();
        _settings.MainWindowUiSettings = ui;
        int value = (int)Math.Round(_maxLinesRow.DoubleValue);
        value = Math.Clamp(value, 1, 20);
        ui.MaxTextBoxLineCount = value;
        if (_windowResetRequested)
        {
            ui.WindowLocation = System.Drawing.Point.Empty;
            ui.WindowSize = System.Drawing.Size.Empty;
        }
    }

    private void ApplyAudioSettings()
    {
        var audio = _settings.AudioSettings ?? new AudioSettings();
        _settings.AudioSettings = audio;
        audio.CustomBeepSettings ??= new AudioSettings.CustomBeepSettingsData();

        string? selectedMic = _microphoneRow.TextValue;
        audio.ActiveCaptureDeviceFullName = string.IsNullOrWhiteSpace(selectedMic) ? null : selectedMic;

        string? hotkeyText = _muteHotkeyRow.TextValue;
        audio.MicrophoneToggleMuteHotKey = string.IsNullOrWhiteSpace(hotkeyText) ? null : HotkeyManager.NormalizeHotkey(hotkeyText);

        audio.CustomBeepSettings.UseCustomBeeps = _useCustomBeepsRow.BoolValue;
        audio.CustomBeepSettings.BeepStartFile = NormalizeBeepPath(_customBeepRows[0].TextValue);
        audio.CustomBeepSettings.BeepSuccessFile = NormalizeBeepPath(_customBeepRows[1].TextValue);
        audio.CustomBeepSettings.BeepFailureFile = NormalizeBeepPath(_customBeepRows[2].TextValue);
        audio.CustomBeepSettings.BeepEndFile = NormalizeBeepPath(_customBeepRows[3].TextValue);
        audio.CustomBeepSettings.BeepMuteFile = NormalizeBeepPath(_customBeepRows[4].TextValue);
        audio.CustomBeepSettings.BeepUnmuteFile = NormalizeBeepPath(_customBeepRows[5].TextValue);

        if (!string.IsNullOrWhiteSpace(selectedMic))
        {
            var device = _audioDeviceManager.CaptureDevices.FirstOrDefault(d => string.Equals(GetDeviceFriendlyName(d), selectedMic, StringComparison.Ordinal));
            if (device != null)
            {
                _audioDeviceManager.SelectMicrophone(device);
            }
        }
    }

    private string? NormalizeBeepPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string trimmed = path.Trim();
        string baseDir = AppContext.BaseDirectory;
        try
        {
            string fullPath = Path.GetFullPath(trimmed);
            if (fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            {
                string relative = Path.GetRelativePath(baseDir, fullPath);
                return string.IsNullOrEmpty(relative) ? trimmed : relative;
            }
            return fullPath;
        }
        catch
        {
            return trimmed;
        }
    }

    private void ValidateAllRows()
    {
        foreach (var row in Categories.SelectMany(c => c.Rows))
        {
            row.UpdateValidation();
        }
        UpdateAggregateState();
    }

    private void ResetDirtyTracking()
    {
        foreach (var row in Categories.SelectMany(c => c.Rows))
        {
            row.OriginalValue = row.Value;
        }
        _windowResetRequested = false;
        UpdateAggregateState();
    }

    private void RevertToSnapshot()
    {
        foreach (var row in Categories.SelectMany(c => c.Rows))
        {
            row.Value = row.OriginalValue;
        }
        _windowResetRequested = false;
        ValidateAllRows();
    }

    private void MoveToNextIssue()
    {
        var issues = Categories
            .SelectMany(c => c.Rows)
            .Where(r => r.ValidationState == SettingValidationState.Error)
            .ToList();
        if (issues.Count == 0)
        {
            issues = Categories
                .SelectMany(c => c.Rows)
                .Where(r => r.ValidationState == SettingValidationState.Warning)
                .ToList();
        }
        if (issues.Count == 0)
            return;

        SettingRowViewModel next;
        if (_lastFocusedIssue is null)
        {
            next = issues[0];
        }
        else
        {
            int index = issues.IndexOf(_lastFocusedIssue);
            next = issues[(index + 1) % issues.Count];
        }
        _lastFocusedIssue = next;
        if (next.Parent is not null && next.Parent.IsEnabled)
        {
            SelectedCategory = next.Parent;
        }
        FocusRowRequested?.Invoke(this, next);
    }
}

internal enum SettingsDialogCloseReason
{
    Save,
    Cancel
}

internal sealed class SettingsDialogCloseRequestEventArgs : EventArgs
{
    public SettingsDialogCloseRequestEventArgs(SettingsDialogCloseReason reason, SettingsDialogSaveResult? result)
    {
        Reason = reason;
        SaveResult = result;
    }

    public SettingsDialogCloseReason Reason { get; }
    public SettingsDialogSaveResult? SaveResult { get; }
}

internal sealed class SettingsDialogSaveResult
{
    public bool ApplyMultiLinePreferences { get; init; }
    public bool RefreshHotkeyVisuals { get; init; }
    public bool ReloadBeeps { get; init; }
    public bool ResetWindowPosition { get; init; }
    public bool RefreshMicrophoneSelection { get; init; }
    public string? ActiveMicrophoneName { get; init; }
}

internal sealed class SettingsCategoryViewModel : ObservableObject
{
    public const string GeneralKey = "general";

    public SettingsCategoryViewModel(string key, string displayName)
    {
        Key = key;
        DisplayName = displayName;
        Rows = new ObservableCollection<SettingRowViewModel>();
    }

    public string Key { get; }
    public string DisplayName { get; }
    public ObservableCollection<SettingRowViewModel> Rows { get; }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}

internal enum SettingEditorType
{
    None,
    Info,
    Number,
    Toggle,
    Text,
    Hotkey,
    Combo,
    FilePath
}

internal enum SettingValidationState
{
    Ok,
    Warning,
    Error
}

internal sealed class SettingValidationResult
{
    private SettingValidationResult(SettingValidationState state, string? message)
    {
        State = state;
        Message = message;
    }

    public SettingValidationState State { get; }
    public string? Message { get; }

    public static SettingValidationResult Ok() => new(SettingValidationState.Ok, null);
    public static SettingValidationResult Warning(string message) => new(SettingValidationState.Warning, message);
    public static SettingValidationResult Error(string message) => new(SettingValidationState.Error, message);
}

internal class SettingRowViewModel : ObservableObject
{
    private object? _value;
    private SettingValidationState _validationState;
    private string? _validationMessage;
    private bool _isEnabled = true;

    public SettingRowViewModel(string key, string displayName, SettingEditorType editorType)
    {
        Key = key;
        DisplayName = displayName;
        EditorType = editorType;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public string? Description { get; set; }
    public SettingEditorType EditorType { get; }
    public object? Tag { get; set; }
    public SettingsCategoryViewModel? Parent { get; set; }

    public event EventHandler? ValueChanged;

    public object? Value
    {
        get => _value;
        set
        {
            if (!Equals(_value, value))
            {
                _value = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TextValue));
                OnPropertyChanged(nameof(DoubleValue));
                OnPropertyChanged(nameof(BoolValue));
                OnPropertyChanged(nameof(IsDirty));
                UpdateValidation();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public object? OriginalValue { get; set; }

    public string? TextValue
    {
        get => Value?.ToString();
        set => Value = value;
    }

    public double DoubleValue
    {
        get
        {
            if (Value is double d) return d;
            if (Value is int i) return i;
            if (Value is string s && double.TryParse(s, out var parsed)) return parsed;
            return 0d;
        }
        set => Value = value;
    }

    public bool BoolValue
    {
        get => Value is bool b && b;
        set => Value = value;
    }

    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Step { get; set; }
    public string? Placeholder { get; set; }

    private IEnumerable<object>? _choices;
    public IEnumerable<object>? Choices
    {
        get => _choices;
        set => SetProperty(ref _choices, value);
    }

    public ICommand? PrimaryActionCommand { get; set; }
    public string? PrimaryActionLabel { get; set; }
    public ICommand? SecondaryActionCommand { get; set; }
    public string? SecondaryActionLabel { get; set; }

    public bool IsReadOnly { get; set; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public SettingValidationState ValidationState
    {
        get => _validationState;
        set
        {
            if (SetProperty(ref _validationState, value))
            {
                OnPropertyChanged(nameof(HasValidationIssue));
            }
        }
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public bool HasValidationIssue => ValidationState != SettingValidationState.Ok;

    public bool IsDirty => !Equals(Value, OriginalValue);

    public Func<SettingRowViewModel, SettingValidationResult>? Validator { get; set; }

    public void UpdateValidation()
    {
        if (Validator is null)
        {
            ValidationState = SettingValidationState.Ok;
            ValidationMessage = null;
            return;
        }
        var result = Validator(this);
        ValidationState = result.State;
        ValidationMessage = result.Message;
    }

    public void RaiseChoicesChanged()
    {
        OnPropertyChanged(nameof(Choices));
    }
}

internal sealed class AudioDeviceOption
{
    public AudioDeviceOption(string id, string displayName, bool isAvailable)
    {
        Id = id;
        DisplayName = displayName;
        IsAvailable = isAvailable;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public bool IsAvailable { get; }
}

internal abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action execute)
        : this(_ => execute(), null)
    {
    }

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;
        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
