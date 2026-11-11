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
        ChestAccessibilitySnapshot result = new ChestAccessibilitySnapshot();
        try
        {
            AutomationElement root = AutomationElement.FromHandle(window);
            if (root == null) return result;
            System.Windows.Automation.AutomationElementCollection elements = root.FindAll(
                AutomationTreeScope.Descendants, AutomationCondition.TrueCondition);

            double counterCenterY = double.NaN;
            double counterLeft = 0;
            double scale = 1D;
            int counterCandidates = 0;
            List<VisibleReward> rewards = new List<VisibleReward>();
            for (int i = 0; i < elements.Count; i++)
            {
                AutomationElement element = elements[i];
                if (element.Current.IsOffscreen) continue;
                string name = element.Current.Name ?? string.Empty;
                System.Windows.Rect nativeBounds = element.Current.BoundingRectangle;
                if (nativeBounds.IsEmpty || nativeBounds.Width <= 0D || nativeBounds.Height <= 0D) continue;

                long pointBalance;
                if (element.Current.ControlType == AutomationControlType.Text &&
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

                if (element.Current.ControlType == AutomationControlType.Image && IsCountdownText(name))
                {
                    rewards.Add(new VisibleReward
                    {
                        State = ChestSlotState.Countdown,
                        Text = name,
                        Bounds = ToRectangle(nativeBounds)
                    });
                    continue;
                }

                if (element.Current.ControlType == AutomationControlType.Button)
                {
                    rewards.Add(new VisibleReward
                    {
                        State = element.Current.IsEnabled ? ChestSlotState.Ready : ChestSlotState.Busy,
                        Text = element.Current.IsEnabled ? "宝箱" : "处理中",
                        Bounds = ToRectangle(nativeBounds)
                    });
                }
            }

            // Ambiguous numbers (e.g. an open inventory panel) must not drive input.
            if (double.IsNaN(counterCenterY) || counterCandidates != 1) return result;

            double upperCenterY = counterCenterY - 88.4D * scale;
            double lowerCenterY = counterCenterY - 40.4D * scale;
            double maximumDistance = 24D * scale;
            foreach (VisibleReward reward in rewards)
            {
                double horizontalOffset = reward.Bounds.Left + reward.Bounds.Width / 2D - counterLeft;
                if (horizontalOffset < 20D * scale || horizontalOffset > 340D * scale) continue;
                double centerY = reward.Bounds.Top + reward.Bounds.Height / 2D;
                double upperDistance = Math.Abs(centerY - upperCenterY);
                double lowerDistance = Math.Abs(centerY - lowerCenterY);
                double distance = Math.Min(upperDistance, lowerDistance);
                if (distance > maximumDistance) continue;

                if (reward.State == ChestSlotState.Ready || reward.State == ChestSlotState.Busy)
                {
                    double expectedWidth = 62D * scale;
                    double expectedHeight = 44D * scale;
                    if (Math.Abs(reward.Bounds.Width - expectedWidth) > 13D * scale ||
                        Math.Abs(reward.Bounds.Height - expectedHeight) > 12D * scale)
                        continue;
                }

                ChestSlotObservation target = upperDistance <= lowerDistance ? result.Upper : result.Lower;
                if (target.State == ChestSlotState.Empty || reward.State == ChestSlotState.Countdown)
                    Assign(target, reward);
            }
            result.Available = true;
        }
        catch (System.Windows.Automation.ElementNotAvailableException) { }
        catch (InvalidOperationException) { }
        catch (COMException) { }
        catch (Exception ex) { Debug.WriteLine("Chest accessibility read failed: " + ex); }
        return result;
    }

    private static void Assign(ChestSlotObservation target, VisibleReward reward)
    {
        target.State = reward.State;
        target.DisplayText = reward.Text;
        target.ScreenBounds = reward.Bounds;
    }

    private static Rectangle ToRectangle(System.Windows.Rect value)
    {
        return new Rectangle(
            (int)Math.Round(value.Left),
            (int)Math.Round(value.Top),
            Math.Max(1, (int)Math.Round(value.Width)),
            Math.Max(1, (int)Math.Round(value.Height)));
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
