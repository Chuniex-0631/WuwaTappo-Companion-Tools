using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// Runs the actual AutoPilot F24 worker with an in-memory input boundary.
// No pet discovery, account access, UIA reads or real keyboard input occur.
internal static class AutoPilotWorkerTests
{
    private static int assertions;

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        try
        {
            BudgetStopsWorker();
            ExpiredLeaseStopsWorker();
            FailedSendStopsWorker();
            StaleGenerationCannotStart();
            StopDrainsWorker();
            Console.WriteLine("PASS: AutoPilot worker; 5 scenarios; " + assertions + " assertions; no real input sent.");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.Exit(1); }
    }

    private static FieldInfo Field(string name)
    {
        return typeof(MonitorForm).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
    }

    private static object Call(MonitorForm form, string name, params object[] args)
    {
        return typeof(MonitorForm).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, args);
    }

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    private static void RunWorker(MonitorForm form, int budget, long lease)
    {
        ManualResetEvent signal = new ManualResetEvent(false);
        Field("clickerRunning").SetValue(form, true);
        Field("clickerStopSignal").SetValue(form, signal);
        Field("remainingInputBudget").SetValue(form, budget);
        Field("inputLeaseUntil").SetValue(form, lease);
        Exception failure = null;
        Thread worker = new Thread(delegate()
        {
            try { Call(form, "ClickerLoop", signal); }
            catch (Exception ex) { failure = ex; }
        });
        worker.IsBackground = true;
        Field("clickerWorker").SetValue(form, worker);
        worker.Start();
        Check(worker.Join(2000), "worker must finish within its bounded budget");
        if (failure != null) throw failure;
        Check(!(bool)Field("clickerRunning").GetValue(form), "worker clears running state");
        Check(Field("clickerStopSignal").GetValue(form) == null, "worker releases its stop signal");
    }

    private static void BudgetStopsWorker()
    {
        int presses = 0, releases = 0;
        NativeMethods.InputSender = delegate(uint count, NativeMethods.INPUT[] inputs, int size)
        {
            if (count == 2) Interlocked.Increment(ref presses);
            else Interlocked.Increment(ref releases);
            return count;
        };
        using (MonitorForm form = new MonitorForm())
        {
            RunWorker(form, 5, Stopwatch.GetTimestamp() + Stopwatch.Frequency * 3);
            Check(presses == 5, "five missing points permit exactly five balanced presses");
            Check((long)Field("sentCount").GetValue(form) == 5, "telemetry counts only completed presses");
            Check(releases >= 1, "worker finishes with key-up cleanup");
        }
    }

    private static void ExpiredLeaseStopsWorker()
    {
        int presses = 0;
        NativeMethods.InputSender = delegate(uint count, NativeMethods.INPUT[] inputs, int size)
        { if (count == 2) Interlocked.Increment(ref presses); return count; };
        using (MonitorForm form = new MonitorForm())
        {
            RunWorker(form, 150, Stopwatch.GetTimestamp() - 1);
            Check(presses == 0, "expired read lease must not send a down event");
        }
    }

    private static void FailedSendStopsWorker()
    {
        foreach (uint accepted in new uint[] { 0, 1 })
        {
            int presses = 0;
            NativeMethods.InputSender = delegate(uint count, NativeMethods.INPUT[] inputs, int size)
            {
                if (count == 2) { Interlocked.Increment(ref presses); return accepted; }
                return count;
            };
            using (MonitorForm form = new MonitorForm())
            {
                RunWorker(form, 150, Stopwatch.GetTimestamp() + Stopwatch.Frequency * 3);
                Check(presses == 1, "failed or partial delivery must end this worker attempt");
                Check((long)Field("sentCount").GetValue(form) == 0, "failed delivery cannot inflate telemetry");
            }
        }
    }

    private static void StaleGenerationCannotStart()
    {
        NativeMethods.InputSender = delegate(uint count, NativeMethods.INPUT[] inputs, int size) { return count; };
        using (MonitorForm form = new MonitorForm())
        {
            Field("monitoring").SetValue(form, true);
            Field("monitorGeneration").SetValue(form, 2);
            Call(form, "StartClicker", 1, 100L);
            Check(Field("clickerWorker").GetValue(form) == null, "stale observation cannot create a worker");
            Field("monitoring").SetValue(form, false);
        }
    }

    private static void StopDrainsWorker()
    {
        int presses = 0;
        using (ManualResetEvent firstPress = new ManualResetEvent(false))
        using (MonitorForm form = new MonitorForm())
        {
            NativeMethods.InputSender = delegate(uint count, NativeMethods.INPUT[] inputs, int size)
            {
                if (count == 2) { Interlocked.Increment(ref presses); firstPress.Set(); }
                return count;
            };
            Field("monitoring").SetValue(form, true);
            Field("monitorGeneration").SetValue(form, 2);
            Call(form, "StartClicker", 2, 150L);
            Check(firstPress.WaitOne(2000), "current observation starts the actual worker");
            Call(form, "StopClicker");
            int afterStop = Volatile.Read(ref presses);
            Thread.Sleep(80);
            Check(Volatile.Read(ref presses) == afterStop, "no down event follows returned StopClicker");
            Thread worker = (Thread)Field("clickerWorker").GetValue(form);
            Check(worker != null && !worker.IsAlive, "StopClicker drains its input worker");
            Field("monitoring").SetValue(form, false);
        }
    }
}
