using System.IO;
using System.Windows;
using TasksList.Infrastructure.Storage;

namespace TasksList.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TasksList");
        var database = new TasksListDatabase(Path.Combine(dataDirectory, "taskslist.db"));

        try
        {
            await database.InitializeAsync();
            var window = new MainWindow(database);
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
}
