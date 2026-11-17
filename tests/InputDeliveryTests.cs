using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// This executable replaces the Windows input boundary. It never sends real input.
internal static class InputDeliveryTests
{
    private static int assertions;

    private static void Main()
    {
        Equal(IntPtr.Size == 8 ? 40 : 28, Marshal.SizeOf(typeof(NativeMethods.INPUT)), "Windows INPUT layout");
        CheckMouseDelivery(2, 1, true);
        CheckMouseDelivery(0, 1, false);
        CheckMouseDelivery(1, 1, false);
        CheckMouseDelivery(1, 0, false);
        CheckKeyboardDelivery(2, 1, true);
        CheckKeyboardDelivery(0, 1, false);
        CheckKeyboardDelivery(1, 1, false);
        CheckKeyboardDelivery(1, 0, false);
        Console.WriteLine("Input delivery: 8 scenarios, " + assertions + " assertions passed; no real input sent.");
    }

    private static void CheckMouseDelivery(uint accepted, uint releaseAccepted, bool expected)
    {
        NativeMethods.INPUT down = new NativeMethods.INPUT();
        down.type = 0;
        down.U.mi.dwFlags = 0x0002;
        NativeMethods.INPUT up = new NativeMethods.INPUT();
        up.type = 0;
        up.U.mi.dwFlags = 0x0004;
        List<NativeMethods.INPUT[]> calls = new List<NativeMethods.INPUT[]>();
        bool result = NativeMethods.SendBalancedPress(new[] { down, up }, delegate(NativeMethods.INPUT[] batch)
        {
            calls.Add(batch);
            return calls.Count == 1 ? accepted : releaseAccepted;
        });
        Equal(expected, result, "mouse result");
        Equal(accepted == 1 ? 2 : 1, calls.Count, "only a partial press triggers release recovery");
        Equal(2, calls[0].Length, "down/up sent in one batch");
        Equal(0x0002U, calls[0][0].U.mi.dwFlags, "mouse down first");
        Equal(0x0004U, calls[0][1].U.mi.dwFlags, "mouse up second");
        if (calls.Count == 2)
        {
            Equal(1, calls[1].Length, "recovery sends one event");
            Equal(0U, calls[1][0].type, "mouse recovery event type");
            Equal(0x0004U, calls[1][0].U.mi.dwFlags, "recovery only releases");
        }
    }

    private static void CheckKeyboardDelivery(uint accepted, uint releaseAccepted, bool expected)
    {
        Func<uint, NativeMethods.INPUT[], int, uint> original = NativeMethods.InputSender;
        List<NativeMethods.INPUT[]> calls = new List<NativeMethods.INPUT[]>();
        NativeMethods.InputSender = delegate(uint count, NativeMethods.INPUT[] batch, int size)
        {
            Equal((uint)batch.Length, count, "keyboard native count");
            Equal(Marshal.SizeOf(typeof(NativeMethods.INPUT)), size, "keyboard native size");
            calls.Add(batch);
            return calls.Count == 1 ? accepted : releaseAccepted;
        };
        try
        {
            Equal(expected, NativeMethods.SendF24Press(), "keyboard result");
            Equal(accepted == 1 ? 2 : 1, calls.Count, "keyboard release recovery count");
            Equal(2, calls[0].Length, "keyboard down/up sent in one batch");
            Equal(1U, calls[0][0].type, "keyboard event type");
            Equal((ushort)0x87, calls[0][0].U.ki.wVk, "F24 down key");
            Equal(0U, calls[0][0].U.ki.dwFlags, "keyboard down flag");
            Equal((ushort)0x87, calls[0][1].U.ki.wVk, "F24 up key");
            Equal(0x0002U, calls[0][1].U.ki.dwFlags, "keyboard up flag");
            if (calls.Count == 2)
            {
                Equal(1, calls[1].Length, "keyboard recovery sends one event");
                Equal((ushort)0x87, calls[1][0].U.ki.wVk, "recovery releases F24");
                Equal(0x0002U, calls[1][0].U.ki.dwFlags, "keyboard recovery only releases");
            }
        }
        finally { NativeMethods.InputSender = original; }
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        assertions++;
        if (!object.Equals(expected, actual))
            throw new InvalidOperationException(message + ": expected " + expected + ", got " + actual);
    }
}
