using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// Exercises the actual WinForms tray lifecycle without a pet or reward account.
internal static class DesktopLifecycleTests
{
    private static int assertions;
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        NativeMethods.InputSender = delegate(uint count, NativeMethods.INPUT[] inputs, int size) { return count; };
        try
        {
            TrayLifecycle();
            ApplicationLifecycle();
            if (typeof(MonitorForm).GetField("clickerToggleButton", BindingFlags.Instance | BindingFlags.NonPublic) != null)
            {
                ToolkitInputFailure();
                ToolkitTrayStop();
            }
            Console.WriteLine("PASS: desktop lifecycle; " + assertions + " assertions.");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.Exit(1); }
    }

    private static void Assert(bool value, string description)
    {
        assertions++;
        if (!value) throw new Exception(description);
    }

    private static object Field(object owner, string name)
    {
        return owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }

    private static void Invoke(object owner, string name, params object[] args)
    {
        owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
    }

    private static void ToolkitInputFailure()
    {
        int presses = 0;
        NativeMethods.InputSender = delegate(uint count, NativeMethods.INPUT[] inputs, int size)
        {
            if (count == 2) Interlocked.Increment(ref presses);
            return 0; // Windows rejects input; the real Windows API is never called.
        };
        using (MonitorForm form = new MonitorForm())
        {
            form.Show();
            Application.DoEvents();
            // PerformClick follows the same event path as keyboard and accessibility activation.
            ((Button)Field(form, "clickerToggleButton")).PerformClick();
            Thread worker = (Thread)Field(form, "clickerWorker");
            Assert(worker != null && worker.Join(1000), "Rejected input stops the worker promptly");
            Invoke(form, "UpdateClickerRate", null, EventArgs.Empty);
            Assert(!(bool)Field(form, "clickerRunning"), "Rejected input clears the running state");
            Assert(((Button)Field(form, "clickerToggleButton")).Text == "开始运行", "Failure restores the start button");
            Assert(((Label)Field(form, "clickerStatusLabel")).Text.Contains("已停止"), "Failure reports stopped state");
            Assert(((Label)Field(form, "actualRateLabel")).Text == "0 次/秒", "Failure clears displayed input rate");
            Assert(((Label)Field(form, "lastActionLabel")).Text.Contains("输入发送失败"), "Failure explains why input stopped");
            Assert(Interlocked.CompareExchange(ref presses, 0, 0) == 1, "Rejected input is attempted once");
            Thread.Sleep(60);
            Assert(Interlocked.CompareExchange(ref presses, 0, 0) == 1, "Failure never keeps sending in the background");
            form.Close();
        }
    }

    private static void ToolkitTrayStop()
    {
        int presses = 0;
        using (ManualResetEvent sent = new ManualResetEvent(false))
        {
            NativeMethods.InputSender = delegate(uint count, NativeMethods.INPUT[] inputs, int size)
            {
                if (count == 2)
                {
                    Interlocked.Increment(ref presses);
                    sent.Set();
                }
                return count; // Record only; no real key or mouse event leaves this test.
            };
            using (MonitorForm form = new MonitorForm())
            {
                form.Show();
                Application.DoEvents();
                ((Button)Field(form, "clickerToggleButton")).PerformClick();
                Assert(sent.WaitOne(1000), "Click event starts the mocked sender");
                TrayController tray = (TrayController)Field(form, "tray");
                ContextMenuStrip menu = (ContextMenuStrip)Field(tray, "menu");
                tray.Minimize();
                Assert(!form.Visible && (bool)Field(form, "clickerRunning"), "Minimize preserves the running sender");
                ((ToolStripMenuItem)menu.Items[3]).PerformClick();
                Thread worker = (Thread)Field(form, "clickerWorker");
                Assert(worker == null || worker.Join(1000), "Tray stop drains the sender");
                Assert(!(bool)Field(form, "clickerRunning"), "Tray stop clears running state");
                Assert(((Button)Field(form, "clickerToggleButton")).Text == "开始运行", "Tray stop restores start button");
                Assert(menu.Items[0].Text == "已停止", "Tray stop refreshes its status");
                int atStop = Interlocked.CompareExchange(ref presses, 0, 0);
                Thread.Sleep(80);
                Assert(Interlocked.CompareExchange(ref presses, 0, 0) == atStop, "No new input is sent after tray stop returns");

                // Simulate an already-active monitor without starting UIA or scanning a real pet.
                typeof(MonitorForm).GetField("monitoring", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form, true);
                int generation = (int)Field(form, "monitorGeneration");
                ((ToolStripMenuItem)menu.Items[3]).PerformClick();
                Assert(!(bool)Field(form, "monitoring"), "Tray stop also clears monitoring");
                Assert((int)Field(form, "monitorGeneration") > generation, "Tray stop invalidates outstanding monitor work");
                form.Close();
            }
        }
        NativeMethods.InputSender = delegate(uint count, NativeMethods.INPUT[] inputs, int size) { return count; };
    }

    private static void TrayLifecycle()
    {
        using (Form form = new Form())
        {
            int stops = 0;
            int prepared = 0;
            string status = "运行中";
            using (TrayController tray = new TrayController(form, "托盘回归测试",
                delegate { prepared++; form.Opacity = 1D; }, delegate { return status; }, delegate { stops++; status = "已停止"; }))
            {
                form.Show();
                Application.DoEvents();
                IntPtr handle = form.Handle;
                NotifyIcon icon = (NotifyIcon)Field(tray, "icon");
                ContextMenuStrip menu = (ContextMenuStrip)Field(tray, "menu");
                Assert(!icon.Visible, "No idle tray icon before first minimize");
                for (int i = 0; i < 20; i++)
                {
                    form.Opacity = 0.3D;
                    tray.Minimize();
                    Assert(!form.Visible && icon.Visible, "Minimize hides window and keeps recovery icon");
                    Assert(form.Handle == handle, "Hiding must preserve hotkey handle");
                    tray.Restore();
                    Assert(form.Visible && form.Opacity == 1D && form.WindowState == FormWindowState.Normal,
                        "Restore cancels fades and normalizes state");
                }
                Assert(prepared == 40, "Every transition prepares window once");
                tray.Minimize();
                ((ToolStripMenuItem)menu.Items[3]).PerformClick();
                Assert(stops == 1 && menu.Items[0].Text == "已停止", "Stop menu updates status while hidden");
                ((ToolStripMenuItem)menu.Items[2]).PerformClick();
                Assert(form.Visible, "Show menu restores window");
                tray.Minimize();
                ((ToolStripMenuItem)menu.Items[5]).PerformClick();
                Assert(form.IsDisposed && (bool)Field(tray, "disposed"), "Exit disposes form and tray");
                tray.Dispose();
                tray.Restore();
                Assert(form.IsDisposed, "Repeated disposal and late restore are harmless");
            }
        }
    }

    private static void ApplicationLifecycle()
    {
        using (MonitorForm form = new MonitorForm())
        {
            form.Show();
            Application.DoEvents();
            TrayController tray = (TrayController)Field(form, "tray");
            IntPtr handle = form.Handle;
            tray.Minimize();
            Assert(!form.Visible && !form.IsDisposed, "Real application remains alive in tray");
            Message message = Message.Create(handle, CompanionActivation.Message, IntPtr.Zero, IntPtr.Zero);
            object[] args = new object[] { message };
            typeof(MonitorForm).GetMethod("WndProc", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, args);
            Assert(form.Visible && form.Handle == handle, "Second launch message restores actual window");
            tray.Minimize();
            form.Close();
            Assert((bool)Field(form, "closing"), "Every close path marks shutdown");
            Assert(!(bool)Field(form, "monitoring") && !(bool)Field(form, "clickerRunning"), "Close stops monitoring and input");
            Assert((bool)Field(tray, "disposed"), "Closing hidden application disposes tray");
        }
    }
}
