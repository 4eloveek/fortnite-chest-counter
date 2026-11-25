using FortniteChestCounter.Core;

namespace FortniteChestCounter.Windows;

/// <summary>
/// A button that, once clicked, captures the next key combination as a global
/// hotkey. It intercepts command keys (Tab, arrows, Enter) so the user can bind
/// any ordinary key; only modifier-only presses are ignored and Esc cancels.
/// </summary>
internal sealed class HotkeyCaptureButton : Button
{
    private bool _capturing;

    public event Action<HotkeyBinding>? Captured;
    public event Action? Cancelled;

    public void BeginCapture()
    {
        _capturing = true;
        Text = "Нажмите нужную клавишу… (Esc — отмена)";
        Focus();
    }

    protected override bool IsInputKey(Keys keyData) => _capturing || base.IsInputKey(keyData);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_capturing)
        {
            CaptureKey(keyData);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (_capturing)
        {
            CaptureKey(keyData);
            return true;
        }

        return base.ProcessDialogKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_capturing)
        {
            CaptureKey(e.KeyData);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        if (_capturing)
        {
            _capturing = false;
            Cancelled?.Invoke();
        }

        base.OnLostFocus(e);
    }

    private void CaptureKey(Keys keyData)
    {
        Keys keyCode = keyData & Keys.KeyCode;
        if (keyCode == Keys.Escape && (keyData & Keys.Modifiers) == Keys.None)
        {
            _capturing = false;
            Cancelled?.Invoke();
            return;
        }

        if (keyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            // Wait for a real key; a lone modifier is not a usable global hotkey.
            return;
        }

        int modifiers = 0;
        if ((keyData & Keys.Control) != 0) modifiers |= HotkeyBinding.Control;
        if ((keyData & Keys.Alt) != 0) modifiers |= HotkeyBinding.Alt;
        if ((keyData & Keys.Shift) != 0) modifiers |= HotkeyBinding.Shift;

        _capturing = false;
        Captured?.Invoke(new HotkeyBinding(modifiers, (int)keyCode));
    }
}