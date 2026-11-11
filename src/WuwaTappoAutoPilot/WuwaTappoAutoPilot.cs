using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using AutomationCondition = System.Windows.Automation.Condition;
using AutomationControlType = System.Windows.Automation.ControlType;
using AutomationElement = System.Windows.Automation.AutomationElement;
using AutomationTreeScope = System.Windows.Automation.TreeScope;

[assembly: AssemblyTitle("桌宠自动挂机助手")]
[assembly: AssemblyProduct("桌宠自动挂机助手")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        NativeMethods.EnablePerMonitorDpiAwareness();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        bool created;
        using (Mutex mutex = new Mutex(true, "WuwaTappoAutoPilot.SingleInstance", out created))
        {
            if (!created)
            {
                MessageBox.Show("桌宠自动挂机助手已经在运行。", "桌宠自动挂机助手", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Application.Run(new MonitorForm());
        }
    }

}

internal sealed class MonitorForm : Form
{
    private const int WmNcLButtonDown = 0x00A1;
    private const int WmSyscommand = 0x0112;
    private const int ScRestore = 0xF120;
    private const int HtCaption = 0x0002;
    private const int WsMinimizeBox = 0x00020000;
    private const uint AwHide = 0x00010000;
    private const uint AwBlend = 0x00080000;
    private const int WmDpiChanged = 0x02E0;
    private const int FixedRate = 50;
    private static readonly Color WindowColor = Color.FromArgb(255, 248, 232);
    private static readonly Color CardColor = Color.FromArgb(247, 232, 203);
    private static readonly Color TextColor = Color.FromArgb(119, 82, 59);
    private static readonly Color MutedColor = Color.FromArgb(157, 124, 96);
    private static readonly Color AccentColor = Color.FromArgb(246, 204, 126);
    private static readonly Color StopColor = Color.FromArgb(190, 112, 88);
    private static readonly Color SuccessColor = Color.FromArgb(125, 145, 84);
    private static readonly Color WarningColor = Color.FromArgb(190, 132, 63);
    private static readonly Color ErrorColor = Color.FromArgb(181, 91, 82);

    private readonly System.Windows.Forms.Timer scanTimer;
    private readonly Label clickerStatusLabel;
    private readonly Label actualRateLabel;
    private readonly System.Windows.Forms.Timer clickerUiTimer;
    private readonly ModernButton toggleButton;
    private readonly Label stateLabel;
    private readonly Label detailLabel;
    private readonly Label lastActionLabel;
    private readonly WindowChromeButton minimizeButton;
    private readonly WindowChromeButton closeButton;
    private readonly System.Windows.Forms.Timer windowAnimationTimer;
    private readonly object clickerStateSync = new object();

    private volatile bool monitoring;
    private bool scanning;
    private int monitorGeneration;
    private readonly AutoPilotController controller = new AutoPilotController();
    private readonly Stopwatch sessionClock = Stopwatch.StartNew();
    private readonly List<PetReadJob> readJobs = new List<PetReadJob>();
    private int petProcessId;
    private Point lastCursorPoint;
    private double lastCursorMoveAt;
    private volatile bool clickerRunning;
    private Thread clickerWorker;
    private ManualResetEvent clickerStopSignal;
    private long sentCount;
    private long lastDisplayedCount;
    private long lastDisplayTicks;
    private long lastMonitorButtonToggleTicks;
    private long observedPointBalance = -1;
    private long targetPointBalance = -1;
    private long measuredSendRate;
    private long inputLeaseUntil;
    private int remainingInputBudget;
    private volatile bool closing;
    private bool minimizing;
    private long animationStartedAt;
    private int animationDuration;
    private double animationFrom;
    private double animationTo;
    private Action animationCompleted;
    private int currentWindowDpi;
    private float currentUiScale = 1F;

    public MonitorForm()
    {
        Text = "桌宠自动挂机助手";
        ClientSize = new Size(480, 480);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = WindowColor;
        ForeColor = TextColor;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Opacity = 0D;

        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        Label title = MakeLabel("桌宠自动挂机助手", 18F, FontStyle.Bold, TextColor);
        title.Location = new Point(28, 22);
        title.AutoSize = true;

        Label subtitle = MakeLabel("自动补充点数、等待冷却并领取宝箱", 9F, FontStyle.Regular, MutedColor);
        subtitle.Location = new Point(30, 64);
        subtitle.AutoSize = true;
        title.MouseDown += BeginWindowDrag;
        subtitle.MouseDown += BeginWindowDrag;

        minimizeButton = new WindowChromeButton();
        minimizeButton.Text = "−";
        minimizeButton.AccessibleName = "最小化";
        minimizeButton.Location = new Point(402, 9);
        minimizeButton.Size = new Size(30, 28);
        minimizeButton.ForeColor = MutedColor;
        minimizeButton.HoverColor = Color.FromArgb(241, 222, 190);
        minimizeButton.MouseDown += MinimizeButtonMouseDown;

        closeButton = new WindowChromeButton();
        closeButton.Text = "×";
        closeButton.AccessibleName = "关闭";
        closeButton.Location = new Point(438, 9);
        closeButton.Size = new Size(30, 28);
        closeButton.ForeColor = MutedColor;
        closeButton.HoverColor = Color.FromArgb(218, 155, 129);
        closeButton.MouseDown += CloseButtonMouseDown;

        Label reserveHint = MakeLabel("自动储备双槽点数，低余额隐藏后重新补足", 8.5F, FontStyle.Regular, MutedColor);
        reserveHint.Location = new Point(30, 92);
        reserveHint.AutoSize = true;

        toggleButton = new ModernButton();
        toggleButton.Location = new Point(24, 126);
        toggleButton.Size = new Size(432, 58);
        toggleButton.FillColor = AccentColor;
        toggleButton.TextColor = TextColor;
        toggleButton.BackColor = WindowColor;
        toggleButton.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
        toggleButton.Text = "开启自动挂机";
        toggleButton.MouseDown += MonitorToggleButtonMouseDown;

        RoundedPanel statusPanel = new RoundedPanel();
        statusPanel.Location = new Point(24, 208);
        statusPanel.Size = new Size(432, 212);
        statusPanel.BackColor = CardColor;
        statusPanel.CornerRadius = 10;

        Label statusCaption = MakeLabel("自动挂机状态", 9F, FontStyle.Regular, MutedColor);
        statusCaption.Location = new Point(20, 14);
        statusCaption.AutoSize = true;

        clickerStatusLabel = MakeLabel("已停止", 12F, FontStyle.Bold, TextColor);
        clickerStatusLabel.AutoSize = false;
        clickerStatusLabel.Location = new Point(20, 35);
        clickerStatusLabel.Size = new Size(392, 30);
        clickerStatusLabel.TextAlign = ContentAlignment.MiddleLeft;

        actualRateLabel = MakeLabel("点数：-- · 目标：-- · 连点：0 次/秒", 9F, FontStyle.Regular, MutedColor);
        actualRateLabel.AutoSize = false;
        actualRateLabel.Location = new Point(20, 68);
        actualRateLabel.Size = new Size(392, 28);
        actualRateLabel.TextAlign = ContentAlignment.MiddleLeft;

        Panel divider = new Panel();
        divider.Location = new Point(20, 102);
        divider.Size = new Size(392, 1);
        divider.BackColor = Color.FromArgb(213, 180, 135);

        stateLabel = MakeLabel("上宝箱：未运行", 10F, FontStyle.Bold, TextColor);
        stateLabel.Location = new Point(20, 111);
        stateLabel.AutoSize = false;
        stateLabel.Size = new Size(190, 30);
        stateLabel.TextAlign = ContentAlignment.MiddleLeft;

        detailLabel = MakeLabel("下宝箱：未运行", 10F, FontStyle.Bold, TextColor);
        detailLabel.Location = new Point(216, 111);
        detailLabel.AutoSize = false;
        detailLabel.Size = new Size(196, 30);
        detailLabel.TextAlign = ContentAlignment.MiddleLeft;

        lastActionLabel = MakeLabel("点击上方按钮开启自动挂机", 8.5F, FontStyle.Regular, MutedColor);
        lastActionLabel.AutoSize = false;
        lastActionLabel.Location = new Point(20, 151);
        lastActionLabel.Size = new Size(392, 42);
        lastActionLabel.TextAlign = ContentAlignment.MiddleLeft;

        statusPanel.Controls.Add(statusCaption);
        statusPanel.Controls.Add(clickerStatusLabel);
        statusPanel.Controls.Add(actualRateLabel);
        statusPanel.Controls.Add(divider);
        statusPanel.Controls.Add(stateLabel);
        statusPanel.Controls.Add(detailLabel);
        statusPanel.Controls.Add(lastActionLabel);

        Label footnote = MakeLabel("请在桌宠设置中保持“始终显示宝箱”开启", 8F, FontStyle.Regular, MutedColor);
        footnote.Location = new Point(30, 440);
        footnote.AutoSize = true;

        Controls.Add(title);
        Controls.Add(subtitle);
        Controls.Add(minimizeButton);
        Controls.Add(closeButton);
        Controls.Add(reserveHint);
        Controls.Add(toggleButton);
        Controls.Add(statusPanel);
        Controls.Add(footnote);

        scanTimer = new System.Windows.Forms.Timer();
        scanTimer.Interval = 900;
        scanTimer.Tick += ScanTimer_Tick;
        clickerUiTimer = new System.Windows.Forms.Timer();
        clickerUiTimer.Interval = 500;
        clickerUiTimer.Tick += UpdateClickerRate;
        clickerUiTimer.Start();
        lastDisplayTicks = Stopwatch.GetTimestamp();
        windowAnimationTimer = new System.Windows.Forms.Timer();
        windowAnimationTimer.Interval = 15;
        windowAnimationTimer.Tick += WindowAnimationTick;
        NativeMethods.GetCursorPos(out lastCursorPoint);
        MouseDown += BeginWindowDrag;
        SizeChanged += delegate { UpdateWindowRegion(); };
        Shown += delegate
        {
            UpdateWindowRegion();
            BeginOpacityAnimation(0D, 1D, 170, null);
        };
    }
    protected override CreateParams CreateParams
    {
        get
        {
            const int CsDropShadow = 0x00020000;
            CreateParams parameters = base.CreateParams;
            parameters.ClassStyle |= CsDropShadow;
            parameters.Style |= WsMinimizeBox;
            return parameters;
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmDpiChanged)
        {
            int newDpi = unchecked((int)(message.WParam.ToInt64() & 0xffffL));
            int oldDpi = currentWindowDpi > 0 ? currentWindowDpi : 96;
            int buttonWidthBefore = toggleButton == null ? 0 : toggleButton.Width;
            float buttonFontBefore = toggleButton == null ? 0F : toggleButton.Font.Size;
            base.WndProc(ref message);

            if (newDpi > 0 && oldDpi > 0 && buttonWidthBefore > 0 && toggleButton.Width > 0)
            {
                float requestedScale = newDpi / (float)oldDpi;
                float frameworkScale = toggleButton.Width / (float)buttonWidthBefore;
                if (frameworkScale <= 0F) frameworkScale = 1F;
                float remainingScale = requestedScale / frameworkScale;
                if (Math.Abs(remainingScale - 1F) > 0.015F)
                {
                    SuspendLayout();
                    SizeF scale = new SizeF(remainingScale, remainingScale);
                    foreach (Control control in Controls)
                        control.Scale(scale);
                    ResumeLayout(true);
                }

                float totalFontScale = buttonFontBefore > 0F
                    ? toggleButton.Font.Size / buttonFontBefore
                    : 1F;
                if (totalFontScale <= 0F) totalFontScale = 1F;
                float remainingFontScale = requestedScale / totalFontScale;
                if (Math.Abs(remainingFontScale - 1F) > 0.015F)
                    ScaleControlFonts(this, remainingFontScale);

                currentUiScale = Math.Max(0.5F, Math.Min(3F, currentUiScale * requestedScale));
            }

            currentWindowDpi = newDpi > 0 ? newDpi : currentWindowDpi;
            UpdateWindowRegion();
            Invalidate(true);
            return;
        }
        if (message.Msg == WmSyscommand && (message.WParam.ToInt64() & 0xFFF0L) == ScRestore && !closing)
        {
            Opacity = 0D;
            base.WndProc(ref message);
            BeginOpacityAnimation(0D, 1D, 170, null);
            return;
        }
        base.WndProc(ref message);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        currentWindowDpi = NativeMethods.GetWindowDpi(Handle);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        base.OnHandleDestroyed(e);
    }

    private void BeginWindowDrag(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(Handle, WmNcLButtonDown, new IntPtr(HtCaption), IntPtr.Zero);
    }

    private void UpdateWindowRegion()
    {
        if (WindowState == FormWindowState.Minimized || Width <= 0 || Height <= 0) return;
        using (GraphicsPath path = CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), 10))
        {
            Region previous = Region;
            Region = new Region(path);
            if (previous != null) previous.Dispose();
        }
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
    {
        int diameter = radius * 2;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void MinimizeButtonMouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || closing || minimizing) return;
        minimizing = true;
        BeginOpacityAnimation(Opacity, 0D, 140, delegate
        {
            WindowState = FormWindowState.Minimized;
            Opacity = 1D;
            minimizing = false;
        });
    }

    private void CloseButtonMouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || closing) return;
        closing = true;
        if (monitoring) ToggleMonitoring();
        StopClicker();
        windowAnimationTimer.Stop();
        Opacity = 1D;
        NativeMethods.AnimateWindow(Handle, 140, AwHide | AwBlend);
        Close();
    }

    private void BeginOpacityAnimation(double from, double to, int duration, Action completed)
    {
        windowAnimationTimer.Stop();
        animationFrom = Math.Max(0D, Math.Min(1D, from));
        animationTo = Math.Max(0D, Math.Min(1D, to));
        animationDuration = Math.Max(1, duration);
        animationCompleted = completed;
        animationStartedAt = Stopwatch.GetTimestamp();
        Opacity = animationFrom;
        windowAnimationTimer.Start();
    }

    private void WindowAnimationTick(object sender, EventArgs e)
    {
        double elapsed = (Stopwatch.GetTimestamp() - animationStartedAt) * 1000D / Stopwatch.Frequency;
        double progress = Math.Max(0D, Math.Min(1D, elapsed / animationDuration));
        double eased = progress * progress * (3D - 2D * progress);
        Opacity = animationFrom + ((animationTo - animationFrom) * eased);
        if (progress < 1D) return;
        windowAnimationTimer.Stop();
        Action completed = animationCompleted;
        animationCompleted = null;
        if (completed != null) completed();
    }

    private Label MakeLabel(string text, float size, FontStyle style, Color color)
    {
        Label label = new Label();
        label.Text = text;
        label.Font = new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
        label.ForeColor = color;
        label.BackColor = Color.Transparent;
        return label;
    }

    private void MonitorToggleButtonMouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        long now = Stopwatch.GetTimestamp();
        if ((now - Interlocked.Read(ref lastMonitorButtonToggleTicks)) / (double)Stopwatch.Frequency < 0.25D) return;
        Interlocked.Exchange(ref lastMonitorButtonToggleTicks, now);
        ToggleMonitoring();
    }

    private void StartClicker(int generation, long missing)
    {
        lock (clickerStateSync)
        {
            if (!IsCurrentScan(generation) || missing <= 0) return;
            // A hung UIA read cannot leave input running indefinitely.
            Interlocked.Exchange(ref inputLeaseUntil, Stopwatch.GetTimestamp() + Stopwatch.Frequency * 3);
            Interlocked.Exchange(ref remainingInputBudget, (int)Math.Min(missing, 150));
            if (clickerRunning) return;
            if (clickerWorker != null && clickerWorker.IsAlive) return;
            ManualResetEvent signal = new ManualResetEvent(false);
            clickerStopSignal = signal;
            clickerRunning = true;
            clickerWorker = new Thread(delegate() { ClickerLoop(signal); });
            clickerWorker.IsBackground = true;
            clickerWorker.Name = "F24 sender";
            clickerWorker.Start();
        }
    }

    private void StopClicker()
    {
        Thread worker;
        lock (clickerStateSync)
        {
            clickerRunning = false;
            Interlocked.Exchange(ref inputLeaseUntil, 0);
            Interlocked.Exchange(ref remainingInputBudget, 0);
            if (clickerStopSignal != null) clickerStopSignal.Set();
            worker = clickerWorker;
        }
        if (worker != null && worker != Thread.CurrentThread && worker.IsAlive)
            worker.Join(250);
        NativeMethods.SendF24KeyUp();
    }

    private void ClickerLoop(ManualResetEvent signal)
    {
        NativeMethods.TimeBeginPeriod(1);
        try
        {
            const double interval = 1.0 / FixedRate;
            Stopwatch clock = Stopwatch.StartNew();
            double next = clock.Elapsed.TotalSeconds;
            while (!signal.WaitOne(0))
            {
                if (Stopwatch.GetTimestamp() >= Interlocked.Read(ref inputLeaseUntil) ||
                    Interlocked.CompareExchange(ref remainingInputBudget, 0, 0) <= 0) break;
                double now = clock.Elapsed.TotalSeconds;
                if (now < next)
                {
                    signal.WaitOne(Math.Max(1, Math.Min(20, (int)Math.Ceiling((next - now) * 1000D))));
                    continue;
                }
                // Stop and send share the lock, so no down event can follow a stop's key-up.
                lock (clickerStateSync)
                {
                    if (signal.WaitOne(0) || !clickerRunning) break;
                    if (Interlocked.Decrement(ref remainingInputBudget) < 0) break;
                    if (NativeMethods.SendF24Press()) Interlocked.Increment(ref sentCount);
                }
                next = Math.Max(next + interval, clock.Elapsed.TotalSeconds);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("F24 worker stopped: " + ex);
        }
        finally
        {
            NativeMethods.SendF24KeyUp();
            NativeMethods.TimeEndPeriod(1);
            lock (clickerStateSync)
            {
                clickerRunning = false;
                if (object.ReferenceEquals(clickerStopSignal, signal))
                    clickerStopSignal = null;
                signal.Dispose();
            }
        }
    }

    private void UpdateClickerRate(object sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        long total = Interlocked.Read(ref sentCount);
        double seconds = (now - Interlocked.Read(ref lastDisplayTicks)) / (double)Stopwatch.Frequency;
        if (seconds <= 0D) return;
        long rate = clickerRunning ? (long)Math.Round((total - Interlocked.Read(ref lastDisplayedCount)) / seconds) : 0L;
        Interlocked.Exchange(ref measuredSendRate, rate);
        Interlocked.Exchange(ref lastDisplayedCount, total);
        Interlocked.Exchange(ref lastDisplayTicks, now);
        UpdateTelemetry();
    }

    private void ToggleMonitoring()
    {
        Interlocked.Increment(ref monitorGeneration);
        monitoring = !monitoring;
        StopClicker();
        controller.Reset();
        petProcessId = 0;
        observedPointBalance = targetPointBalance = -1;
        measuredSendRate = 0;
        toggleButton.Text = monitoring ? "停止自动挂机" : "开启自动挂机";
        toggleButton.FillColor = monitoring ? StopColor : AccentColor;
        toggleButton.TextColor = monitoring ? WindowColor : TextColor;
        toggleButton.Invalidate();
        SetSlotStatus(monitoring ? "检测中" : "未运行", monitoring ? "检测中" : "未运行");
        SetStatus(monitoring ? "正在启动" : "已停止",
            monitoring ? "正在读取桌宠点数与双槽状态" : "内部连点与宝箱领取均已关闭", MutedColor);
        UpdateTelemetry();
        if (monitoring)
        {
            scanTimer.Start();
            ScanTimer_Tick(null, EventArgs.Empty);
        }
        else scanTimer.Stop();
    }

    private bool IsCurrentScan(int generation)
    {
        return monitoring && generation == Volatile.Read(ref monitorGeneration) && !closing && !IsDisposed;
    }

    private sealed class PetReadResult
    {
        public int ProcessId;
        public IntPtr InputWindow;
        public ChestAccessibilitySnapshot Snapshot;
    }

    private sealed class PetReadJob
    {
        public int ProcessId;
        public int Generation;
        public double Started;
        public System.Threading.Tasks.Task<PetReadResult> Task;
    }

    private async System.Threading.Tasks.Task<PetReadResult> ReadPetWithDeadline(int generation)
    {
        // Process discovery does not call UIA. A new pet process can recover even if an old
        // provider is stuck. At most two outstanding UIA reads exist, including abandoned ones.
        int processId = await System.Threading.Tasks.Task.Run(delegate
        {
            using (Process process = FindPetProcess()) { return process == null ? 0 : process.Id; }
        });
        if (!IsCurrentScan(generation)) return null;
        for (int i = readJobs.Count - 1; i >= 0; i--)
        {
            PetReadJob old = readJobs[i];
            if (old.Task.IsCompleted && (old.ProcessId != processId || old.Generation != generation ||
                sessionClock.Elapsed.TotalSeconds - old.Started > 3.0))
            {
                if (old.Task.IsFaulted) Debug.WriteLine(old.Task.Exception);
                readJobs.RemoveAt(i);
            }
        }
        if (processId == 0) return new PetReadResult();
        PetReadJob job = readJobs.Find(delegate(PetReadJob item) { return item.ProcessId == processId; });
        if (job == null)
        {
            if (readJobs.Count >= 2) return null;
            job = new PetReadJob { ProcessId = processId, Generation = generation,
                Started = sessionClock.Elapsed.TotalSeconds,
                Task = System.Threading.Tasks.Task.Run((Func<PetReadResult>)ReadPet) };
            readJobs.Add(job);
        }
        if (await System.Threading.Tasks.Task.WhenAny(job.Task,
            System.Threading.Tasks.Task.Delay(2000)) != job.Task) return null;
        readJobs.Remove(job);
        if (job.Generation != generation || sessionClock.Elapsed.TotalSeconds - job.Started > 3.0)
        {
            if (job.Task.IsFaulted) Debug.WriteLine(job.Task.Exception);
            return null;
        }
        PetReadResult result = await job.Task;
        return result.ProcessId == processId ? result : null;
    }

    private static PetReadResult ReadPet()
    {
        using (Process process = FindPetProcess())
        {
            if (process == null) return new PetReadResult();
            IntPtr window = process.MainWindowHandle;
            IntPtr input = NativeMethods.FindDesktopCatInput((uint)process.Id);
            return new PetReadResult { ProcessId = process.Id, InputWindow = input,
                Snapshot = window == IntPtr.Zero || input == IntPtr.Zero
                    ? null : ChestAccessibilityReader.Read(window) };
        }
    }

    private async void ScanTimer_Tick(object sender, EventArgs e)
    {
        if (!monitoring || scanning || closing) return;
        int generation = Volatile.Read(ref monitorGeneration);
        scanning = true;
        try
        {
            PetReadResult read = await ReadPetWithDeadline(generation);
            if (!IsCurrentScan(generation)) return;
            if (read == null)
            {
                StopClicker();
                controller.Invalidate();
                observedPointBalance = targetPointBalance = -1;
                SetSlotStatus("读取中", "读取中");
                SetStatus("等待读取恢复", "桌宠响应超时，已暂停输入并等待恢复", WarningColor);
                UpdateTelemetry();
                return;
            }
            if (read.ProcessId != petProcessId)
            {
                StopClicker();
                controller.Reset();
                petProcessId = read.ProcessId;
            }
            if (read.Snapshot == null || !read.Snapshot.Available)
            {
                StopClicker();
                controller.Invalidate();
                observedPointBalance = targetPointBalance = -1;
                SetSlotStatus(read.ProcessId == 0 ? "未连接" : "读取中",
                    read.ProcessId == 0 ? "未连接" : "读取中");
                SetStatus("等待桌宠就绪", "请保持已登录，点数与槽位可读取后自动继续", WarningColor);
                UpdateTelemetry();
                return;
            }

            ChestAccessibilitySnapshot snapshot = read.Snapshot;
            observedPointBalance = snapshot.PointBalance;
            SetSlotStatus(snapshot.Upper.DisplayText, snapshot.Lower.DisplayText);
            PilotDecision plan = controller.Observe(snapshot, sessionClock.Elapsed.TotalSeconds);
            targetPointBalance = plan.Target;
            SetStatus(plan.State, plan.Detail, plan.Action == PilotAction.Fill ? WarningColor : SuccessColor);
            if (plan.Action == PilotAction.Fill)
                StartClicker(generation, plan.Target - snapshot.PointBalance);
            else
                StopClicker();
            UpdateTelemetry();

            if (plan.Action != PilotAction.Claim) return;
            Point cursor;
            if (NativeMethods.GetCursorPos(out cursor))
            {
                if (Math.Abs((double)cursor.X - lastCursorPoint.X) > 2 ||
                    Math.Abs((double)cursor.Y - lastCursorPoint.Y) > 2)
                    lastCursorMoveAt = sessionClock.Elapsed.TotalSeconds;
                lastCursorPoint = cursor;
            }
            if (sessionClock.Elapsed.TotalSeconds - lastCursorMoveAt < 0.8 || NativeMethods.IsMouseButtonDown())
            {
                SetStatus("宝箱已就绪", "鼠标正在使用，空闲后自动领取", WarningColor);
                return;
            }
            ChestCandidate candidate = plan.Candidate;
            if (!NativeMethods.BelongsToProcessAt(candidate.ScreenCenter, (uint)read.ProcessId, read.InputWindow))
            {
                controller.RecordClick(candidate.SlotIndex, snapshot.PointBalance,
                    sessionClock.Elapsed.TotalSeconds, false);
                SetStatus("等待宝箱可点击", "宝箱位置被遮挡或已移动，稍后重新检查", WarningColor);
                return;
            }

            // Refresh the exact slot before input, after stopping and draining the F24 worker.
            PetReadResult fresh = await ReadPetWithDeadline(generation);
            if (!IsCurrentScan(generation)) return;
            ChestCandidate latest = fresh != null && fresh.ProcessId == read.ProcessId && fresh.Snapshot != null &&
                fresh.Snapshot.Available && fresh.Snapshot.PointBalance >= 1000
                ? fresh.Snapshot.Slot(candidate.SlotIndex).ToCandidate(candidate.SlotIndex) : null;
            if (latest == null || latest.ScreenCenter != candidate.ScreenCenter)
            {
                controller.Invalidate();
                SetStatus("重新读取槽位", "宝箱或点数已变化，下一轮重新规划", WarningColor);
                return;
            }

            SetStatus("正在领取宝箱", "已停止连点，等待桌宠响应本次操作", WarningColor);
            bool sent = await System.Threading.Tasks.Task.Run(delegate
            {
                return NativeMethods.ClickAndRestore(fresh.InputWindow, latest.ScreenCenter,
                    (uint)fresh.ProcessId, delegate { return IsCurrentScan(generation); });
            });
            if (!IsCurrentScan(generation)) return;
            controller.RecordClick(candidate.SlotIndex, fresh.Snapshot.PointBalance,
                sessionClock.Elapsed.TotalSeconds, sent);
            SetStatus(sent ? "等待领取响应" : "点击已暂停",
                sent ? "下一轮将根据实际余额和槽位重新规划" : "输入未完成或鼠标已移动，稍后重新检查", WarningColor);
        }
        catch (Exception ex)
        {
            if (IsCurrentScan(generation))
            {
                StopClicker();
                controller.Invalidate();
                observedPointBalance = targetPointBalance = -1;
                SetStatus("等待状态恢复", "本地状态读取中断，稍后自动重试", ErrorColor);
                UpdateTelemetry();
            }
            Debug.WriteLine("AutoPilot scan failed: " + ex);
        }
        finally { scanning = false; }
    }

    private static Process FindPetProcess()
    {
        Process[] list = Process.GetProcessesByName("WuwaTappo");
        Process selected = null;
        try
        {
            foreach (Process process in list)
            {
                if (process.MainWindowHandle == IntPtr.Zero) continue;
                selected = process;
                break;
            }
        }
        finally
        {
            foreach (Process process in list)
                if (!object.ReferenceEquals(process, selected)) process.Dispose();
        }
        return selected;
    }



    private void UpdateTelemetry()
    {
        long points = Interlocked.Read(ref observedPointBalance);
        long target = Interlocked.Read(ref targetPointBalance);
        long rate = Interlocked.Read(ref measuredSendRate);
        string pointText = points < 0 ? "--" : points.ToString("N0");
        string targetText = target < 0 ? "--" : target.ToString("N0");
        SetSingleLineText(actualRateLabel,
            "点数：" + pointText + " · 双槽目标：" + targetText + " · 连点：" + rate.ToString("N0") + " 次/秒",
            9F, 7F);
    }



    private void SetStatus(string state, string detail, Color color)
    {
        clickerStatusLabel.ForeColor = color;
        SetSingleLineText(clickerStatusLabel, state, 12F, 9F);
        lastActionLabel.ForeColor = color;
        SetSingleLineText(lastActionLabel, detail, 8.5F, 7F);
    }



    private void SetSlotStatus(string upper, string lower)
    {
        SetSingleLineText(stateLabel, "上宝箱：" + upper, 10F, 8.5F);
        SetSingleLineText(detailLabel, "下宝箱：" + lower, 10F, 8.5F);
        stateLabel.ForeColor = SlotStatusColor(upper);
        detailLabel.ForeColor = SlotStatusColor(lower);
    }

    private static Color SlotStatusColor(string value)
    {
        if (value == "宝箱") return SuccessColor;
        if (value == "空") return MutedColor;
        if (value == "未连接" || value == "未监控" || value == "未运行") return MutedColor;
        return WarningColor;
    }

    private void SetSingleLineText(Label label, string text, float preferredSize, float minimumSize)
    {
        preferredSize *= currentUiScale;
        minimumSize *= currentUiScale;
        float selectedSize = preferredSize;
        using (Graphics graphics = label.CreateGraphics())
        {
            while (selectedSize > minimumSize)
            {
                using (Font testFont = new Font("Microsoft YaHei UI", selectedSize, label.Font.Style, GraphicsUnit.Point))
                {
                    Size measured = TextRenderer.MeasureText(graphics, text, testFont,
                        new Size(int.MaxValue, label.Height),
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    if (measured.Width <= label.ClientSize.Width) break;
                }
                selectedSize -= 0.25F;
            }
        }

        label.Text = text;
        if (Math.Abs(label.Font.Size - selectedSize) > 0.05F)
        {
            Font previous = label.Font;
            label.Font = new Font("Microsoft YaHei UI", selectedSize, previous.Style, GraphicsUnit.Point);
            previous.Dispose();
        }
    }

    private static void ScaleControlFonts(Control parent, float factor)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is Label || control is Button)
            {
                Font source = control.Font;
                float size = Math.Max(5F, source.Size * factor);
                control.Font = new Font(source.FontFamily, size, source.Style, source.Unit,
                    source.GdiCharSet, source.GdiVerticalFont);
                if (control.Parent == null || !object.ReferenceEquals(source, control.Parent.Font))
                    source.Dispose();
            }
            if (control.HasChildren) ScaleControlFonts(control, factor);
        }
    }



    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        monitoring = false;
        Interlocked.Increment(ref monitorGeneration);
        scanTimer.Stop();
        StopClicker();
        Thread worker;
        lock (clickerStateSync)
            worker = clickerWorker;
        if (worker != null && worker != Thread.CurrentThread && worker.IsAlive)
            worker.Join(1000);
        scanTimer.Dispose();
        clickerUiTimer.Stop();
        clickerUiTimer.Dispose();
        windowAnimationTimer.Stop();
        windowAnimationTimer.Dispose();
        base.OnFormClosed(e);
    }
}

