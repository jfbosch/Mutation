using CognitiveSupport;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Mutation.Ui.ViewModels.Settings;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Mutation.Ui.Views;

public sealed partial class SettingsDialog : ContentDialog
{
        private readonly Window _owner;
        private bool _isLoaded;

        public SettingsDialog(Settings settings, Window owner)
        {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                ViewModel = new SettingsDialogViewModel(settings ?? throw new ArgumentNullException(nameof(settings)));
                this.InitializeComponent();
                this.DataContext = ViewModel;

                this.Loaded += (_, __) =>
                {
                        _isLoaded = true;
                        ViewModel.RecalculateValidation();
                };
        }

        internal SettingsDialogViewModel ViewModel { get; }

        public Settings? UpdatedSettings { get; private set; }

        private void OnFormChanged(object sender, TextChangedEventArgs e)
        {
                if (!_isLoaded)
                        return;
                ViewModel.MarkDirty();
                ViewModel.RecalculateValidation();
        }

        private void OnToggleChanged(object sender, RoutedEventArgs e)
        {
                if (!_isLoaded)
                        return;
                ViewModel.MarkDirty();
                ViewModel.RecalculateValidation();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
                if (!_isLoaded)
                        return;
                ViewModel.MarkDirty();
                ViewModel.RecalculateValidation();
        }

        private void OnNumberBoxChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
                if (!_isLoaded)
                        return;
                ViewModel.MarkDirty();
                ViewModel.RecalculateValidation();
        }

        private async void BrowseTempFolder_Click(object sender, RoutedEventArgs e)
        {
                var folder = await PickFolderAsync();
                if (folder != null)
                {
                        ViewModel.WorkingCopy.SpeechToTextSettings.TempDirectory = folder;
                        ViewModel.MarkDirty();
                        ViewModel.RecalculateValidation();
                }
        }

        private async void BrowseSuccess_Click(object sender, RoutedEventArgs e) => await SelectBeepFileAsync(path => ViewModel.WorkingCopy.AudioSettings.CustomBeepSettings.BeepSuccessFile = path);
        private async void BrowseFailure_Click(object sender, RoutedEventArgs e) => await SelectBeepFileAsync(path => ViewModel.WorkingCopy.AudioSettings.CustomBeepSettings.BeepFailureFile = path);
        private async void BrowseMute_Click(object sender, RoutedEventArgs e) => await SelectBeepFileAsync(path => ViewModel.WorkingCopy.AudioSettings.CustomBeepSettings.BeepMuteFile = path);
        private async void BrowseStart_Click(object sender, RoutedEventArgs e) => await SelectBeepFileAsync(path => ViewModel.WorkingCopy.AudioSettings.CustomBeepSettings.BeepStartFile = path);
        private async void BrowseEnd_Click(object sender, RoutedEventArgs e) => await SelectBeepFileAsync(path => ViewModel.WorkingCopy.AudioSettings.CustomBeepSettings.BeepEndFile = path);
        private async void BrowseUnmute_Click(object sender, RoutedEventArgs e) => await SelectBeepFileAsync(path => ViewModel.WorkingCopy.AudioSettings.CustomBeepSettings.BeepUnmuteFile = path);

        private async Task SelectBeepFileAsync(Action<string?> apply)
        {
                var file = await PickAudioFileAsync();
                if (file != null)
                {
                        apply(file);
                        ViewModel.MarkDirty();
                        ViewModel.RecalculateValidation();
                }
        }

        private async Task<string?> PickAudioFileAsync()
        {
                var picker = new FileOpenPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_owner));
                picker.FileTypeFilter.Add(".wav");
                picker.FileTypeFilter.Add(".mp3");
                picker.FileTypeFilter.Add(".ogg");
                picker.FileTypeFilter.Add(".flac");
                StorageFile? file = await picker.PickSingleFileAsync();
                return file?.Path;
        }

        private async Task<string?> PickFolderAsync()
        {
                var picker = new FolderPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_owner));
                picker.FileTypeFilter.Add("*");
                StorageFolder? folder = await picker.PickSingleFolderAsync();
                return folder?.Path;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
                ViewModel.RecalculateValidation();
                if (!ViewModel.CanSave)
                {
                        args.Cancel = true;
                        return;
                }

                UpdatedSettings = ViewModel.BuildUpdatedSettings();
        }
}
