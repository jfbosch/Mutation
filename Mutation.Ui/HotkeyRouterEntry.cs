using CognitiveSupport;
using Mutation.Ui.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Mutation.Ui;

public enum HotkeyBindingState
{
        Inactive,
        Bound,
        Failed
}

public sealed class HotkeyRouterEntry : INotifyPropertyChanged
{
        private static readonly Brush TransparentBrush = new SolidColorBrush(Colors.Transparent);
        private static readonly Brush InvalidBackgroundBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xB2, 0x1E, 0x1E));
        private static readonly Brush NeutralStatusBrush = ResolveThemeBrush("SystemFillColorNeutralBrush") ?? new SolidColorBrush(Color.FromArgb(0xFF, 0x6B, 0x6B, 0x6B));
        private static readonly Brush SuccessStatusBrush = ResolveThemeBrush("SystemFillColorSuccessBrush") ?? new SolidColorBrush(Color.FromArgb(0xFF, 0x0B, 0x8A, 0x00));
        private static readonly Brush FailureStatusBrush = ResolveThemeBrush("SystemFillColorCriticalBrush") ?? new SolidColorBrush(Color.FromArgb(0xFF, 0xD1, 0x42, 0x42));

        private readonly char[] _separators = new[] { '+', '-', ',', '/', '\\', '|' , ';', ':' , ' ' };

        private string _fromHotkeyText = string.Empty;
        private string _toHotkeyText = string.Empty;
        private string? _formattedFrom;
        private string? _formattedTo;
        private bool _isFromValid;
        private bool _isToValid;
        private bool _isDuplicate;
        private string? _fromValidationMessage;
        private string? _toValidationMessage;
        private HotkeyBindingState _bindingState = HotkeyBindingState.Inactive;
        private string? _bindingError;
        private string? _combinedError;

        internal HotKeyRouterSettings.HotKeyRouterMap Map { get; }

        public HotkeyRouterEntry(HotKeyRouterSettings.HotKeyRouterMap map)
        {
                Map = map ?? throw new ArgumentNullException(nameof(map));

                _fromHotkeyText = map.FromHotKey ?? string.Empty;
                _toHotkeyText = map.ToHotKey ?? string.Empty;

                EvaluateFrom(commit: true);
                EvaluateTo(commit: true);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string FromHotkey
        {
                get => _fromHotkeyText;
                set
                {
                        if (SetField(ref _fromHotkeyText, value ?? string.Empty, nameof(FromHotkey)))
                        {
                                EvaluateFrom(commit: false);
                                SetBindingResult(HotkeyBindingState.Inactive, null);
                        }
                }
        }

        public string ToHotkey
        {
                get => _toHotkeyText;
                set
                {
                        if (SetField(ref _toHotkeyText, value ?? string.Empty, nameof(ToHotkey)))
                        {
                                EvaluateTo(commit: false);
                                SetBindingResult(HotkeyBindingState.Inactive, null);
                        }
                }
        }

        public bool IsFromValid => _isFromValid;

        public bool IsToValid => _isToValid;

        public bool IsDuplicate => _isDuplicate;

        public bool IsFromInputValid => _isFromValid && !_isDuplicate;

        public bool IsValid => IsFromInputValid && _isToValid;

        public string? NormalizedFromHotkey => string.IsNullOrWhiteSpace(_formattedFrom) ? null : _formattedFrom;

        public string? NormalizedToHotkey => string.IsNullOrWhiteSpace(_formattedTo) ? null : _formattedTo;

        public Brush FromBackgroundBrush => IsFromInputValid ? TransparentBrush : InvalidBackgroundBrush;

        public Brush ToBackgroundBrush => _isToValid ? TransparentBrush : InvalidBackgroundBrush;

        public HotkeyBindingState BindingState => _bindingState;

        public string BindingStatusGlyph => _bindingState switch
        {
                HotkeyBindingState.Bound => "\uE73E", // CheckMark
                HotkeyBindingState.Failed => "\uEA39", // StatusErrorFull
                _ => "\uF142" // Record
        };

        public Brush BindingStatusBrush => _bindingState switch
        {
                HotkeyBindingState.Bound => SuccessStatusBrush,
                HotkeyBindingState.Failed => FailureStatusBrush,
                _ => NeutralStatusBrush
        };

        public string BindingStatusTooltip
        {
                get
                {
                        if (_bindingState == HotkeyBindingState.Bound)
                                return "Hotkey is bound.";
                        if (HasBindingError && !string.IsNullOrEmpty(_combinedError))
                                return _combinedError!;
                        if (_bindingState == HotkeyBindingState.Failed)
                                return "Hotkey binding failed.";
                        return "Hotkey is not currently bound.";
                }
        }

        public bool HasBindingError => !string.IsNullOrEmpty(_combinedError);

        public Visibility BindingErrorVisibility => HasBindingError ? Visibility.Visible : Visibility.Collapsed;

        public string? BindingErrorMessage => _combinedError;

        public void CommitFromHotkey()
        {
                EvaluateFrom(commit: true);
        }

        public void CommitToHotkey()
        {
                EvaluateTo(commit: true);
        }

        public void SetDuplicate(bool isDuplicate)
        {
                if (_isDuplicate == isDuplicate)
                        return;

                _isDuplicate = isDuplicate;
                OnPropertyChanged(nameof(IsDuplicate));
                OnPropertyChanged(nameof(IsFromInputValid));
                OnPropertyChanged(nameof(IsValid));
                OnPropertyChanged(nameof(FromBackgroundBrush));
                if (isDuplicate)
                        SetBindingResult(HotkeyBindingState.Inactive, null);
                else
                        UpdateCombinedError();
        }

        public void SetBindingResult(HotkeyBindingState state, string? message)
        {
                if (_bindingState == state && string.Equals(_bindingError, message, StringComparison.Ordinal))
                {
                        UpdateCombinedError();
                        return;
                }

                _bindingState = state;
                _bindingError = message;
                OnPropertyChanged(nameof(BindingState));
                OnPropertyChanged(nameof(BindingStatusGlyph));
                OnPropertyChanged(nameof(BindingStatusBrush));
                OnPropertyChanged(nameof(BindingStatusTooltip));
                UpdateCombinedError();
        }

        private void EvaluateFrom(bool commit)
        {
                _formattedFrom = FormatHotkey(_fromHotkeyText);
                (_isFromValid, _fromValidationMessage) = ValidateFormattedHotkey(_formattedFrom, true);

                if (commit)
                {
                        ApplyFormattedValue(ref _fromHotkeyText, _formattedFrom, nameof(FromHotkey));
                        Map.FromHotKey = _isFromValid ? _formattedFrom : null;
                }
                else if (!_isFromValid)
                {
                        Map.FromHotKey = null;
                }

                OnPropertyChanged(nameof(IsFromValid));
                OnPropertyChanged(nameof(IsFromInputValid));
                OnPropertyChanged(nameof(IsValid));
                OnPropertyChanged(nameof(FromBackgroundBrush));
                UpdateCombinedError();
        }

        private void EvaluateTo(bool commit)
        {
                _formattedTo = FormatHotkey(_toHotkeyText);
                (_isToValid, _toValidationMessage) = ValidateFormattedHotkey(_formattedTo, false);

                if (commit)
                {
                        ApplyFormattedValue(ref _toHotkeyText, _formattedTo, nameof(ToHotkey));
                        Map.ToHotKey = _isToValid ? _formattedTo : null;
                }
                else if (!_isToValid)
                {
                        Map.ToHotKey = null;
                }

                OnPropertyChanged(nameof(IsToValid));
                OnPropertyChanged(nameof(IsValid));
                OnPropertyChanged(nameof(ToBackgroundBrush));
                UpdateCombinedError();
        }

        private void ApplyFormattedValue(ref string storage, string? formatted, string propertyName)
        {
                string newValue = formatted ?? string.Empty;
                if (!string.Equals(storage, newValue, StringComparison.Ordinal))
                {
                        storage = newValue;
                        OnPropertyChanged(propertyName);
                }
        }

        private (bool isValid, string? message) ValidateFormattedHotkey(string? formatted, bool isFrom)
        {
                if (string.IsNullOrWhiteSpace(formatted))
                        return (false, "Enter a hotkey.");

                try
                {
                        _ = Hotkey.Parse(formatted);
                        return (true, null);
                }
                catch (Exception ex)
                {
                        return (false, ex.Message);
                }
        }

        private string? FormatHotkey(string? value)
        {
                if (string.IsNullOrWhiteSpace(value))
                        return string.Empty;

                IEnumerable<string> parts = value
                        .Split(_separators, StringSplitOptions.RemoveEmptyEntries)
                        .Select(part => part.Trim())
                        .Where(part => !string.IsNullOrWhiteSpace(part))
                        .Select(part => part.ToUpperInvariant());

                string formatted = string.Join('+', parts);
                return formatted;
        }

        private void UpdateCombinedError()
        {
                string? message = null;
                if (!IsFromInputValid)
                        message = _isDuplicate ? "Duplicate 'From' hotkey." : _fromValidationMessage;
                else if (!_isToValid)
                        message = _toValidationMessage;
                else if (_bindingState == HotkeyBindingState.Failed)
                        message = _bindingError;

                if (!string.Equals(_combinedError, message, StringComparison.Ordinal))
                {
                        _combinedError = message;
                        OnPropertyChanged(nameof(BindingErrorMessage));
                        OnPropertyChanged(nameof(HasBindingError));
                        OnPropertyChanged(nameof(BindingErrorVisibility));
                        OnPropertyChanged(nameof(BindingStatusTooltip));
                }
        }

        private bool SetField<T>(ref T storage, T value, string propertyName)
        {
                if (EqualityComparer<T>.Default.Equals(storage, value))
                        return false;

                storage = value;
                OnPropertyChanged(propertyName);
                return true;
        }

        private static Brush? ResolveThemeBrush(string key)
        {
                if (Application.Current?.Resources.TryGetValue(key, out object? value) == true && value is Brush brush)
                        return brush;
                return null;
        }

        private void OnPropertyChanged(string propertyName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
