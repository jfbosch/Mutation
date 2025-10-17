using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Mutation.Ui.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    public SettingsDialog()
    {
        this.InitializeComponent();
        
        // Set dialog size to 50% of parent window
        SetDialogSize();
    }

    private void SetDialogSize()
    {
        // The dialog will be sized when shown, based on the XamlRoot
        this.Loaded += (s, e) =>
        {
            if (this.XamlRoot != null)
            {
                var bounds = this.XamlRoot.Size;
                this.MaxWidth = bounds.Width * 0.5;
                this.MaxHeight = bounds.Height * 0.5;
            }
        };
    }
}