internal sealed class WindowChromeButton : Button
{
    private bool hovered;
    private bool pressed;
    public Color HoverColor { get; set; }

    public WindowChromeButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        TabStop = false;
        Cursor = Cursors.Hand;
        Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular);
        BackColor = Color.Transparent;
        HoverColor = Color.FromArgb(241, 222, 190);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Color normal = Parent == null ? Color.FromArgb(255, 248, 232) : Parent.BackColor;
        Color fill = hovered ? HoverColor : normal;
        if (pressed) fill = Color.FromArgb(fill.R * 3 / 4, fill.G * 3 / 4, fill.B * 3 / 4);
        using (SolidBrush brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, ClientRectangle);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

internal sealed class RoundedPanel : Panel
{
    public int CornerRadius { get; set; }
    public RoundedPanel()
    {
        CornerRadius = 10;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); UpdateRegion(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath path = RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius))
        using (SolidBrush brush = new SolidBrush(BackColor))
        using (Pen border = new Pen(Color.FromArgb(72, 141, 98, 69), 1F))
        {
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(border, path);
        }
        base.OnPaint(e);
    }
    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        using (GraphicsPath path = RoundedRectangle(new Rectangle(0, 0, Width, Height), CornerRadius))
        {
            Region previous = Region;
            Region = new Region(path);
            if (previous != null) previous.Dispose();
        }
    }
    internal static GraphicsPath RoundedRectangle(Rectangle rect, int radius)
    {
        int d = Math.Max(2, radius * 2);
        GraphicsPath path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ModernButton : Button
{
    private bool hovered;
    private bool pressed;
    public Color FillColor { get; set; }
    public Color TextColor { get; set; }
    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);
        Rectangle rect = pressed ? new Rectangle(2, 3, Width - 5, Height - 5) : new Rectangle(0, 0, Width - 1, Height - 2);
        Color fill = pressed ? Darken(FillColor, .80F) : (hovered ? Lighten(FillColor, .12F) : FillColor);
        using (GraphicsPath path = RoundedPanel.RoundedRectangle(rect, 10))
        using (SolidBrush brush = new SolidBrush(fill))
        using (Pen border = new Pen(Color.FromArgb(hovered ? 180 : 110, 196, 145, 82), hovered ? 1.5F : 1F))
        {
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(border, path);
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, rect, TextColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
    private static Color Lighten(Color c, float a) { return Color.FromArgb(c.A, c.R + (int)((255 - c.R) * a), c.G + (int)((255 - c.G) * a), c.B + (int)((255 - c.B) * a)); }
    private static Color Darken(Color c, float f) { return Color.FromArgb(c.A, (int)(c.R * f), (int)(c.G * f), (int)(c.B * f)); }
}


internal static class NativeMethods
{
    internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public INPUTUNION U;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    internal static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    internal static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hwnd, ref Point point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint count, INPUT[] inputs, int size);

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint NativeTimeBeginPeriod(uint period);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint NativeTimeEndPeriod(uint period);

    [DllImport("user32.dll")]
    internal static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    internal static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool AnimateWindow(IntPtr hwnd, uint time, uint flags);

    internal static void EnablePerMonitorDpiAwareness()
    {
        try
        {
            if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return;
        }
        catch (EntryPointNotFoundException) { }
        try { SetProcessDPIAware(); }
        catch { }
    }

    internal static int GetWindowDpi(IntPtr hwnd)
    {
        try
        {
            uint dpi = GetDpiForWindow(hwnd);
            if (dpi > 0) return (int)dpi;
        }
        catch (EntryPointNotFoundException) { }
        catch (DllNotFoundException) { }
        try
        {
            using (Graphics graphics = Graphics.FromHwnd(hwnd))
                return Math.Max(96, (int)Math.Round(graphics.DpiX));
        }
        catch { return 96; }
    }

    internal static IntPtr FindDesktopCatInput(uint processId)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr hwnd, IntPtr unused)
        {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid == processId && GetWindowClass(hwnd) == "DesktopCatInput")
            {
                found = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    internal static bool BelongsToProcessAt(Point screenPoint, uint processId, IntPtr expectedInputWindow)
    {
        IntPtr hwnd = WindowFromPoint(screenPoint);
        if (hwnd == IntPtr.Zero) return false;
        uint pid;
        GetWindowThreadProcessId(hwnd, out pid);
        if (pid != processId) return false;
        return hwnd == expectedInputWindow || GetWindowClass(hwnd) == "DesktopCatInput";
    }

    internal static bool IsMouseButtonDown()
    {
        return (GetAsyncKeyState(0x01) & 0x8000) != 0 ||
               (GetAsyncKeyState(0x02) & 0x8000) != 0 ||
               (GetAsyncKeyState(0x04) & 0x8000) != 0;
    }

    internal static bool ClickAndRestore(IntPtr inputWindow, Point target, uint processId, Func<bool> canContinue)
    {
        if (!canContinue() || IsMouseButtonDown()) return false;
        Point original;
        if (!GetCursorPos(out original)) return false;
        if (!SetCursorPos(target.X, target.Y)) return false;
        Thread.Sleep(80);

        if (!canContinue() || IsMouseButtonDown() || !IsCursorNear(target, 8) || !BelongsToProcessAt(target, processId, inputWindow))
        {
            RestoreCursorIfUntouched(original, target);
            return false;
        }

        Point clientPoint = target;
        if (!ScreenToClient(inputWindow, ref clientPoint))
        {
            RestoreCursorIfUntouched(original, target);
            return false;
        }
        int packedCoordinates = unchecked((clientPoint.Y << 16) | (clientPoint.X & 0xffff));
        if (!PostMessage(inputWindow, 0x0200, IntPtr.Zero, new IntPtr(packedCoordinates)))
        {
            RestoreCursorIfUntouched(original, target);
            return false;
        }
        Thread.Sleep(120);

        if (!canContinue() || IsMouseButtonDown() || !IsCursorNear(target, 8) || !BelongsToProcessAt(target, processId, inputWindow))
        {
            RestoreCursorIfUntouched(original, target);
            return false;
        }

        INPUT[] inputs = new INPUT[2];
        inputs[0].type = 0;
        inputs[0].U.mi.dwFlags = 0x0002;
        inputs[1].type = 0;
        inputs[1].U.mi.dwFlags = 0x0004;
        uint sentDown = SendInput(1, new INPUT[] { inputs[0] }, Marshal.SizeOf(typeof(INPUT)));
        Thread.Sleep(65);
        uint sentUp = SendInput(1, new INPUT[] { inputs[1] }, Marshal.SizeOf(typeof(INPUT)));
        Thread.Sleep(180);
        RestoreCursorIfUntouched(original, target);
        return sentDown == 1 && sentUp == 1;
    }

    private static void RestoreCursorIfUntouched(Point original, Point automaticTarget)
    {
        Point current;
        if (!GetCursorPos(out current)) return;
        int dx = current.X - automaticTarget.X;
        int dy = current.Y - automaticTarget.Y;
        if (dx * dx + dy * dy <= 64)
            SetCursorPos(original.X, original.Y);
    }

    private static bool IsCursorNear(Point target, int radius)
    {
        Point current;
        if (!GetCursorPos(out current)) return false;
        int dx = current.X - target.X;
        int dy = current.Y - target.Y;
        return dx * dx + dy * dy <= radius * radius;
    }

    internal static bool SendF24Press()
    {
        INPUT down = new INPUT();
        down.type = 1;
        down.U.ki.wVk = 0x87;
        INPUT up = new INPUT();
        up.type = 1;
        up.U.ki.wVk = 0x87;
        up.U.ki.dwFlags = 0x0002;
        return SendInput(2, new INPUT[] { down, up }, Marshal.SizeOf(typeof(INPUT))) == 2;
    }

    internal static void SendF24KeyUp()
    {
        INPUT up = new INPUT();
        up.type = 1;
        up.U.ki.wVk = 0x87;
        up.U.ki.dwFlags = 0x0002;
        SendInput(1, new INPUT[] { up }, Marshal.SizeOf(typeof(INPUT)));
    }

    internal static void TimeBeginPeriod(uint period) { NativeTimeBeginPeriod(period); }
    internal static void TimeEndPeriod(uint period) { NativeTimeEndPeriod(period); }

    private static string GetWindowClass(IntPtr hwnd)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder(128);
        GetClassName(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }
}
