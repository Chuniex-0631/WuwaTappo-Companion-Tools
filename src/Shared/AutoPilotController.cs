using System;
using System.Drawing;

internal enum PilotAction { Wait, Fill, Claim }

internal sealed class PilotDecision
{
    public PilotAction Action;
    public string State;
    public string Detail;
    public ChestCandidate Candidate;
    public long Target = AutoPilotController.Reserve;
}

// UI-thread owned. The clock is monotonic and injectable for offline scenario tests.
// Missing UI controls mean unobserved/hidden, never proof of a completed reward.
internal sealed class AutoPilotController
{
    internal const long Reserve = 2000;
    private readonly bool autoRefill;
    private int pendingSlot = -1;
    private double clickedAt;
    private long beforeClick;
    private int changedSamples;
    private double changedSince;
    private ChestSlotState changedState;
    private int stableSlot = -1;
    private Point stablePoint;
    private int stableSamples;
    private readonly double[] retryAt = new double[2];
    private readonly int[] attempts = new int[2];
    private long lastBalance = -1;
    private double progressAt;
    private double fillPausedUntil;

    public AutoPilotController(bool autoRefill = true)
    {
        this.autoRefill = autoRefill;
    }

    public void Reset()
    {
        pendingSlot = -1;
        stableSlot = -1;
        stableSamples = changedSamples = 0;
        lastBalance = -1;
        progressAt = fillPausedUntil = 0;
        Array.Clear(retryAt, 0, retryAt.Length);
        Array.Clear(attempts, 0, attempts.Length);
    }

    public void Invalidate()
    {
        stableSlot = -1;
        stableSamples = changedSamples = 0;
        lastBalance = -1;
    }

    public PilotDecision Observe(ChestAccessibilitySnapshot snapshot, double now)
    {
        if (!snapshot.Available || snapshot.PointBalance < 0)
        {
            Invalidate();
            return Wait("等待本地状态", "暂时无法读取点数和槽位，输入已暂停");
        }

        long balance = snapshot.PointBalance;
        if (pendingSlot >= 0)
        {
            ChestSlotState state = snapshot.Slot(pendingSlot).State;
            if (now - clickedAt < 2.0)
                return Wait("等待领取响应", "已暂停连点，等待桌宠更新界面");

            if (state == ChestSlotState.Empty || state == ChestSlotState.Countdown)
            {
                if (changedSamples == 0 || state != changedState)
                {
                    changedSamples = 0;
                    changedSince = now;
                    changedState = state;
                }
                changedSamples++;
                if (changedSamples < 2 || now - changedSince < 0.5)
                    return Wait("同步槽位状态", "目标槽位已变化，正在复核当前界面");

                int slot = pendingSlot;
                pendingSlot = -1;
                stableSlot = -1;
                stableSamples = changedSamples = 0;
                lastBalance = -1;
                if (beforeClick - balance >= 1000)
                    attempts[slot] = 0;
                // No success counter: a hidden slot or optimistic countdown is not a receipt.
                return Wait("重新规划", state == ChestSlotState.Empty
                    ? (autoRefill ? "槽位已隐藏，接下来按实际余额补足双槽储备" : "槽位已隐藏，继续监控可见宝箱")
                    : (autoRefill ? "槽位已进入倒计时，接下来检查双槽点数储备" : "槽位已进入倒计时，继续监控双槽变化"));
            }

            changedSamples = 0;
            if (now - clickedAt < 12.0)
                return Wait("等待领取响应", "桌宠仍在处理，期间不会重复点击");

            int failed = pendingSlot;
            pendingSlot = -1;
            retryAt[failed] = now + RetryDelay(attempts[failed]);
            stableSlot = -1;
            stableSamples = 0;
            return Wait("等待重试", "响应尚未确认，将延迟重试并重新读取状态");
        }

        if (snapshot.Upper.State == ChestSlotState.Busy || snapshot.Lower.State == ChestSlotState.Busy)
        {
            Invalidate();
            return Wait("等待桌宠处理", "宝箱按钮暂不可用，内部输入已暂停");
        }

        if (balance >= 1000)
        {
            // A delayed/blocked slot must not starve the other ready slot.
            bool anyReady = false;
            for (int i = 0; i < 2; i++)
            {
                ChestCandidate candidate = snapshot.Slot(i).ToCandidate(i);
                if (candidate == null) continue;
                anyReady = true;
                if (now < retryAt[i]) continue;
                if (stableSlot == i && Distance(stablePoint, candidate.ScreenCenter) <= 18)
                    stableSamples++;
                else
                {
                    stableSlot = i;
                    stablePoint = candidate.ScreenCenter;
                    stableSamples = 1;
                }
                if (stableSamples < 2)
                    return Wait("宝箱已就绪", "已暂停连点，正在复核宝箱位置");
                return new PilotDecision { Action = PilotAction.Claim, Candidate = candidate,
                    State = "准备领取宝箱", Detail = "等待鼠标空闲后领取，随后重新检查槽位" };
            }
            if (anyReady)
                return Wait("等待重试", "宝箱仍可见，按退避计划等待下一次尝试");
        }

        stableSlot = -1;
        stableSamples = 0;
        if (!autoRefill)
            return Wait("监控中", balance < 1000
                ? "余额不足，槽位可能隐藏；补点请先停止监控"
                : "当前没有可领取宝箱，等待槽位变化");
        if (balance >= Reserve)
        {
            lastBalance = balance;
            progressAt = now;
            bool countdown = snapshot.Upper.State == ChestSlotState.Countdown ||
                snapshot.Lower.State == ChestSlotState.Countdown;
            return Wait(countdown ? "等待宝箱冷却" : "等待槽位显示",
                countdown ? "双槽已储备 2,000 点，到期后自动检查领取"
                          : "储备已足够，等待桌宠界面就绪或显示宝箱");
        }

        if (balance != lastBalance)
        {
            lastBalance = balance;
            progressAt = Math.Max(now, fillPausedUntil);
        }
        if (now < fillPausedUntil)
            return Wait("等待计数恢复", "点数暂未增长，稍后自动重新尝试补点");
        if (now - progressAt >= 12.0)
        {
            fillPausedUntil = now + 30.0;
            progressAt = fillPausedUntil;
            return Wait("等待计数恢复", "点数暂未增长，已暂停输入并等待恢复");
        }
        return new PilotDecision { Action = PilotAction.Fill, State = "正在补充点数",
            Detail = "双槽储备还需 " + (Reserve - balance).ToString("N0") + " 点" };
    }

    public void RecordClick(int slot, long balance, double now, bool sent)
    {
        if (slot < 0 || slot > 1) throw new ArgumentOutOfRangeException("slot");
        stableSlot = -1;
        stableSamples = changedSamples = 0;
        attempts[slot] = Math.Min(attempts[slot] + 1, 5);
        retryAt[slot] = now + RetryDelay(attempts[slot]);
        if (!sent) return;
        pendingSlot = slot;
        clickedAt = now;
        beforeClick = balance;
    }

    private static double RetryDelay(int attempt)
    {
        return Math.Min(60.0, 3.0 * Math.Pow(2.0, Math.Max(0, attempt - 1)));
    }

    private static double Distance(Point a, Point b)
    {
        double x = (double)a.X - b.X, y = (double)a.Y - b.Y;
        return Math.Sqrt(x * x + y * y);
    }

    private static PilotDecision Wait(string state, string detail)
    {
        return new PilotDecision { Action = PilotAction.Wait, State = state, Detail = detail };
    }
}
