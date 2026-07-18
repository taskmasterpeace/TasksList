using System.Windows.Input;

namespace TasksList.App.Shell;

public static class HotkeyGestureText
{
    public static string Format(HotkeyGesture gesture)
    {
        var parts = new List<string>();
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        var key = KeyInterop.KeyFromVirtualKey((int)gesture.VirtualKey);
        var keyText = key is >= Key.D0 and <= Key.D9
            ? ((int)key - (int)Key.D0).ToString()
            : key.ToString();
        parts.Add(keyText);
        return string.Join("+", parts);
    }

    public static bool TryParse(string text, out HotkeyGesture gesture)
    {
        gesture = new HotkeyGesture(HotkeyModifiers.None, 0);
        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        foreach (var token in parts[..^1])
        {
            if (token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifiers.Control;
            }
            else if (token.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifiers.Alt;
            }
            else if (token.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifiers.Shift;
            }
            else if (token.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                     token.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= HotkeyModifiers.Win;
            }
            else
            {
                return false;
            }
        }

        var keyToken = parts[^1];
        var parsed = keyToken.Length == 1 && char.IsAsciiDigit(keyToken[0])
            ? Enum.TryParse<Key>($"D{keyToken}", ignoreCase: true, out var key)
            : Enum.TryParse<Key>(keyToken, ignoreCase: true, out key);
        if (!parsed || key == Key.None)
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, (uint)KeyInterop.VirtualKeyFromKey(key));
        return gesture.IsBound;
    }
}
