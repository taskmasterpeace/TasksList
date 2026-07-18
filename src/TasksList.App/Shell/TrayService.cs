using System.Drawing;
using Forms = System.Windows.Forms;

namespace TasksList.App.Shell;

public sealed record TrayCommands(
    Action NewSticky,
    Action NewFromClipboard,
    Action ClipboardPalette,
    Action CaptureRegion,
    Action ToggleAllNotes,
    Action ShowLibrary,
    Action DisableGhostMode,
    Action<bool> SetMonitoringPaused,
    Action ShowSettings,
    Action Exit);

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _pauseItem;

    public TrayService(TrayCommands commands, bool monitoringPaused)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Item("New Sticky", commands.NewSticky));
        menu.Items.Add(Item("New from Clipboard", commands.NewFromClipboard));
        menu.Items.Add(Item("Clipboard Palette", commands.ClipboardPalette));
        menu.Items.Add(Item("Capture Region", commands.CaptureRegion));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Item("Show/Hide All Notes", commands.ToggleAllNotes));
        menu.Items.Add(Item("Library", commands.ShowLibrary));
        menu.Items.Add(Item("Disable Ghost Mode", commands.DisableGhostMode));
        _pauseItem = new Forms.ToolStripMenuItem("Pause Clipboard Monitoring")
        {
            Checked = monitoringPaused,
            CheckOnClick = true,
        };
        _pauseItem.CheckedChanged += (_, _) => commands.SetMonitoringPaused(_pauseItem.Checked);
        menu.Items.Add(_pauseItem);
        menu.Items.Add(Item("Settings", commands.ShowSettings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Item("Exit", commands.Exit));

        _icon = new Forms.NotifyIcon
        {
            Text = "Task'sList",
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => commands.ShowLibrary();
    }

    public void ShowError(string message)
    {
        _icon.BalloonTipTitle = "Task'sList shortcut conflict";
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = Forms.ToolTipIcon.Warning;
        _icon.ShowBalloonTip(6000);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }

    private static Forms.ToolStripMenuItem Item(string text, Action callback)
    {
        var item = new Forms.ToolStripMenuItem(text);
        item.Click += (_, _) => callback();
        return item;
    }
}
