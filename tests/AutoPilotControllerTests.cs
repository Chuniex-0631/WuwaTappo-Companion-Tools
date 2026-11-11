using System;
using System.Drawing;

// Offline traces only. No UI Automation, SendInput, accounts or network are touched.
internal static class AutoPilotControllerTests
{
    private static int assertions;
    private static int scenarios;

    private static void Main()
    {
        Run("First chest at 1000, debit hides both slots", FirstChestHiddenAfterDebit);
        Run("Two close chests reconcile independently", DualChestCycle);
        Run("Optimistic countdown resumes observation without a success count", OptimisticCountdown);
        Run("Persistent chest times out and backs off", TimeoutAndRetry);
        Run("Failed upper click cannot starve lower chest", OtherSlotDuringBackoff);
        Run("Unavailable, disabled and missing balance pause all actions", UnavailableAndBusy);
        Run("Stalled point count pauses and retries", StalledCounter);
        Run("Slot and position stability resets correctly", StabilityAndReset);
        Run("Transient hidden state requires consecutive observations", TransientHiddenState);
        Run("Pending transaction cannot be proven by other slot", OtherSlotDoesNotCompletePending);
        Run("Invalidated reading resets consecutive observations", InvalidateResetsStability);
        Run("Exhaustive fresh-state action matrix", FreshStateMatrix);
        Run("Manual monitoring never fills any state combination", ManualModeMatrix);
        Run("Manual hidden-after-debit returns to monitoring", ManualHiddenCycle);
        Run("Manual dual-chest cycle requires no refill", ManualDualCycle);
        Run("Manual disabled, unavailable and backoff stay safe", ManualBusyAndBackoff);
        Console.WriteLine("PASS: " + scenarios + " scenarios; " + assertions + " assertions.");
    }

    private static void Run(string name, Action scenario)
    {
        try { scenario(); scenarios++; Console.WriteLine("PASS: " + name); }
        catch (Exception ex) { Console.Error.WriteLine("FAIL: " + name + ": " + ex.Message); Environment.Exit(1); }
    }

    private static ChestAccessibilitySnapshot Snapshot(long points, ChestSlotState upper, ChestSlotState lower)
    {
        ChestAccessibilitySnapshot result = new ChestAccessibilitySnapshot();
        result.Available = true;
        result.PointBalance = points;
        result.Upper.State = upper;
        result.Upper.ScreenBounds = new Rectangle(300, 100, 62, 44);
        result.Lower.State = lower;
        result.Lower.ScreenBounds = new Rectangle(300, 148, 62, 44);
        return result;
    }

    private static void Expect(PilotAction expected, PilotDecision actual, string message)
    {
        assertions++;
        if (actual.Action != expected) throw new Exception(message + ": expected " + expected + ", got " + actual.Action);
        assertions++;
        if (actual.Target != 2000) throw new Exception(message + ": reserve drifted from fixed dual-slot 2000");
    }

    private static void ExpectSlot(int expected, PilotDecision actual, string message)
    {
        Expect(PilotAction.Claim, actual, message);
        assertions++;
        if (actual.Candidate == null || actual.Candidate.SlotIndex != expected)
            throw new Exception(message + ": wrong target slot");
    }

    private static void FirstChestHiddenAfterDebit()
    {
        AutoPilotController c = new AutoPilotController();
        Expect(PilotAction.Fill, c.Observe(Snapshot(0, ChestSlotState.Empty, ChestSlotState.Empty), 0), "initial zero");
        Expect(PilotAction.Fill, c.Observe(Snapshot(980, ChestSlotState.Empty, ChestSlotState.Empty), 10), "near threshold");
        ChestAccessibilitySnapshot ready = Snapshot(1000, ChestSlotState.Ready, ChestSlotState.Empty);
        Expect(PilotAction.Wait, c.Observe(ready, 11), "first ready observation stops fill");
        ExpectSlot(0, c.Observe(ready, 12), "stable ready observation");
        c.RecordClick(0, 1000, 12, true);
        ChestAccessibilitySnapshot hidden = Snapshot(0, ChestSlotState.Empty, ChestSlotState.Empty);
        Expect(PilotAction.Wait, c.Observe(hidden, 13), "animation settle");
        Expect(PilotAction.Wait, c.Observe(hidden, 14), "first hidden reading");
        Expect(PilotAction.Wait, c.Observe(hidden, 15), "stable hidden reconciles transaction");
        Expect(PilotAction.Fill, c.Observe(hidden, 16), "resume replenishing without waiting for invisible countdown");
        Expect(PilotAction.Wait, c.Observe(Snapshot(2000, ChestSlotState.Countdown, ChestSlotState.Countdown), 20), "reserve reached");
    }

