using Photino.NET;

namespace DesktopHostSpike;

// ponytail: static holder for the one native window; a DI registration would be the upgrade path if more windows appear.
public static class HostWindow
{
    public static PhotinoWindow? Window { get; set; }
}
