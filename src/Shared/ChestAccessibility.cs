using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using AutomationCondition = System.Windows.Automation.Condition;
using AutomationControlType = System.Windows.Automation.ControlType;
using AutomationElement = System.Windows.Automation.AutomationElement;
using AutomationTreeScope = System.Windows.Automation.TreeScope;

internal sealed class ChestCandidate
{
    public Point ScreenCenter;
    public Rectangle ScreenBounds;
    public int SlotIndex = -1;
}

internal enum ChestSlotState
{
    Empty,
    Countdown,
    Ready,
    Busy
}

internal sealed class ChestSlotObservation
{
    public ChestSlotState State;
    public string DisplayText = "空/隐藏";
    public Rectangle ScreenBounds;

    public ChestCandidate ToCandidate(int slotIndex)
    {
        if (State != ChestSlotState.Ready || ScreenBounds.Width <= 0 || ScreenBounds.Height <= 0) return null;
        ChestCandidate candidate = new ChestCandidate();
        candidate.SlotIndex = slotIndex;
        candidate.ScreenBounds = ScreenBounds;
        candidate.ScreenCenter = new Point(ScreenBounds.Left + ScreenBounds.Width / 2, ScreenBounds.Top + ScreenBounds.Height / 2);
        return candidate;
    }
}

internal sealed class ChestAccessibilitySnapshot
{
    public bool Available;
    public long PointBalance = -1;
    public readonly ChestSlotObservation Upper = new ChestSlotObservation();
    public readonly ChestSlotObservation Lower = new ChestSlotObservation();

    public ChestSlotObservation Slot(int index) { return index == 0 ? Upper : Lower; }

}

// Captured values are projected separately so ambiguous layouts can be regression-tested
// without a running desktop pet or any UI Automation provider calls.
internal sealed class ChestAccessibilityNode
{
    public AutomationControlType ControlType;
    public string Name;
    public System.Windows.Rect Bounds;
    public bool Enabled;
}

internal static class ChestAccessibilityReader
{
    private sealed class VisibleReward
    {
        public ChestSlotState State;
        public string Text;
        public Rectangle Bounds;
    }

    public static ChestAccessibilitySnapshot Read(IntPtr window)
    {
        try
        {
            AutomationElement root = AutomationElement.FromHandle(window);
            if (root == null) return new ChestAccessibilitySnapshot();
            System.Windows.Automation.AutomationElementCollection elements = root.FindAll(
                AutomationTreeScope.Descendants, AutomationCondition.TrueCondition);
            List<ChestAccessibilityNode> nodes = new List<ChestAccessibilityNode>();
            for (int i = 0; i < elements.Count; i++)
            {
                AutomationElement.AutomationElementInformation current = elements[i].Current;
                if (current.IsOffscreen) continue;
                nodes.Add(new ChestAccessibilityNode { ControlType = current.ControlType,
                    Name = current.Name, Bounds = current.BoundingRectangle, Enabled = current.IsEnabled });
            }
            return Interpret(nodes);
        }
        catch (System.Windows.Automation.ElementNotAvailableException) { }
        catch (InvalidOperationException) { }
        catch (COMException) { }
        catch (Exception ex) { Debug.WriteLine("Chest accessibility read failed: " + ex); }
        return new ChestAccessibilitySnapshot();
    }

