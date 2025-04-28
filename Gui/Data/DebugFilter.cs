using Cysharp.Text;
using Hexa.NET.ImGui;
using Tyr.Common.Debug;
using Tyr.Gui.Backend;
using StrSpan = System.ReadOnlySpan<char>;

namespace Tyr.Gui.Data;

public sealed class DebugFilter : IDisposable
{
    // Dictionary to track the enabled state of each node in the tree
    // Format: "module" or "module/file" or "module/file/function" or "module/file/function/line"
    private readonly Dictionary<string, bool> _filterState = [];
    private readonly Dictionary<string, bool>.AlternateLookup<StrSpan> _lookup;

    private Utf16ValueStringBuilder _stringBuilder = ZString.CreateStringBuilder();
    private readonly DebugFramer _debugFramer;

    public DebugFilter(DebugFramer debugFramer)
    {
        _debugFramer = debugFramer;
        _lookup = _filterState.GetAlternateLookup<StrSpan>();
    }

    private StrSpan MakePath(string moduleName, string? filePath = null,
        string? memberName = null, int? lineNumber = null)
    {
        _stringBuilder.Clear();

        if (filePath == null)
            _stringBuilder.AppendFormat("{0}", moduleName);
        else if (memberName == null)
            _stringBuilder.AppendFormat("{0}/{1}", moduleName, filePath);
        else if (lineNumber == null)
            _stringBuilder.AppendFormat("{0}/{1}/{2}", moduleName, filePath, memberName);
        else
            _stringBuilder.AppendFormat("{0}/{1}/{2}/{3}", moduleName, filePath, memberName, lineNumber);

        return _stringBuilder.AsSpan();
    }

    public bool IsEnabled(Meta meta) =>
        IsEnabled(meta.ModuleName, meta.FilePath, meta.MemberName, meta.LineNumber);

    public bool IsEnabled(string module, string? file = null, string? member = null, int? line = null)
    {
        if (!IsEnabledInternal(MakePath(module))) return false;

        if (file == null) return true;
        if (!IsEnabledInternal(MakePath(module, file))) return false;

        if (member == null) return true;
        if (!IsEnabledInternal(MakePath(module, file, member))) return false;

        if (line == null) return true;
        if (!IsEnabledInternal(MakePath(module, file, member, line.Value))) return false;

        return true;

        bool IsEnabledInternal(StrSpan path) => !_lookup.TryGetValue(path, out var enabled) || enabled;
    }

    public void Draw()
    {
        if (ImGui.Begin($"{IconFonts.FontAwesome6.Filter} Debug Filter"))
        {
            RegisterModules();

            foreach (var (moduleName, framer) in _debugFramer.Modules)
            {
                if (framer.MetaTree.Count == 0) continue;

                DrawModuleNode(moduleName, framer);
            }
        }

        ImGui.End();
    }

    // Register all modules, files, and functions
    private void RegisterModules()
    {
        foreach (var (module, framer) in _debugFramer.Modules)
        {
            _filterState.TryAdd(module, true);

            foreach (var (file, functions) in framer.MetaTree)
            {
                _lookup.TryAdd(MakePath(module, file), true);

                foreach (var (function, items) in functions)
                {
                    _lookup.TryAdd(MakePath(module, file, function), true);

                    foreach (var item in items)
                    {
                        _lookup.TryAdd(MakePath(module, file, function, item.Line), true);
                    }
                }
            }
        }
    }

    private void DrawModuleNode(string module, ModuleDebugFramer framer)
    {
        // Get current state
        var isEnabled = _filterState[module];

        // Create tree node with checkbox
        ImGui.PushID(module);
        var isOpen = ImGui.TreeNode("");
        ImGui.SameLine();
        ImGui.Checkbox($"{IconFonts.FontAwesome6.CubesStacked} {module}", ref isEnabled);

        _filterState[module] = isEnabled;

        // Draw child nodes if open
        if (isOpen)
        {
            ImGui.BeginDisabled(!isEnabled);

            // Draw files
            foreach (var (filePath, functions) in framer.MetaTree)
            {
                DrawFileNode(module, filePath, functions, isEnabled);
            }

            ImGui.TreePop();
            ImGui.EndDisabled();
        }

        ImGui.PopID();
    }

