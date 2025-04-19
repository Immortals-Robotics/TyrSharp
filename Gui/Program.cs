using Hexa.NET.KittyUI;

var builder = AppBuilder.Create();
builder.EnableLogging(true);
builder.EnableDebugTools(true);
builder.SetTitle("Tyr");
builder.StyleColorsClassic();

builder.AddWindow<Tyr.Gui.Window>();

builder.Run();