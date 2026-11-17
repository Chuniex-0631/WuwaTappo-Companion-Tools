using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// UI-thread owned. Hiding preserves the form handle (and registered hotkeys).
internal sealed class TrayController : IDisposable
{
    private readonly Form form;
    private readonly Action prepareWindow;
    private readonly Func<string> readStatus;
    private readonly NotifyIcon icon;
    private readonly ContextMenuStrip menu;
    private readonly ToolStripMenuItem statusItem;
    private readonly Timer timer;
    private readonly string title;
    private bool notified;
    private bool disposed;

    internal TrayController(Form form, string title, Action prepareWindow,
        Func<string> readStatus, Action stopAll)
    {
        this.form = form;
        this.title = title;
        this.prepareWindow = prepareWindow;
        this.readStatus = readStatus;
        menu = new ContextMenuStrip();
        statusItem = new ToolStripMenuItem("已停止");
        statusItem.Enabled = false;
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("显示主窗口", null, delegate { Restore(); });
        menu.Items.Add("停止全部操作", null, delegate { stopAll(); RefreshStatus(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, delegate { form.Close(); });
        icon = new NotifyIcon();
        icon.Icon = form.Icon ?? SystemIcons.Application;
        icon.Text = title;
        icon.ContextMenuStrip = menu;
        icon.DoubleClick += delegate { Restore(); };
        timer = new Timer();
        timer.Interval = 500;
        timer.Tick += delegate { RefreshStatus(); };
        form.Resize += OnResize;
        form.FormClosed += OnClosed;
        RefreshStatus();
        timer.Start();
    }

    internal void Minimize()
    {
        if (disposed || form.IsDisposed) return;
        // Detect a missing Explorer tray; second launch is an additional recovery path.
        try
        {
            if (FindWindow("Shell_TrayWnd", null) == IntPtr.Zero)
                throw new InvalidOperationException("Windows notification area is unavailable.");
            icon.Visible = true;
        }
        catch
        {
            Restore();
            MessageBox.Show(form, "无法创建系统托盘图标，窗口将保持显示。", title,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        prepareWindow();
        form.Hide();
        if (!notified)
        {
            notified = true;
            try { icon.ShowBalloonTip(4000, title,
                "已最小化到托盘，当前操作继续运行。双击图标恢复；右键可停止全部操作或退出。",
                ToolTipIcon.Info); }
            catch { /* Notifications may be disabled; the tray menu remains available. */ }
        }
    }

    internal void Restore()
    {
        if (disposed || form.IsDisposed) return;
        prepareWindow();
        form.WindowState = FormWindowState.Normal;
        // A disconnected monitor must not strand the restored window off-screen.
        Rectangle area = Screen.FromRectangle(form.Bounds).WorkingArea;
        if (!area.IntersectsWith(form.Bounds))
            form.Location = new Point(area.Left + Math.Max(0, (area.Width - form.Width) / 2),
                area.Top + Math.Max(0, (area.Height - form.Height) / 2));
        form.Show();
        form.Activate();
    }

    private void OnResize(object sender, EventArgs e)
    {
        if (form.WindowState == FormWindowState.Minimized) Minimize();
    }

    private void RefreshStatus()
    {
        if (disposed) return;
        string status = readStatus();
        statusItem.Text = status;
        string text = title + " · " + status;
        icon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
    }

    private void OnClosed(object sender, FormClosedEventArgs e) { Dispose(); }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        form.Resize -= OnResize;
        form.FormClosed -= OnClosed;
        timer.Stop();
        timer.Dispose();
        icon.Visible = false;
        icon.Dispose();
        menu.Dispose();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string windowName);
}

internal static class CompanionActivation
{
    // One session for the suite prevents two assistants from sending competing input.
    internal const string SessionMutex = "Local\\WuwaTappoCompanionTools.Session";
    internal static readonly int Message = unchecked((int)RegisterWindowMessage(
        "WuwaTappoCompanionTools.Restore.v1"));

    internal static void RestoreExisting()
    {
        AllowSetForegroundWindow(-1);
        if (Message != 0) PostMessage(new IntPtr(0xffff), Message, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);
}
