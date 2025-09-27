using Mutation.Ui.Services;
using System;
using CognitiveSupport;
using System.ComponentModel;

namespace Mutation.Ui;

public sealed class HotkeyRouterEntry : INotifyPropertyChanged
{
        private readonly Action _onChanged;

        internal HotKeyRouterSettings.HotKeyRouterMap Map { get; }

        public HotkeyRouterEntry(HotKeyRouterSettings.HotKeyRouterMap map, Action onChanged)
        {
                Map = map ?? throw new ArgumentNullException(nameof(map));
                _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        }

        public string FromHotkey
        {
                get => Map.FromHotKey ?? string.Empty;
                set => UpdateHotkey(value, true);
        }

        public string ToHotkey
        {
                get => Map.ToHotKey ?? string.Empty;
                set => UpdateHotkey(value, false);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void UpdateHotkey(string? value, bool isFrom)
        {
                string normalized = Normalize(value);
                string current = Normalize(isFrom ? Map.FromHotKey : Map.ToHotKey);
                if (string.Equals(normalized, current, StringComparison.Ordinal))
                        return;

                string? storedValue = string.IsNullOrWhiteSpace(normalized) ? null : normalized;
                if (isFrom)
                        Map.FromHotKey = storedValue;
                else
                        Map.ToHotKey = storedValue;

                OnPropertyChanged(isFrom ? nameof(FromHotkey) : nameof(ToHotkey));
                _onChanged();
        }

        private static string Normalize(string? value) =>
                string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

        private void OnPropertyChanged(string propertyName) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
