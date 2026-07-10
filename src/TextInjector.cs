using System;
using System.Threading;
using System.Windows.Forms;

namespace Flow;

/// <summary>
/// Inserts text into whatever app currently has focus by putting it on the
/// clipboard and sending Ctrl+V, then restoring the previous clipboard contents.
/// Runs on a dedicated STA thread so clipboard access is legal and the UI never blocks.
/// </summary>
public static class TextInjector
{
    private const byte VK_CONTROL = 0x11;
    private const byte VK_V = 0x56;

    public static void Insert(string text, bool paste = true)
    {
        if (string.IsNullOrEmpty(text)) return;

        var t = new Thread(() => DoPaste(text)) { IsBackground = true };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
    }

    private static void DoPaste(string text)
    {
        string? backup = null;
        try { if (Clipboard.ContainsText()) backup = Clipboard.GetText(); } catch { }

        if (!TrySetClipboard(text))
        {
            Thread.Sleep(40);
            TrySetClipboard(text);
        }

        Thread.Sleep(30);
        Native.keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        Native.keybd_event(VK_V, 0, 0, UIntPtr.Zero);
        Native.keybd_event(VK_V, 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
        Native.keybd_event(VK_CONTROL, 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);

        Thread.Sleep(140);
        if (backup != null) TrySetClipboard(backup);
    }

    private static bool TrySetClipboard(string text)
    {
        try { Clipboard.SetText(text); return true; }
        catch { return false; }
    }
}