    private static void DualChestCycle()
    {
        AutoPilotController c = new AutoPilotController();
        ChestAccessibilitySnapshot both = Snapshot(2000, ChestSlotState.Ready, ChestSlotState.Ready);
        Expect(PilotAction.Wait, c.Observe(both, 0), "stabilize upper");
        ExpectSlot(0, c.Observe(both, 1), "upper selected first");
        c.RecordClick(0, 2000, 1, true);
        ChestAccessibilitySnapshot lower = Snapshot(1000, ChestSlotState.Empty, ChestSlotState.Ready);
        Expect(PilotAction.Wait, c.Observe(lower, 3), "upper hidden after debit");
        Expect(PilotAction.Wait, c.Observe(lower, 4), "upper hidden confirmed");
        Expect(PilotAction.Wait, c.Observe(lower, 5), "lower stabilizes");
        ExpectSlot(1, c.Observe(lower, 6), "remaining reserve serves lower");
        c.RecordClick(1, 1000, 6, true);
        ChestAccessibilitySnapshot empty = Snapshot(0, ChestSlotState.Empty, ChestSlotState.Empty);
        Expect(PilotAction.Wait, c.Observe(empty, 8), "lower hidden");
        Expect(PilotAction.Wait, c.Observe(empty, 9), "lower reconciliation");
        Expect(PilotAction.Fill, c.Observe(empty, 10), "refill after both claims");
        Expect(PilotAction.Fill, c.Observe(Snapshot(1200, ChestSlotState.Empty, ChestSlotState.Countdown), 20), "partly hidden cooldown reserve");
        Expect(PilotAction.Wait, c.Observe(Snapshot(2000, ChestSlotState.Countdown, ChestSlotState.Countdown), 30), "both future chests covered");
    }

