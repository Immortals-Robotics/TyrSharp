using Hexa.NET.ImGui;

namespace Tyr.Gui;

public class DebugFilter
{
    private record struct Node(string Name, bool Enabled);

    private readonly Dictionary<string, bool> _nodes = [];

    public void Register(string moduleName)
    {
        _nodes.TryAdd(moduleName, true);
    }

    public bool IsEnabled(string moduleName)
    {
        return _nodes.GetValueOrDefault(moduleName);
    }

    public void Draw()
    {
        ImGui.Begin("Debug Filter");

        foreach (var (name, enabled) in _nodes)
        {
            var refEnabled = enabled;
            if (ImGui.Checkbox(name, ref refEnabled))
            {
                _nodes[name] = refEnabled;
            }
        }
        
        ImGui.End();
    }
}