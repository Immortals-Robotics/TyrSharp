using Hexa.NET.KittyUI;

Tyr.Common.Config.Storage.Initialize(args[0]);

using var sslVisionPublisher = new Tyr.Vision.SslVisionDataPublisher();
using var gcPublisher = new Tyr.Referee.GcDataPublisher();

using var referee = new Tyr.Referee.Runner();
using var vision = new Tyr.Vision.Runner();

var builder = AppBuilder.Create();
builder.EnableLogging(true);
builder.EnableDebugTools(true);
builder.SetTitle("Tyr");
builder.StyleColorsClassic();

builder.AddWindow<Tyr.Gui.Window>();

builder.Run();