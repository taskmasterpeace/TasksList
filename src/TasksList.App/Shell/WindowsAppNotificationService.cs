using System.IO;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using TasksList.Core.Models;

namespace TasksList.App.Shell;

public interface IWindowsAppNotificationService
{
    event EventHandler<WindowsNotificationActivation>? Activated;

    string? TryRegister();

    bool TryShowReminder(Note note);

    void Unregister();
}

public sealed class WindowsAppNotificationService : IWindowsAppNotificationService
{
    private readonly AppNotificationManager _manager;
    private bool _registered;

    public WindowsAppNotificationService()
    {
        _manager = AppNotificationManager.Default;
        _manager.NotificationInvoked += NotificationInvoked;
    }

    public event EventHandler<WindowsNotificationActivation>? Activated;

    public string? TryRegister()
    {
        if (_registered) return null;
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return "Windows app notifications require Windows 10 version 1809 or newer.";
        }

        try
        {
            if (!AppNotificationManager.IsSupported()) return "Windows app notifications are unavailable on this system.";
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "notification-icon.png");
            _manager.Register("Task'sList", new Uri(iconPath));
            _registered = true;
            return null;
        }
        catch (Exception exception)
        {
            return $"Windows app notifications could not be registered: {exception.Message}";
        }
    }

    public bool TryShowReminder(Note note)
    {
        if (!_registered) return false;

        try
        {
            var request = WindowsNotificationPolicy.ForReminder(note);
            var builder = new AppNotificationBuilder()
                .AddText(request.Title)
                .AddText(request.Message);
            foreach (var argument in request.Arguments)
            {
                builder.AddArgument(argument.Key, argument.Value);
            }

            var button = new AppNotificationButton(request.ActionLabel);
            foreach (var argument in request.Arguments)
            {
                button.AddArgument(argument.Key, argument.Value);
            }
            builder.AddButton(button);

            _manager.Show(builder.BuildNotification());
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Unregister()
    {
        _manager.NotificationInvoked -= NotificationInvoked;
        if (!_registered) return;
        try
        {
            _manager.Unregister();
        }
        catch
        {
            // Shutdown must remain reliable when Windows notification state is unavailable.
        }
        _registered = false;
    }

    public static string? TryUnregisterAll()
    {
        try
        {
            AppNotificationManager.Default.UnregisterAll();
            return null;
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
    }

    private void NotificationInvoked(
        AppNotificationManager sender,
        AppNotificationActivatedEventArgs args)
    {
        var activation = WindowsNotificationPolicy.ParseActivation(
            new Dictionary<string, string>(args.Arguments));
        if (activation is not null) Activated?.Invoke(this, activation);
    }
}
