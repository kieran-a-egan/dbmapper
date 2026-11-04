using Photino.NET;

// Isolation probe: plain Photino.NET window with inline HTML, no Blazor. STA entry point.
public static class Program
{
    [STAThread]
    public static void Main()
    {
        var win = new PhotinoWindow().SetTitle("Photino probe").SetSize(600, 400).Center();
        win.LoadRawString("<html><body style='background:#fff'><h1>probe ok</h1></body></html>");
        win.WaitForClose();
    }
}
