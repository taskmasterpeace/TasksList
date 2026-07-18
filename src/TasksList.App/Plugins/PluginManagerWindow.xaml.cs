using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TasksList.PluginSdk;

namespace TasksList.App.Plugins;

public partial class PluginManagerWindow : Window
{
    private readonly List<PluginCardViewModel> _plugins;

    public PluginManagerWindow(PluginCatalogSnapshot catalog)
    {
        InitializeComponent();
        _plugins = catalog.Plugins.Select(entry => new PluginCardViewModel(entry.Manifest)).ToList();
        PluginList.ItemsSource = _plugins;
        CatalogStatus.Text = catalog.Errors.Count == 0
            ? $"{_plugins.Count} compatible plugins"
            : $"{_plugins.Count} compatible · {catalog.Errors.Count} quarantined";
    }

    private void ReviewClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: PluginCardViewModel plugin })
        {
            return;
        }

        MessageBox.Show(
            $"{plugin.Name} requests:\n\n{plugin.Capabilities.Replace(" · ", "\n", StringComparison.Ordinal)}\n\n" +
            "Capabilities remain denied until the plugin is enabled in a later release step.",
            $"Review {plugin.Name}",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void InstallClick(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = "Install Task'sList plugin",
            Filter = "Task'sList plugins (*.taskplugin)|*.taskplugin",
            CheckFileExists = true,
        };
        if (picker.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var pluginsRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TasksList",
                "plugins");
            var installed = PluginPackageInstaller.Install(picker.FileName, pluginsRoot);
            _plugins.Add(new PluginCardViewModel(installed.Manifest));
            PluginList.Items.Refresh();
            var manifest = installed.Manifest;
            MessageBox.Show(
                $"{manifest.Name} was installed. Its capabilities remain denied until you review and enable them.",
                "Plugin installed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            MessageBox.Show(exception.Message, "Plugin rejected", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void TitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}

public sealed class PluginCardViewModel
{
    public PluginCardViewModel(PluginManifest manifest) => Manifest = manifest;

    public PluginManifest Manifest { get; }

    public string Name => Manifest.Name;

    public string Id => Manifest.Id;

    public string Version => $"v{Manifest.Version}";

    public string Capabilities => string.Join(" · ", Manifest.Capabilities.Select(item => item.ToManifestName()));
}
