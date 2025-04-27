using Hexa.NET.ImGui;
using Tyr.Gui.Backend;

namespace Tyr.Gui.Data;

public class DebugFilter(DebugFramer debugFramer)
{
    // Dictionary to track the enabled state of each node in the tree
    // Format: "module" or "module/file" or "module/file/function" or "module/file/function/line"
    private readonly Dictionary<string, bool> _filterState = [];

    public bool IsEnabled(string moduleName, string? filePath, string? memberName, int lineNumber)
    {
        // Build the path to check
        string path = moduleName;

        if (filePath != null)
        {
            path = $"{path}/{filePath}";

            if (memberName != null)
            {
                path = $"{path}/{memberName}";

                if (lineNumber != null)
                {
                    path = $"{path}/{lineNumber}";
                }
            }
        }

        // Check parent nodes first (hierarchical filtering)
        string[] parts = path.Split('/');
        string currentPath = "";

        // Check each level of the hierarchy
        for (int i = 0; i < parts.Length; i++)
        {
            currentPath = i == 0 ? parts[0] : $"{currentPath}/{parts[i]}";

            // If any parent level is disabled, this node is disabled
            if (_filterState.TryGetValue(currentPath, out bool enabled) && !enabled)
                return false;
        }

        // If we have explicitly stored this path, return its state
        if (_filterState.TryGetValue(path, out bool state))
            return state;

        // Default to enabled if not found
        return true;
    }

    private void RegisterNode(string path, bool defaultEnabled = true)
    {
        _filterState.TryAdd(path, defaultEnabled);
    }

    public void Draw()
    {
        // Register all modules, files, and functions
        foreach (var (moduleName, framer) in debugFramer.Modules)
        {
            RegisterNode(moduleName);

            foreach (var (filePath, functions) in framer.MetaTree)
            {
                string filePath_nodePath = $"{moduleName}/{filePath}";
                RegisterNode(filePath_nodePath);

                foreach (var (functionName, lines) in functions)
                {
                    string function_nodePath = $"{filePath_nodePath}/{functionName}";
                    RegisterNode(function_nodePath);

                    foreach (var line in lines)
                    {
                        RegisterNode($"{function_nodePath}/{line}");
                    }
                }
            }
        }

        if (ImGui.Begin($"{IconFonts.FontAwesome6.Filter} Debug Filter"))
        {
            // Draw modules
            foreach (var (moduleName, framer) in debugFramer.Modules)
            {
                DrawModuleNode(moduleName, framer);
            }
        }

        ImGui.End();
    }

    private void DrawModuleNode(string moduleName, ModuleDebugFramer framer)
    {
        // Get current state
        bool isEnabled = _filterState.GetValueOrDefault(moduleName, true);
        bool wasEnabled = isEnabled;

        // Create tree node with checkbox
        ImGui.PushID(moduleName);
        bool isOpen = ImGui.TreeNodeEx($"##tree_{moduleName}");
        ImGui.SameLine();
        ImGui.Checkbox($"{moduleName}", ref isEnabled);

        // Update state if changed
        if (isEnabled != wasEnabled)
        {
            _filterState[moduleName] = isEnabled;
        }

        // Draw child nodes if open
        if (isOpen)
        {
            // Draw files
            foreach (var (filePath, functions) in framer.MetaTree)
            {
                string filePath_nodePath = $"{moduleName}/{filePath}";
                DrawFileNode(filePath_nodePath, filePath, functions);
            }

            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private void DrawFileNode(string nodePath, string filePath, Dictionary<string, SortedSet<int>> functions)
    {
        // Get current state
        bool isEnabled = _filterState.GetValueOrDefault(nodePath, true);
        bool wasEnabled = isEnabled;

        // Extract filename from path for display
        string displayName = System.IO.Path.GetFileName(filePath);

        // Create tree node with checkbox
        ImGui.PushID(nodePath);
        bool isOpen = ImGui.TreeNodeEx($"##tree_{nodePath}");
        ImGui.SameLine();
        ImGui.Checkbox($"{displayName}", ref isEnabled);

        // Show full path as tooltip
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(filePath);
        }

        // Update state if changed
        if (isEnabled != wasEnabled)
        {
            _filterState[nodePath] = isEnabled;
        }

        // Draw child nodes if open
        if (isOpen)
        {
            // Draw functions
            foreach (var (functionName, lines) in functions)
            {
                string function_nodePath = $"{nodePath}/{functionName}";
                DrawFunctionNode(function_nodePath, functionName, lines);
            }

            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private void DrawFunctionNode(string nodePath, string functionName, SortedSet<int> lines)
    {
        // Get current state
        bool isEnabled = _filterState.GetValueOrDefault(nodePath, true);
        bool wasEnabled = isEnabled;

        // Create tree node with checkbox
        ImGui.PushID(nodePath);
        bool isOpen = ImGui.TreeNodeEx($"##tree_{nodePath}");
        ImGui.SameLine();
        ImGui.Checkbox($"{functionName}()", ref isEnabled);

        // Update state if changed
        if (isEnabled != wasEnabled)
        {
            _filterState[nodePath] = isEnabled;
        }

        // Draw child nodes if open
        if (isOpen)
        {
            // Draw lines
            foreach (var lineNumber in lines)
            {
                string line_nodePath = $"{nodePath}/{lineNumber}";
                DrawLineNode(line_nodePath, lineNumber);
            }

            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private void DrawLineNode(string nodePath, int lineNumber)
    {
        // Get current state
        bool isEnabled = _filterState.GetValueOrDefault(nodePath, true);
        bool wasEnabled = isEnabled;

        // Create leaf node with checkbox (no children)
        ImGui.PushID(nodePath);
        ImGui.Indent();
        ImGui.Checkbox($"Line {lineNumber}", ref isEnabled);
        ImGui.Unindent();

        // Update state if changed
        if (isEnabled != wasEnabled)
        {
            _filterState[nodePath] = isEnabled;
        }

        ImGui.PopID();
    }
}