using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Flow;

/// <summary>
/// Global hold-to-talk hotkey using a low-level keyboard hook.
/// Fires <see cref="Activated"/> when the combo (e.g. Ctrl+Space) is first held,
/// and <see cref="Deactivated"/> when it's released. Swallows the main key while
/// the combo is active so it doesn't type into the focused app.
/// </summary>
public sealed class HotKeyListener : IDisposable
{
    private readonly string _modifier;
    private readonly int _mainVk;
    private readonly Native.LowLevelKeyboardProc _proc;
    private IntPtr _hook = IntPtr.Zero;

    private bool _modDown;
    private bool _active;

    public event Action? Activated;
    public event Action? Deactivated;

    public HotKeyListener(string modifier, string key)
    {
        _modifier = string.IsNullOrWhiteSpace(modifier) ? "Ctrl" : modifier.Trim();
        _mainVk = ParseKey(key);
        _proc = HookProc; // keep a reference alive so the delegate isn't GC'd
    }

    public void Start()
    {
        _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, Native.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException("Could not install the global keyboard hook.");
    }

    private static int ParseKey(string key)
    {
        if (!string.IsNullOrWhiteSpace(key) && Enum.TryParse<Keys>(key, true, out var k))
            return (int)k;
        return (int)Keys.Space;
    }

    private bool IsModifierVk(int vk) => _modifier.ToLowerInvariant() switch
    {
        "ctrl" or "control" => vk == 0x11 || vk == 0xA2 || vk == 0xA3,
        "alt" or "menu"     => vk == 0x12 || vk == 0xA4 || vk == 0xA5,
        "shift"             => vk == 0x10 || vk == 0xA0 || vk == 0xA1,
        "win" or "windows"  => vk == 0x5B || vk == 0x5C,
        "none"              => false,
        _                   => vk == 0x11 || vk == 0xA2 || vk == 0xA3,
    };

    private bool ModifierSatisfied =>
        _modifier.Equals("none", StringComparison.OrdinalIgnoreCase) || _modDown;

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            int vk = Marshal.ReadInt32(lParam); // vkCode is the first field of KBDLLHOOKSTRUCT
            bool down = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
            bool up = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;

            if (IsModifierVk(vk))
            {
                if (down) _modDown = true;
                else if (up)
                {
                    _modDown = false;
                    if (_active) { _active = false; Deactivated?.Invoke(); }
                }
            }
            else if (vk == _mainVk)
            {
                if (down && ModifierSatisfied)
                {
                    if (!_active) { _active = true; Activated?.Invoke(); }
                    return (IntPtr)1; // swallow so the app never sees the keypress
                }
                if (up)
                {
                    bool wasActive = _active;
                    if (_active) { _active = false; Deactivated?.Invoke(); }
                    if (wasActive || ModifierSatisfied) return (IntPtr)1;
                }
            }
        }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