    internal static ChestAccessibilitySnapshot Interpret(IEnumerable<ChestAccessibilityNode> nodes)
    {
        ChestAccessibilitySnapshot result = new ChestAccessibilitySnapshot();
        double counterCenterY = double.NaN;
        double counterLeft = 0;
        double scale = 1D;
        int counterCandidates = 0;
        List<VisibleReward> rewards = new List<VisibleReward>();
        foreach (ChestAccessibilityNode node in nodes)
        {
            string name = node.Name ?? string.Empty;
            System.Windows.Rect nativeBounds = node.Bounds;
            if (!UsableBounds(nativeBounds)) continue;
            long pointBalance;
            if (node.ControlType == AutomationControlType.Text &&
                long.TryParse(name, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out pointBalance) && pointBalance >= 0)
            {
                counterCandidates++;
                counterCenterY = nativeBounds.Top + nativeBounds.Height / 2D;
                counterLeft = nativeBounds.Left;
                scale = Math.Max(0.75D, Math.Min(3D, nativeBounds.Height / 23.2D));
                result.PointBalance = pointBalance;
                continue;
            }
            if (node.ControlType == AutomationControlType.Image && IsCountdownText(name))
            {
                rewards.Add(new VisibleReward { State = ChestSlotState.Countdown,
                    Text = name, Bounds = ToRectangle(nativeBounds) });
                continue;
            }
            if (node.ControlType == AutomationControlType.Button)
                rewards.Add(new VisibleReward { State = node.Enabled ? ChestSlotState.Ready : ChestSlotState.Busy,
                    Text = node.Enabled ? "宝箱" : "处理中", Bounds = ToRectangle(nativeBounds) });
        }

        // Ambiguous numbers (e.g. an open inventory panel) must not drive input.
        if (double.IsNaN(counterCenterY) || counterCandidates != 1) return result;
        double upperCenterY = counterCenterY - 88.4D * scale;
        double lowerCenterY = counterCenterY - 40.4D * scale;
        double maximumDistance = 24D * scale;
        Rectangle[] buttonBounds = new Rectangle[2];
        bool[] buttonObserved = new bool[2];
        foreach (VisibleReward reward in rewards)
        {
            double horizontalOffset = reward.Bounds.Left + reward.Bounds.Width / 2D - counterLeft;
            if (horizontalOffset < 20D * scale || horizontalOffset > 340D * scale) continue;
            double centerY = reward.Bounds.Top + reward.Bounds.Height / 2D;
            double upperDistance = Math.Abs(centerY - upperCenterY);
            double lowerDistance = Math.Abs(centerY - lowerCenterY);
            if (Math.Min(upperDistance, lowerDistance) > maximumDistance) continue;
            if (reward.State == ChestSlotState.Ready || reward.State == ChestSlotState.Busy)
            {
                if (Math.Abs(reward.Bounds.Width - 62D * scale) > 13D * scale ||
                    Math.Abs(reward.Bounds.Height - 44D * scale) > 12D * scale) continue;
            }
            int slot = upperDistance <= lowerDistance ? 0 : 1;
            if (reward.State == ChestSlotState.Ready || reward.State == ChestSlotState.Busy)
            {
                // Separate matching buttons in the same slot are ambiguous. Duplicated
                // accessibility nodes for the same rectangle can be safely coalesced.
                if (buttonObserved[slot] && buttonBounds[slot] != reward.Bounds) return result;
                buttonObserved[slot] = true;
                buttonBounds[slot] = reward.Bounds;
            }
            ChestSlotObservation target = result.Slot(slot);
            // A disabled button must remain blocking even when its countdown image is
            // also exposed in the accessibility tree, independent of enumeration order.
            if (target.State == ChestSlotState.Busy) continue;
            if (target.State == ChestSlotState.Empty || reward.State == ChestSlotState.Busy ||
                reward.State == ChestSlotState.Countdown)
                Assign(target, reward);
        }
        result.Available = true;
        return result;
    }

    private static bool UsableBounds(System.Windows.Rect value)
    {
        return !value.IsEmpty && value.Width > 0D && value.Height > 0D &&
            !double.IsNaN(value.Left) && !double.IsNaN(value.Top) &&
            !double.IsInfinity(value.Left) && !double.IsInfinity(value.Top) &&
            !double.IsInfinity(value.Width) && !double.IsInfinity(value.Height) &&
            value.Left >= int.MinValue && value.Top >= int.MinValue &&
            value.Right <= int.MaxValue && value.Bottom <= int.MaxValue &&
            value.Width <= int.MaxValue && value.Height <= int.MaxValue;
    }

    private static void Assign(ChestSlotObservation target, VisibleReward reward)
    {
        target.State = reward.State;
        target.DisplayText = reward.Text;
        target.ScreenBounds = reward.Bounds;
    }

    private static Rectangle ToRectangle(System.Windows.Rect value)
    {
        return new Rectangle((int)Math.Round(value.Left), (int)Math.Round(value.Top),
            Math.Max(1, (int)Math.Round(value.Width)), Math.Max(1, (int)Math.Round(value.Height)));
    }

    private static bool IsCountdownText(string value)
    {
        if (value.Length < 4 || value.Length > 6) return false;
        int colon = value.IndexOf(':');
        if (colon < 1 || colon != value.LastIndexOf(':') || value.Length - colon - 1 != 2) return false;
        for (int i = 0; i < value.Length; i++)
            if (i != colon && (value[i] < '0' || value[i] > '9')) return false;
        return (value[colon + 1] - '0') * 10 + value[colon + 2] - '0' < 60;
    }
}
