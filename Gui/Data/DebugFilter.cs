using Hexa.NET.ImGui;
using Tyr.Gui.Backend;

namespace Tyr.Gui.Data;

public class DebugFilter(DebugFramer debugFramer)
{
    // Dictionary to track the enabled state of each node in the tree
    // Format: "module" or "module/file" or "module/file/function" or "module/file/function/line"
    private readonly Dictionary<string, bool> _filterState = [];

    private bool IsEnabledInternal(string path) => !_filterState.TryGetValue(path, out var enabled) || enabled;

    public bool IsEnabled(string moduleName, string? filePath = null, string? memberName = null, int? lineNumber = null)
    {
        if (!IsEnabledInternal(moduleName)) return false;

        if (filePath == null) return true;
        if (!IsEnabledInternal($"{moduleName}/{filePath}")) return false;

        if (memberName == null) return true;
        if (!IsEnabledInternal($"{moduleName}/{filePath}/{memberName}")) return false;

        if (lineNumber == null) return true;
        if (!IsEnabledInternal($"{moduleName}/{filePath}/{memberName}/{lineNumber}")) return false;

        return true;
    }

    public void Draw()
    {
        if (ImGui.Begin($"{IconFonts.FontAwesome6.Filter} Debug Filter"))
        {
            RegisterModules();

            foreach (var (moduleName, framer) in debugFramer.Modules)
            {
                DrawModuleNode(moduleName, framer);
            }
        }

        ImGui.End();
    }

    // Register all modules, files, and functions
    private void RegisterModules()
    {
        foreach (var (moduleName, framer) in debugFramer.Modules)
        {
            _filterState.TryAdd(moduleName, true);

            foreach (var (filePath, functions) in framer.MetaTree)
            {
                var filePathNodePath = $"{moduleName}/{filePath}";
                _filterState.TryAdd(filePathNodePath, true);

                foreach (var (functionName, lines) in functions)
                {
                    var functionNodePath = $"{filePathNodePath}/{functionName}";
                    _filterState.TryAdd(functionNodePath, true);

                    foreach (var line in lines)
                    {
                        _filterState.TryAdd($"{functionNodePath}/{line}", true);
                    }
                }
            }
        }
    }

    private void DrawModuleNode(string moduleName, ModuleDebugFramer framer)
    {
        // Get current state
        var isEnabled = _filterState[moduleName];

        // Create tree node with checkbox
        ImGui.PushID(moduleName);
        var isOpen = ImGui.TreeNodeEx($"##tree_{moduleName}");
        ImGui.SameLine();
        ImGui.Checkbox($"{IconFonts.FontAwesome6.CubesStacked} {moduleName}", ref isEnabled);

        _filterState[moduleName] = isEnabled;

        // Draw child nodes if open
        if (isOpen)
        {
            ImGui.BeginDisabled(!isEnabled);

            // Draw files
            foreach (var (filePath, functions) in framer.MetaTree)
            {
                var filePathNodePath = $"{moduleName}/{filePath}";
                DrawFileNode(filePathNodePath, filePath, functions, isEnabled);
            }

            ImGui.TreePop();
            ImGui.EndDisabled();
        }

        ImGui.PopID();
    }

    private void DrawFileNode(string nodePath, string filePath, Dictionary<string, SortedSet<int>> functions,
        bool parentEnabled)
    {
        // Get current state
        var isEnabled = parentEnabled && _filterState[nodePath];

        // Extract filename from path for display
        var displayName = Path.GetFileName(filePath);

        // Create tree node with checkbox
        ImGui.PushID(nodePath);
        var isOpen = ImGui.TreeNodeEx($"##tree_{nodePath}");
        ImGui.SameLine();
        ImGui.PushFont(FontRegistry.Instance.MonoFont);
        ImGui.Checkbox($"{IconFonts.FontAwesome6.FileCode} {displayName}", ref isEnabled);
        ImGui.PopFont();

        // Show full path as tooltip
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
        {
            ImGui.PushFont(FontRegistry.Instance.MonoFont);
            ImGui.SetTooltip(filePath);
            ImGui.PopFont();
        }

        if (parentEnabled)
        {
            _filterState[nodePath] = isEnabled;
        }

        // Draw child nodes if open
        if (isOpen)
        {
            ImGui.BeginDisabled(!isEnabled);

            // Draw functions
            foreach (var (functionName, lines) in functions)
            {
                var functionNodePath = $"{nodePath}/{functionName}";
                DrawFunctionNode(functionNodePath, functionName, lines, isEnabled);
            }

            ImGui.EndDisabled();
            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private void DrawFunctionNode(string nodePath, string functionName, SortedSet<int> lines, bool parentEnabled)
    {
        // Get current state
        var isEnabled = parentEnabled && _filterState.GetValueOrDefault(nodePath, true);

        // Create tree node with checkbox
        ImGui.PushID(nodePath);
        var isOpen = ImGui.TreeNodeEx($"##tree_{nodePath}");
        ImGui.SameLine();
        ImGui.PushFont(FontRegistry.Instance.MonoFont);
        ImGui.Checkbox($"{IconFonts.FontAwesome6.Code} {functionName}()", ref isEnabled);
        ImGui.PopFont();

        if (parentEnabled)
        {
            _filterState[nodePath] = isEnabled;
        }

        // Draw child nodes if open
        if (isOpen)
        {
            ImGui.BeginDisabled(!isEnabled);

            // Draw lines
            foreach (var lineNumber in lines)
            {
                var lineNodePath = $"{nodePath}/{lineNumber}";
                DrawLineNode(lineNodePath, lineNumber, isEnabled);
            }

            ImGui.EndDisabled();
            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private void DrawLineNode(string nodePath, int lineNumber, bool parentEnabled)
    {
        // Get current state
        var isEnabled = parentEnabled && _filterState[nodePath];

        // Create leaf node with checkbox (no children)
        ImGui.PushID(nodePath);
        ImGui.Indent();
        ImGui.PushFont(FontRegistry.Instance.MonoFont);
        ImGui.Checkbox($"{IconFonts.FontAwesome6.BarsStaggered} Line {lineNumber}", ref isEnabled);
        ImGui.PopFont();
        ImGui.Unindent();

        if (parentEnabled)
        {
            _filterState[nodePath] = isEnabled;
        }

        ImGui.PopID();
    }
}