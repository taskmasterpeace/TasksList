using System.Windows;
using TasksList.App.Shell;
using TasksList.Core.Models;

namespace TasksList.App.Places;

public partial class NewPlaceDialog : Window
{
    private readonly IReadOnlyList<ParentChoice> _choices;

    public NewPlaceDialog(IReadOnlyList<Place> places)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => DwmWindowService.Apply(this, DwmWindowKind.Palette);
        _choices = [
            new ParentChoice(null, "Top level"),
            .. places.Select(place => new ParentChoice(place.Id, place.Name)),
        ];
        ParentBox.ItemsSource = _choices;
        ParentBox.SelectedIndex = 0;
        Loaded += (_, _) => NameBox.Focus();
    }

    public string PlaceName => NameBox.Text.Trim();

    public PlaceId? ParentPlaceId => (ParentBox.SelectedItem as ParentChoice)?.Id;

    private void CreateClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PlaceName))
        {
            NameBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private sealed record ParentChoice(PlaceId? Id, string Name);
}
