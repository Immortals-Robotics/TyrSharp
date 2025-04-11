using Hexa.NET.KittyUI;

namespace Tyr.Gui;

internal static class Program
{
    private static void Main()
    {
        var builder = AppBuilder.Create();
        builder.EnableLogging(true);
        builder.EnableDebugTools(true);
        builder.SetTitle("Tyr");
        builder.StyleColorsClassic();

        builder.AddWindow<Window>();

        builder.Run();
    }
}