using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using CognitiveSupport;

namespace Mutation.Ui.Services;

/// <summary>
/// Replaces WinForms tooltip logic with WinUI ToolTipService.
/// </summary>
public class TooltipManager
{
    private readonly Settings _settings;

    public TooltipManager(Settings settings)
    {
        _settings = settings;
    }

    public void SetToolTip(UIElement element, string text)
    {
        var toolTip = new ToolTip { Content = text };
        ToolTipService.SetToolTip(element, toolTip);
    }
}
