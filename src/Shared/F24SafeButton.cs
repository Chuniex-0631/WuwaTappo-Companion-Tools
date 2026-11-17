using System.Windows.Forms;

// The helper's global F24 input also reaches its focused control. ButtonBase
// clears an in-progress mouse press on any KeyUp, so an injected F24 release
// between mouse-down and mouse-up would silently cancel Click. Ignore F24
// locally while retaining normal mouse, keyboard and accessibility activation.
internal class F24SafeButton : Button
{
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F24) { e.Handled = true; return; }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F24) { e.Handled = true; return; }
        base.OnKeyUp(e);
    }
}
