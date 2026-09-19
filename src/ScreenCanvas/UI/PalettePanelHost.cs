using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ScreenCanvas.UI;

public sealed class PalettePanelHost : Border
{
    private bool _isOpen;
    public event EventHandler? Opened;
    public bool IsOpen { get=>_isOpen; set { if(_isOpen==value)return;_isOpen=value;Visibility=value?Visibility.Visible:Visibility.Collapsed;if(value)Opened?.Invoke(this,EventArgs.Empty); } }
    public UIElement? PlacementTarget { get; set; }
    public PlacementMode Placement { get; set; }
}