    private static void OptimisticCountdown()
    {
        AutoPilotController c = new AutoPilotController();
        c.RecordClick(0, 2500, 0, true);
        ChestAccessibilitySnapshot predicted = Snapshot(2500, ChestSlotState.Countdown, ChestSlotState.Countdown);
        Expect(PilotAction.Wait, c.Observe(predicted, 2), "predicted countdown sample one");
        Expect(PilotAction.Wait, c.Observe(predicted, 3), "predicted countdown sample two");
        Expect(PilotAction.Wait, c.Observe(predicted, 4), "sufficient reserve waits");
        ChestAccessibilitySnapshot rollback = Snapshot(2500, ChestSlotState.Ready, ChestSlotState.Countdown);
        Expect(PilotAction.Wait, c.Observe(rollback, 5), "rollback must stabilize");
        ExpectSlot(0, c.Observe(rollback, 6), "rollback can retry through normal path");
        assertions++;
        foreach (System.Reflection.FieldInfo f in typeof(AutoPilotController).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            if (f.Name.IndexOf("successful", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new Exception("successful claim statistics must not be retained");
    }

    private static void TimeoutAndRetry()
    {
        AutoPilotController c = new AutoPilotController();
        ChestAccessibilitySnapshot ready = Snapshot(1500, ChestSlotState.Ready, ChestSlotState.Empty);
        c.RecordClick(0, 1500, 0, true);
        for (int t = 1; t <= 12; t++)
            Expect(PilotAction.Wait, c.Observe(ready, t), "never repeat before response timeout " + t);
        Expect(PilotAction.Wait, c.Observe(ready, 13), "retry backoff");
        Expect(PilotAction.Wait, c.Observe(ready, 14), "retry backoff continues");
        Expect(PilotAction.Wait, c.Observe(ready, 15), "new stability after backoff");
        ExpectSlot(0, c.Observe(ready, 16), "normal retry after timeout");
        c.RecordClick(0, 1500, 16, true);
        Expect(PilotAction.Wait, c.Observe(ready, 28), "second timeout");
        Expect(PilotAction.Wait, c.Observe(ready, 33), "second backoff doubled");
        Expect(PilotAction.Wait, c.Observe(ready, 34), "restabilize after doubled backoff");
        ExpectSlot(0, c.Observe(ready, 35), "second retry");
    }

    private static void OtherSlotDuringBackoff()
    {
        AutoPilotController c = new AutoPilotController();
        ChestAccessibilitySnapshot both = Snapshot(2000, ChestSlotState.Ready, ChestSlotState.Ready);
        c.RecordClick(0, 2000, 0, false);
        Expect(PilotAction.Wait, c.Observe(both, 0.5), "lower stabilizes during upper backoff");
        ExpectSlot(1, c.Observe(both, 1.5), "lower is not starved");
    }

    private static void UnavailableAndBusy()
    {
        AutoPilotController c = new AutoPilotController();
        ChestAccessibilitySnapshot unknown = Snapshot(0, ChestSlotState.Empty, ChestSlotState.Empty);
        unknown.Available = false;
        Expect(PilotAction.Wait, c.Observe(unknown, 0), "unavailable snapshot");
        unknown.Available = true;
        unknown.PointBalance = -1;
        Expect(PilotAction.Wait, c.Observe(unknown, 1), "unreadable balance");
        Expect(PilotAction.Wait, c.Observe(Snapshot(0, ChestSlotState.Busy, ChestSlotState.Empty), 2), "busy low balance");
        Expect(PilotAction.Wait, c.Observe(Snapshot(2000, ChestSlotState.Ready, ChestSlotState.Busy), 3), "busy blocks competing claim");
        Expect(PilotAction.Wait, c.Observe(Snapshot(2000, ChestSlotState.Empty, ChestSlotState.Empty), 4), "sufficient balance plus hidden slots never fills");
    }

    private static void StalledCounter()
    {
        AutoPilotController c = new AutoPilotController();
        ChestAccessibilitySnapshot low = Snapshot(50, ChestSlotState.Empty, ChestSlotState.Empty);
        Expect(PilotAction.Fill, c.Observe(low, 0), "start fill");
        Expect(PilotAction.Fill, c.Observe(low, 11), "within progress deadline");
        Expect(PilotAction.Wait, c.Observe(low, 12), "stalled deadline");
        Expect(PilotAction.Wait, c.Observe(low, 41), "pause until retry");
        Expect(PilotAction.Fill, c.Observe(low, 42), "retry after pause");
        Expect(PilotAction.Wait, c.Observe(low, 54), "second stalled deadline");
        Expect(PilotAction.Wait, c.Observe(Snapshot(100, ChestSlotState.Empty, ChestSlotState.Empty), 55), "new progress still respects bounded pause");
        Expect(PilotAction.Fill, c.Observe(Snapshot(100, ChestSlotState.Empty, ChestSlotState.Empty), 84), "resume after subsequent pause");
    }

    private static void StabilityAndReset()
    {
        AutoPilotController c = new AutoPilotController();
        ChestAccessibilitySnapshot upper = Snapshot(1000, ChestSlotState.Ready, ChestSlotState.Empty);
        Expect(PilotAction.Wait, c.Observe(upper, 0), "first position");
        upper.Upper.ScreenBounds = new Rectangle(400, 100, 62, 44);
        Expect(PilotAction.Wait, c.Observe(upper, 1), "moved position needs stabilization");
        ExpectSlot(0, c.Observe(upper, 2), "moved position stabilizes");
        ChestAccessibilitySnapshot lower = Snapshot(1000, ChestSlotState.Empty, ChestSlotState.Ready);
        Expect(PilotAction.Wait, c.Observe(lower, 3), "switching slot invalidates stability");
        ExpectSlot(1, c.Observe(lower, 4), "new slot stable");
        c.RecordClick(1, 1000, 4, true);
        c.Reset();
        Expect(PilotAction.Fill, c.Observe(Snapshot(0, ChestSlotState.Empty, ChestSlotState.Empty), 5), "reset discards pending transaction");
        Expect(PilotAction.Wait, c.Observe(lower, 6), "reset clears previous stability");
        ExpectSlot(1, c.Observe(lower, 7), "new session can claim");
    }

    private static void TransientHiddenState()
    {
        AutoPilotController c = new AutoPilotController();
        c.RecordClick(0, 1500, 0, true);
        ChestAccessibilitySnapshot hidden = Snapshot(500, ChestSlotState.Empty, ChestSlotState.Empty);
        Expect(PilotAction.Wait, c.Observe(hidden, 2), "hidden first sample");
        Expect(PilotAction.Wait, c.Observe(Snapshot(1500, ChestSlotState.Ready, ChestSlotState.Empty), 3), "ready returns");
        Expect(PilotAction.Wait, c.Observe(hidden, 4), "hidden restarts consecutive confirmation");
        Expect(PilotAction.Wait, c.Observe(hidden, 4.2), "too soon to confirm");
        Expect(PilotAction.Wait, c.Observe(hidden, 5), "hidden eventually reconciles");
        Expect(PilotAction.Fill, c.Observe(hidden, 6), "resumes after stable hidden transition");
    }

    private static void OtherSlotDoesNotCompletePending()
    {
        AutoPilotController c = new AutoPilotController();
        c.RecordClick(0, 2500, 0, true);
        ChestAccessibilitySnapshot wrong = Snapshot(1500, ChestSlotState.Ready, ChestSlotState.Countdown);
        for (int t = 2; t < 12; t++)
            Expect(PilotAction.Wait, c.Observe(wrong, t), "other slot countdown cannot complete pending " + t);
        Expect(PilotAction.Wait, c.Observe(wrong, 12), "original still ready times out");
        Expect(PilotAction.Wait, c.Observe(wrong, 13), "backoff after original remained ready");
    }

    private static void InvalidateResetsStability()
    {
        AutoPilotController c = new AutoPilotController();
        ChestAccessibilitySnapshot ready = Snapshot(1000, ChestSlotState.Ready, ChestSlotState.Empty);
        Expect(PilotAction.Wait, c.Observe(ready, 0), "first sample");
        c.Invalidate();
        Expect(PilotAction.Wait, c.Observe(ready, 1), "invalid reading cannot preserve stability");
        ExpectSlot(0, c.Observe(ready, 2), "stability restored");
        c.RecordClick(0, 1000, 2, true);
        ChestAccessibilitySnapshot hidden = Snapshot(0, ChestSlotState.Empty, ChestSlotState.Empty);
        Expect(PilotAction.Wait, c.Observe(hidden, 4), "pending hidden first sample");
        c.Invalidate();
        Expect(PilotAction.Wait, c.Observe(hidden, 5), "pending hidden confirmation restarts after invalid read");
        Expect(PilotAction.Wait, c.Observe(hidden, 6), "pending hidden can reconcile");
        Expect(PilotAction.Fill, c.Observe(hidden, 7), "invalid read does not deadlock transaction");
    }

    private static void FreshStateMatrix()
    {
        ChestSlotState[] states = { ChestSlotState.Empty, ChestSlotState.Countdown, ChestSlotState.Ready, ChestSlotState.Busy };
        long[] balances = { -1, 0, 99, 999, 1000, 1500, 1999, 2000, 3000, 728923 };
        foreach (long balance in balances)
        foreach (ChestSlotState upper in states)
        foreach (ChestSlotState lower in states)
        {
            AutoPilotController c = new AutoPilotController();
            ChestAccessibilitySnapshot s = Snapshot(balance, upper, lower);
            PilotDecision d = c.Observe(s, 0);
            string label = balance + ":" + upper + "/" + lower;
            if (balance < 0 || upper == ChestSlotState.Busy || lower == ChestSlotState.Busy)
                Expect(PilotAction.Wait, d, label + " input must pause");
            else if (balance >= 1000 && (upper == ChestSlotState.Ready || lower == ChestSlotState.Ready))
            {
                Expect(PilotAction.Wait, d, label + " stabilize first");
                ExpectSlot(upper == ChestSlotState.Ready ? 0 : 1, c.Observe(s, 1), label + " stable claim");
            }
            else if (balance < 2000)
                Expect(PilotAction.Fill, d, label + " bounded reserve");
            else
                Expect(PilotAction.Wait, d, label + " sufficient reserve");
        }
    }

    private static void ManualModeMatrix()
    {
        ChestSlotState[] states = { ChestSlotState.Empty, ChestSlotState.Countdown, ChestSlotState.Ready, ChestSlotState.Busy };
        long[] balances = { -1, 0, 99, 999, 1000, 1500, 1999, 2000, 3000, 728923 };
        foreach (long balance in balances)
        foreach (ChestSlotState upper in states)
        foreach (ChestSlotState lower in states)
        {
            AutoPilotController c = new AutoPilotController(false);
            ChestAccessibilitySnapshot s = Snapshot(balance, upper, lower);
            string label = "manual " + balance + ":" + upper + "/" + lower;
            Expect(PilotAction.Wait, c.Observe(s, 0), label + " first observation never fills");
            bool canClaim = balance >= 1000 && upper != ChestSlotState.Busy && lower != ChestSlotState.Busy &&
                (upper == ChestSlotState.Ready || lower == ChestSlotState.Ready);
            if (canClaim)
                ExpectSlot(upper == ChestSlotState.Ready ? 0 : 1, c.Observe(s, 1), label + " ready can be claimed");
            else
                Expect(PilotAction.Wait, c.Observe(s, 60), label + " prolonged monitoring never fills");
        }
    }

    private static void ManualHiddenCycle()
    {
        AutoPilotController c = new AutoPilotController(false);
        Expect(PilotAction.Wait, c.Observe(Snapshot(0, ChestSlotState.Empty, ChestSlotState.Empty), 0), "manual zero waits for user input");
        ChestAccessibilitySnapshot ready = Snapshot(1000, ChestSlotState.Ready, ChestSlotState.Empty);
        Expect(PilotAction.Wait, c.Observe(ready, 1), "manual threshold stabilizes");
        ExpectSlot(0, c.Observe(ready, 2), "manual threshold can claim");
        c.RecordClick(0, 1000, 2, true);
        ChestAccessibilitySnapshot hidden = Snapshot(0, ChestSlotState.Empty, ChestSlotState.Empty);
        Expect(PilotAction.Wait, c.Observe(hidden, 3), "manual settle");
        Expect(PilotAction.Wait, c.Observe(hidden, 4), "manual first hidden observation");
        Expect(PilotAction.Wait, c.Observe(hidden, 5), "manual hidden reconciled");
        PilotDecision wait = c.Observe(hidden, 6);
        Expect(PilotAction.Wait, wait, "manual must not refill hidden slots");
        assertions++;
        if (wait.State != "监控中") throw new Exception("manual hidden transaction did not finish");
        Expect(PilotAction.Wait, c.Observe(Snapshot(999, ChestSlotState.Empty, ChestSlotState.Empty), 60), "manual user progress below threshold waits");
        Expect(PilotAction.Wait, c.Observe(ready, 61), "manual fresh visible chest stabilizes");
        ExpectSlot(0, c.Observe(ready, 62), "manual can run another cycle after user replenishment");
    }

    private static void ManualDualCycle()
    {
        AutoPilotController c = new AutoPilotController(false);
        ChestAccessibilitySnapshot both = Snapshot(2000, ChestSlotState.Ready, ChestSlotState.Ready);
        Expect(PilotAction.Wait, c.Observe(both, 0), "manual both stabilize");
        ExpectSlot(0, c.Observe(both, 1), "manual upper claim");
        c.RecordClick(0, 2000, 1, true);
        ChestAccessibilitySnapshot lower = Snapshot(1000, ChestSlotState.Empty, ChestSlotState.Ready);
        Expect(PilotAction.Wait, c.Observe(lower, 3), "manual upper hidden after deduction");
        Expect(PilotAction.Wait, c.Observe(lower, 4), "manual upper reconciliation");
        Expect(PilotAction.Wait, c.Observe(lower, 5), "manual lower stabilizes");
        ExpectSlot(1, c.Observe(lower, 6), "manual lower remains claimable");
        c.RecordClick(1, 1000, 6, true);
        ChestAccessibilitySnapshot hidden = Snapshot(0, ChestSlotState.Empty, ChestSlotState.Empty);
        Expect(PilotAction.Wait, c.Observe(hidden, 8), "manual lower first hidden");
        Expect(PilotAction.Wait, c.Observe(hidden, 9), "manual lower reconciliation");
        Expect(PilotAction.Wait, c.Observe(hidden, 10), "manual dual depletion does not refill");
        Expect(PilotAction.Wait, c.Observe(Snapshot(2000, ChestSlotState.Countdown, ChestSlotState.Countdown), 30), "manual cooldown stays monitoring");
    }

    private static void ManualBusyAndBackoff()
    {
        AutoPilotController c = new AutoPilotController(false);
        ChestAccessibilitySnapshot unknown = Snapshot(0, ChestSlotState.Empty, ChestSlotState.Empty);
        unknown.Available = false;
        Expect(PilotAction.Wait, c.Observe(unknown, 0), "manual unavailable");
        Expect(PilotAction.Wait, c.Observe(Snapshot(1000, ChestSlotState.Busy, ChestSlotState.Ready), 1), "manual disabled target blocks input");
        ChestAccessibilitySnapshot both = Snapshot(2000, ChestSlotState.Ready, ChestSlotState.Ready);
        c.RecordClick(0, 2000, 2, false);
        Expect(PilotAction.Wait, c.Observe(both, 2.5), "manual lower stabilizes during upper backoff");
        ExpectSlot(1, c.Observe(both, 3.5), "manual failed upper cannot starve lower");
        c.Reset();
        ChestAccessibilitySnapshot ready = Snapshot(1000, ChestSlotState.Ready, ChestSlotState.Empty);
        c.RecordClick(0, 1000, 4, true);
        Expect(PilotAction.Wait, c.Observe(ready, 15), "manual waits for response");
        Expect(PilotAction.Wait, c.Observe(ready, 16), "manual timeout starts backoff");
        Expect(PilotAction.Wait, c.Observe(ready, 18), "manual backoff continues");
        Expect(PilotAction.Wait, c.Observe(ready, 19), "manual retry stabilizes");
        ExpectSlot(0, c.Observe(ready, 20), "manual retry after backoff");
    }
}
