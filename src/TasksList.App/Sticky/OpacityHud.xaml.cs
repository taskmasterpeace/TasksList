using System.Windows.Controls;

namespace TasksList.App.Sticky;

public partial class OpacityHud : UserControl
{
    public OpacityHud()
    {
        InitializeComponent();
    }

    public void SetOpacity(double opacity) => ValueText.Text = $"{opacity:P0}";
}
