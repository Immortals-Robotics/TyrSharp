using Hexa.NET.ImGui;
using Tyr.Gui.Backend;

namespace Tyr.Gui.Data;

public class DebugFilter(DebugFramer debugFramer)
{
    private readonly Dictionary<string, bool> _nodes = [];

    public bool IsEnabled(string moduleName)
    {
        return _nodes.GetValueOrDefault(moduleName);
    }

    private void Register(string moduleName)
    {
        _nodes.TryAdd(moduleName, true);
    }

    public void Draw()
    {
        foreach (var module in debugFramer.Modules.Keys)
        {
            Register(module);
        }

        if (ImGui.Begin("Debug Filter"))
        {
            ImGui.PushFont(FontRegistry.Instance.UiFont);

            foreach (var (name, enabled) in _nodes)
            {
                var refEnabled = enabled;
                if (ImGui.Checkbox(name, ref refEnabled))
                {
                    _nodes[name] = refEnabled;
                }
            }

            ImGui.PopFont();
        }

        ImGui.End();
    }
}