    private void DrawFileNode(string module, string file,
        Dictionary<string, SortedSet<MetaTreeItem>> functions, bool parentEnabled)
    {
        var path = MakePath(module, file);

        // Get current state
        var isEnabled = parentEnabled && _lookup[path];

        // Extract filename from the path for display
        var displayName = Path.GetFileName(file);

        // Create tree node with checkbox
        ImGui.PushID(file);
        var isOpen = ImGui.TreeNode("");
        ImGui.SameLine();
        ImGui.PushFont(FontRegistry.Instance.MonoFont);
        ImGui.Checkbox($"{IconFonts.FontAwesome6.FileCode} {displayName}", ref isEnabled);
        ImGui.PopFont();

        // Show full path as tooltip
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
        {
            ImGui.PushFont(FontRegistry.Instance.MonoFont);
            ImGui.SetTooltip(file);
            ImGui.PopFont();
        }

        if (parentEnabled)
        {
            _lookup[path] = isEnabled;
        }

        // Draw child nodes if open
        if (isOpen)
        {
            ImGui.BeginDisabled(!isEnabled);

            // Draw functions
            foreach (var (functionName, items) in functions)
            {
                DrawFunctionNode(module, file, functionName, items, isEnabled);
            }

            ImGui.EndDisabled();
            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private void DrawFunctionNode(string module, string file, string function,
        SortedSet<MetaTreeItem> items,
        bool parentEnabled)
    {
        var path = MakePath(module, file, function);

        // Get current state
        var isEnabled = parentEnabled && _lookup[path];

        // Create tree node with checkbox
        ImGui.PushID(function);
        var isOpen = ImGui.TreeNode("");
        ImGui.SameLine();
        ImGui.PushFont(FontRegistry.Instance.MonoFont);
        ImGui.Checkbox($"{IconFonts.FontAwesome6.Code} {function}()", ref isEnabled);
        ImGui.PopFont();

        if (parentEnabled)
        {
            _lookup[path] = isEnabled;
        }

        // Draw child nodes if open
        if (isOpen)
        {
            ImGui.BeginDisabled(!isEnabled);

            // Draw lines
            foreach (var item in items)
            {
                DrawItemNode(module, file, function, item, isEnabled);
            }

            ImGui.EndDisabled();
            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private void DrawItemNode(string module, string file, string function, MetaTreeItem treeItem,
        bool parentEnabled)
    {
        var path = MakePath(module, file, function, treeItem.Line);

        // Get current state
        var isEnabled = parentEnabled && _lookup[path];

        // Create leaf node with checkbox (no children)
        ImGui.PushID(treeItem.Line);
        ImGui.Indent();
        ImGui.PushFont(FontRegistry.Instance.MonoFont);

        var icon = treeItem.Type switch
        {
            MetaTreeItem.ItemType.Plot => $"{IconFonts.FontAwesome6.ChartLine}",
            MetaTreeItem.ItemType.Draw => $"{IconFonts.FontAwesome6.DrawPolygon}",
            _ => $"{IconFonts.FontAwesome6.Square}" // Default to square icon for other types
        };
        
        ImGui.Checkbox($"{icon} Line {treeItem.Line}", ref isEnabled);

        if (!string.IsNullOrWhiteSpace(treeItem.Expression) && ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
        {
            ImGui.SetTooltip(treeItem.Expression);
        }

        ImGui.PopFont();
        ImGui.Unindent();

        if (parentEnabled)
        {
            _lookup[path] = isEnabled;
        }

        ImGui.PopID();
    }

    public void Dispose()
    {
        _stringBuilder.Dispose();
        _stringBuilder = default;
    }
}