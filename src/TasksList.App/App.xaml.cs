using System.IO;
using System.Windows;
using System.Windows.Media;
using TasksList.App.Theming;
using TasksList.Infrastructure.Storage;

namespace TasksList.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataDirectory = Environment.GetEnvironmentVariable("TASKSLIST_DATA_DIR") ??
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TasksList");
        var database = new TasksListDatabase(Path.Combine(dataDirectory, "taskslist.db"));

        try
        {
            LoadUserTheme(dataDirectory);
            await database.InitializeAsync();
            var window = new MainWindow(database, dataDirectory);
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Task'sList could not open its local data.\n\n{exception.Message}",
                "Task'sList",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void LoadUserTheme(string dataDirectory)
    {
        var activeThemeDirectory = Path.Combine(dataDirectory, "themes", "active");
        var activeThemePath = Path.Combine(activeThemeDirectory, "theme.json");
        if (!File.Exists(activeThemePath))
        {
            Directory.CreateDirectory(activeThemeDirectory);
            var bundledThemePath = Path.Combine(AppContext.BaseDirectory, "themes", "default", "theme.json");
            if (File.Exists(bundledThemePath))
            {
                File.Copy(bundledThemePath, activeThemePath);
            }
        }

        var theme = File.Exists(activeThemePath)
            ? ThemeLoader.LoadOrDefault(activeThemePath)
            : ThemeLoader.Default;
        ApplyBrush(theme, "canvas", "CanvasBrush");
        ApplyBrush(theme, "panel", "PanelBrush");
        ApplyBrush(theme, "card", "CardBrush");
        ApplyBrush(theme, "border", "BorderBrush");
        ApplyBrush(theme, "accent", "PrimaryBrush");
        ApplyBrush(theme, "success", "SuccessBrush");
    }

    private void ApplyBrush(ThemeDefinition theme, string token, string resourceKey)
    {
        if (theme.Tokens.TryGetValue(token, out var value) &&
            ColorConverter.ConvertFromString(value) is Color color)
        {
            Resources[resourceKey] = new SolidColorBrush(color);
        }
    }
}
