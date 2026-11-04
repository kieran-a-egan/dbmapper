using Photino.Blazor;
using DesktopHostSpike;

public static class Program
{
    // STAThread is required: Photino's WebView2 host never creates its web surface on an MTA thread (observed: black window, no msedgewebview2 child).
    [STAThread]
    public static void Main(string[] args)
    {
        var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);
        appBuilder.RootComponents.Add<App>("#app");

        var app = appBuilder.Build();

        app.MainWindow
            .SetTitle("DbMapper host spike")
            .SetSize(960, 640)
            .Center();

        HostWindow.Window = app.MainWindow;
        app.Run();
    }
}
