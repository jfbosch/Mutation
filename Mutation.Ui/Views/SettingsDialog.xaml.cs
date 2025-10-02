using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Mutation.Ui.ViewModels;

namespace Mutation.Ui.Views;

public sealed partial class SettingsDialog : ContentDialog
{
        public SettingsDialogViewModel ViewModel { get; }

        public SettingsDialog(SettingsDialogViewModel viewModel)
        {
                this.InitializeComponent();
                ViewModel = viewModel;
                DataContext = this;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
                if (!ViewModel.SaveCommand.CanExecute(null))
                {
                        args.Cancel = true;
                        return;
                }

                ViewModel.SaveCommand.Execute(null);
                if (ViewModel.HasValidationErrors)
                {
                        args.Cancel = true;
                }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
                ViewModel.CancelCommand.Execute(null);
        }

        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
                ViewModel.ResetDefaultsCommand.Execute(null);
                args.Cancel = true;
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
                if (sender is Button button && button.Tag is SettingRowViewModel row)
                {
                        row.ApplyValue(row.Value);
                }
        }
}

public sealed class SettingTemplateSelector : DataTemplateSelector
{
        public DataTemplate? InfoTemplate { get; set; }
        public DataTemplate? NumberTemplate { get; set; }
        public DataTemplate? ActionTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
                if (item is SettingRowViewModel row)
                {
                        return row.EditorType switch
                        {
                                SettingEditorType.Info => InfoTemplate!,
                                SettingEditorType.Number => NumberTemplate!,
                                SettingEditorType.Action => ActionTemplate!,
                                _ => InfoTemplate!
                        };
                }

                return base.SelectTemplateCore(item, container);
        }
}

public sealed class ValidationStateToVisibilityConverter : IValueConverter
{
        public object Convert(object value, Type targetType, object parameter, string language)
        {
                if (value is ValidationState state)
                        return state == ValidationState.Error ? Visibility.Visible : Visibility.Collapsed;

                return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
}
