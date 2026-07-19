namespace TasksList.App.Shell;

public enum AppNotificationKind
{
    Information,
    Success,
    Warning,
}

public sealed record AppNotification(
    string Message,
    AppNotificationKind Kind,
    string? ActionLabel,
    Func<Task>? Action);

public sealed class AppNotificationService
{
    public event EventHandler? Changed;

    public AppNotification? Current { get; private set; }

    public void Show(
        string message,
        AppNotificationKind kind,
        string? actionLabel = null,
        Func<Task>? action = null)
    {
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Message is required.", nameof(message));
        if ((actionLabel is null) != (action is null))
        {
            throw new ArgumentException("An action label and callback must be supplied together.");
        }

        Current = new AppNotification(message.Trim(), kind, actionLabel, action);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dismiss()
    {
        if (Current is null) return;
        Current = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task InvokeActionAsync()
    {
        var notification = Current;
        if (notification?.Action is null) return;

        await notification.Action();
        if (ReferenceEquals(Current, notification)) Dismiss();
    }
}
