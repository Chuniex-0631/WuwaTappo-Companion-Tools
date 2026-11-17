using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// Calls the real .NET Framework control event/message paths. No SendInput,
// cursor movement, pet discovery, UIA monitoring or reward operation is used.
internal static class ButtonInteractionTests
{
    private const BindingFlags InstanceMethods = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int assertions;
    private static int scenarios;

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        NativeMethods.InputSender = delegate(uint count, NativeMethods.INPUT[] inputs, int size) { return count; };
        try
        {
            StockButtonReproducesRegression();
            MouseActivation(new WindowChromeButton(), "minimize");
            MouseActivation(new ModernButton(), "stop");
            MouseActivation(new WindowChromeButton(), "close");
            KeyboardActivation(new WindowChromeButton(), "window control");
            KeyboardActivation(new ModernButton(), "operation control");
            RealMinimizeWhileSending();
            Console.WriteLine("PASS: button interaction; " + scenarios + " scenarios; " + assertions + " assertions; no real input.");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.Exit(1); }
    }

    private static void Assert(bool value, string description)
    {
        assertions++;
        if (!value) throw new Exception(description);
    }

    private static object Invoke(object owner, string name, params object[] args)
    {
        return owner.GetType().GetMethod(name, InstanceMethods).Invoke(owner, args);
    }

    private static object Field(object owner, string name)
    {
        return owner.GetType().GetField(name, InstanceMethods).GetValue(owner);
    }

    private static bool MouseIsDown(Button button)
    {
        // FlagMouseDown is the actual ButtonBase flag used by .NET Framework's
        // OnMouseUp to decide whether a mouse press should produce Click.
        return (bool)typeof(ButtonBase).GetMethod("GetFlag", InstanceMethods).Invoke(button, new object[] { 2 });
    }

    private static Form Host(Button button)
    {
        Form form = new Form();
        form.Text = "Button interaction regression test";
        form.ShowInTaskbar = false;
        form.TopMost = true;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(40, 40);
        form.ClientSize = new Size(260, 130);
        button.Location = new Point(30, 30);
        button.Size = new Size(180, 50);
        button.Text = "Test";
        form.Controls.Add(button);
        form.Show();
        Application.DoEvents();
        Invoke(button, "OnMouseEnter", EventArgs.Empty);
        return form;
    }

    private static MouseEventArgs Mouse(Button button)
    {
        return new MouseEventArgs(MouseButtons.Left, 1, button.Width / 2, button.Height / 2, 0);
    }

    private static void StockButtonReproducesRegression()
    {
        using (Button button = new Button())
        {
            button.FlatStyle = FlatStyle.Flat;
            using (Form host = Host(button))
            {
                int clicks = 0;
                button.Click += delegate { clicks++; };
                button.Capture = true;
                Invoke(button, "OnMouseDown", Mouse(button));
                Assert(MouseIsDown(button), "Baseline mouse press enters ButtonBase mouse-down state");
                Invoke(button, "OnKeyDown", new KeyEventArgs(Keys.F24));
                Invoke(button, "OnKeyUp", new KeyEventArgs(Keys.F24));
                Assert(!MouseIsDown(button), "Baseline reproduces F24 KeyUp clearing the real ButtonBase state");
                Invoke(button, "OnMouseUp", Mouse(button));
                Assert(clicks == 0, "Baseline reproduces the lost Click after F24 interference");
                Console.WriteLine("PASS: unpatched .NET Button reproduces the canceled mouse click.");
            }
        }
        scenarios++;
    }

    private static void MouseActivation(Button button, string name)
    {
        using (button)
        using (Form host = Host(button))
        {
            int clicks = 0;
            button.Click += delegate { clicks++; };
            button.Capture = true;
            Invoke(button, "OnMouseDown", Mouse(button));
            Assert(MouseIsDown(button), name + ": mouse-down is armed");
            Assert(clicks == 0, name + ": action does not execute prematurely on mouse-down");
            Keys[] backgroundKeys = new Keys[] { Keys.F24, Keys.F24 | Keys.Shift,
                Keys.F24 | Keys.Control, Keys.F24 | Keys.Alt, Keys.F24 | Keys.Control | Keys.Shift | Keys.Alt };
            for (int i = 0; i < backgroundKeys.Length; i++)
            {
                KeyEventArgs down = new KeyEventArgs(backgroundKeys[i]);
                KeyEventArgs up = new KeyEventArgs(backgroundKeys[i]);
                Invoke(button, "OnKeyDown", down);
                Invoke(button, "OnKeyUp", up);
                Assert(MouseIsDown(button), name + ": F24 preserves ButtonBase mouse-down state");
                Assert(down.Handled && up.Handled, name + ": background F24 is handled locally");
                Assert(clicks == 0, name + ": F24 never clicks the control");
            }
            Invoke(button, "OnMouseUp", Mouse(button));
            Assert(clicks == 1, name + ": releasing the mouse produces exactly one Click");
            Assert(!MouseIsDown(button), name + ": release resets ButtonBase state");
        }
        scenarios++;
    }

    private static void KeyboardActivation(Button button, string name)
    {
        using (button)
        using (Form host = Host(button))
        {
            int clicks = 0;
            int downs = 0;
            int ups = 0;
            button.Click += delegate { clicks++; };
            button.KeyDown += delegate { downs++; };
            button.KeyUp += delegate { ups++; };
            Invoke(button, "OnKeyDown", new KeyEventArgs(Keys.F24));
            Invoke(button, "OnKeyUp", new KeyEventArgs(Keys.F24));
            Assert(clicks == 0 && downs == 0 && ups == 0, name + ": F24 does not activate or reach user key handlers");
            Assert(!MouseIsDown(button), name + ": F24 does not arm a mouse press");

            Invoke(button, "OnKeyDown", new KeyEventArgs(Keys.Space));
            Invoke(button, "OnKeyDown", new KeyEventArgs(Keys.F24));
            Invoke(button, "OnKeyUp", new KeyEventArgs(Keys.F24));
            Invoke(button, "OnKeyUp", new KeyEventArgs(Keys.Space));
            Assert(clicks == 1, name + ": Space still activates once with an intervening F24 release");
            Assert(downs == 1 && ups == 1, name + ": standard Space key handlers are preserved");

            host.AcceptButton = button;
            Assert((bool)Invoke(host, "ProcessDialogKey", Keys.Enter), name + ": Enter remains a dialog activation key");
            Assert(clicks == 2, name + ": Enter activates the default button once");
            button.PerformClick();
            Assert(clicks == 3, name + ": PerformClick remains available");
            button.AccessibilityObject.DoDefaultAction();
            Assert(clicks == 4, name + ": accessibility default action remains available");

            Invoke(button, "OnMouseDown", Mouse(button));
            Assert(MouseIsDown(button), name + ": second mouse press is armed");
            KeyEventArgs normalDown = new KeyEventArgs(Keys.A);
            KeyEventArgs normalUp = new KeyEventArgs(Keys.A);
            Invoke(button, "OnKeyDown", normalDown);
            Invoke(button, "OnKeyUp", normalUp);
            Assert(!MouseIsDown(button), name + ": non-F24 key keeps the original ButtonBase cancellation behavior");
            Assert(downs == 2 && ups == 2, name + ": non-F24 key events reach standard handlers");
            Invoke(button, "OnMouseUp", Mouse(button));
            Assert(clicks == 4, name + ": canceled press does not click");
        }
        scenarios++;
    }

    private static void ButtonMessage(Button button, int message, int keyOrFlags, int coordinates)
    {
        Message value = Message.Create(button.Handle, message, new IntPtr(keyOrFlags), new IntPtr(coordinates));
        Invoke(button, "WndProc", value);
    }

    private static void RealMinimizeWhileSending()
    {
        int presses = 0;
        using (ManualResetEvent sent = new ManualResetEvent(false))
        {
            NativeMethods.InputSender = delegate(uint count, NativeMethods.INPUT[] inputs, int size)
            {
                if (count == 2) { Interlocked.Increment(ref presses); sent.Set(); }
                return count;
            };
            using (MonitorForm form = new MonitorForm())
            {
                form.TopMost = true;
                form.Show();
                Application.DoEvents();
                Invoke(form, "PrepareTrayWindow");
                MethodInfo start = typeof(MonitorForm).GetMethod("StartClicker", InstanceMethods);
                if (start.GetParameters().Length == 2)
                {
                    // Enable only the worker's lease guard; never start the scan timer or UIA.
                    typeof(MonitorForm).GetField("monitoring", InstanceMethods).SetValue(form, true);
                    start.Invoke(form, new object[] { (int)Field(form, "monitorGeneration"), 150L });
                }
                else start.Invoke(form, null);
                Assert(sent.WaitOne(1000), "Real form starts a mocked F24 worker without scanning the pet");
                Assert((bool)Field(form, "clickerRunning"), "Real form is sending before minimize");
                Button minimize = (Button)Field(form, "minimizeButton");
                Invoke(minimize, "OnMouseEnter", EventArgs.Empty);
                int coordinates = (minimize.Height / 2 << 16) | (minimize.Width / 2);
                ButtonMessage(minimize, 0x0201, 1, coordinates); // WM_LBUTTONDOWN
                Assert(MouseIsDown(minimize), "Real minimize button is armed through WndProc");
                ButtonMessage(minimize, 0x0100, (int)Keys.F24, 0); // WM_KEYDOWN
                ButtonMessage(minimize, 0x0101, (int)Keys.F24, 0); // WM_KEYUP
                Assert(MouseIsDown(minimize), "Real minimize button survives F24 WndProc messages");
                ButtonMessage(minimize, 0x0202, 0, coordinates); // WM_LBUTTONUP
                Assert(!form.Visible && !form.IsDisposed, "Mouse release minimizes the real form to its tray");
                Assert((bool)Field(form, "clickerRunning"), "Minimize leaves the mocked worker running");
                int atMinimize = Interlocked.CompareExchange(ref presses, 0, 0);
                Thread.Sleep(80);
                Assert(Interlocked.CompareExchange(ref presses, 0, 0) > atMinimize, "Mocked F24 delivery continues while hidden");
                Invoke(form, "StopAllOperations");
                int atStop = Interlocked.CompareExchange(ref presses, 0, 0);
                Thread.Sleep(60);
                Assert(Interlocked.CompareExchange(ref presses, 0, 0) == atStop, "Tray lifecycle test stops all mocked input cleanly");
                form.Close();
            }
        }
        NativeMethods.InputSender = delegate(uint count, NativeMethods.INPUT[] inputs, int size) { return count; };
        scenarios++;
    }
}
