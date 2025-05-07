using Cysharp.Text;
using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Debug;
using Tyr.Common.Debug.Drawing;
using Tyr.Gui.Backend;
using StrSpan = System.ReadOnlySpan<char>;

namespace Tyr.Gui.Data;

[Configurable]
public sealed partial class DebugFilter(DebugFramer debugFramer) : IDisposable
{
    // Dictionary to track the enabled state of each node in the tree
    // Format: "module" or "module/file" or "module/layer/file" or "module/layer/file/member" or "module/layer/file/member/line"
    [ConfigEntry(StorageType.User)] private static Dictionary<string, bool> FilterState { get; set; } = [];

    private readonly Dictionary<string, bool>.AlternateLookup<StrSpan> _lookup =
        FilterState.GetAlternateLookup<StrSpan>();
    
    private bool IsDebugLayer(string layer) => layer.StartsWith(Meta.DebugLayerPrefix, StringComparison.OrdinalIgnoreCase);

    private Utf16ValueStringBuilder _stringBuilder = ZString.CreateStringBuilder();

    private bool _dirty;

    private StrSpan MakePath(string module, string? layer = null,
        string? file = null, string? member = null, int? line = null)
    {
        _stringBuilder.Clear();

        if (layer == null)
            _stringBuilder.AppendFormat("{0}", module);
        else if (file == null)
            _stringBuilder.AppendFormat("{0}/{1}", module, layer);
        else if (member == null)
            _stringBuilder.AppendFormat("{0}/{1}/{2}", module, layer, file);
        else if (line == null)
            _stringBuilder.AppendFormat("{0}/{1}/{2}/{3}", module, layer, file, member);
        else
            _stringBuilder.AppendFormat("{0}/{1}/{2}/{3}/{4}", module, layer, file, member, line);

        _stringBuilder.Replace('.', '_');
        return _stringBuilder.AsSpan();
    }

    public bool IsEnabled(Meta meta) =>
        IsEnabled(meta.Module, meta.Layer, meta.File, meta.Member, meta.Line);

    public bool IsEnabled(string module, string? layer = null,
        string? file = null, string? member = null, int? line = null)
    {
        if (!IsEnabledInternal(MakePath(module))) return false;

        if (layer == null) return true;
        if (!IsEnabledInternal(MakePath(module, layer))) return false;

        if (file == null) return true;
        if (!IsEnabledInternal(MakePath(module, layer, file))) return false;

        if (member == null) return true;
        if (!IsEnabledInternal(MakePath(module, layer, file, member))) return false;

        if (line == null) return true;
        if (!IsEnabledInternal(MakePath(module, layer, file, member, line.Value))) return false;

        return true;

        bool IsEnabledInternal(StrSpan path) => !_lookup.TryGetValue(path, out var enabled) || enabled;
    }

    public void Draw()
    {
        if (ImGui.Begin($"{IconFonts.FontAwesome6.Filter} Debug Filter"))
        {
            _dirty = false;

            RegisterModules();

            foreach (var (moduleName, framer) in debugFramer.Modules)
            {
                if (framer.MetaTree.Count == 0) continue;

                DrawModuleNode(moduleName, framer);
            }

            if (_dirty)
            {
                Configurable.MarkChanged(StorageType.User);
            }
        }

        ImGui.End();
    }

    // Register all modules, files, and functions
    private void RegisterModules()
    {
        foreach (var (module, framer) in debugFramer.Modules)
        {
            _dirty |= FilterState.TryAdd(module, true);
            foreach (var (layer, files) in framer.MetaTree)
            {
                var defaultValue = !IsDebugLayer(layer);
                _dirty |= _lookup.TryAdd(MakePath(module, layer), defaultValue);
                foreach (var (file, functions) in files)
                {
                    _dirty |= _lookup.TryAdd(MakePath(module, layer, file), true);
                    foreach (var (member, items) in functions)
                    {
                        _dirty |= _lookup.TryAdd(MakePath(module, layer, file, member), true);
                        foreach (var item in items)
                        {
                            _dirty |= _lookup.TryAdd(MakePath(module, layer, file, member, item.Line), true);
                        }
                    }
                }
            }
        }
    }

    private void DrawModuleNode(string module, ModuleDebugFramer framer)
    {
        // Get current state
        var isEnabled = FilterState[module];

        // Create tree node with checkbox
        ImGui.PushID(module);
        var isOpen = ImGui.TreeNode("");
        ImGui.SameLine();
        _dirty |= ImGui.Checkbox($"{IconFonts.FontAwesome6.CubesStacked} {module}", ref isEnabled);

        FilterState[module] = isEnabled;

        // Draw child nodes if open
        if (isOpen)
        {
            ImGui.BeginDisabled(!isEnabled);

            // Draw files
            foreach (var (layer, files) in framer.MetaTree)
            {
                DrawLayerNode(module, layer, files, isEnabled);
            }

            ImGui.TreePop();
            ImGui.EndDisabled();
        }

        ImGui.PopID();
    }

    private void DrawLayerNode(string module, string layer,
        Dictionary<string, Dictionary<string, HashSet<MetaTreeItem>>> files, bool parentEnabled)
    {
        var path = MakePath(module, layer);

        // Get current state
        var isEnabled = parentEnabled && _lookup[path];

        var emptyLayer = string.IsNullOrWhiteSpace(layer);

        var isOpen = true;

        if (!emptyLayer)
        {
            var debugLayer = IsDebugLayer(layer);
            var layerName = debugLayer ? layer[Meta.DebugLayerPrefix.Length..] : layer;
            // Create tree node with checkbox
            ImGui.PushID(layer);
            isOpen = ImGui.TreeNode("");
            ImGui.SameLine();

            ImGui.PushFont(FontRegistry.Instance.MonoFont);
            if (debugLayer) ImGui.PushStyleColor(ImGuiCol.Text, Color.Yellow200);
            _dirty |= ImGui.Checkbox($"{IconFonts.FontAwesome6.LayerGroup} {layerName}", ref isEnabled);
            ImGui.PopFont();
            if (debugLayer) ImGui.PopStyleColor();
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
            foreach (var (file, functions) in files)
            {
                DrawFileNode(module, layer, file, functions, isEnabled);
            }

            ImGui.EndDisabled();

            if (!emptyLayer) ImGui.TreePop();
        }

        if (!emptyLayer) ImGui.PopID();
    }

    private void DrawFileNode(string module, string layer, string file,
        Dictionary<string, HashSet<MetaTreeItem>> functions, bool parentEnabled)
    {
        var path = MakePath(module, layer, file);

        // Get current state
        var isEnabled = parentEnabled && _lookup[path];

        // Extract filename from the path for display
        var displayName = Path.GetFileName(file);

        // Create tree node with checkbox
        ImGui.PushID(file);
        var isOpen = ImGui.TreeNode("");
        ImGui.SameLine();
        ImGui.PushFont(FontRegistry.Instance.MonoFont);
        _dirty |= ImGui.Checkbox($"{IconFonts.FontAwesome6.FileCode} {displayName}", ref isEnabled);
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
            foreach (var (member, items) in functions)
            {
                DrawMemberNode(module, layer, file, member, items, isEnabled);
            }

            ImGui.EndDisabled();
            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private void DrawMemberNode(string module, string layer, string file, string member,
        HashSet<MetaTreeItem> items, bool parentEnabled)
    {
        var path = MakePath(module, layer, file, member);

        // Get current state
        var isEnabled = parentEnabled && _lookup[path];

        // Create tree node with checkbox
        ImGui.PushID(member);
        var isOpen = ImGui.TreeNode("");
        ImGui.SameLine();
        ImGui.PushFont(FontRegistry.Instance.MonoFont);
        _dirty |= ImGui.Checkbox($"{IconFonts.FontAwesome6.Code} {member}()", ref isEnabled);
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
                DrawItemNode(module, layer, file, member, item, isEnabled);
            }

            ImGui.EndDisabled();
            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private void DrawItemNode(string module, string layer, string file, string member, MetaTreeItem treeItem,
        bool parentEnabled)
    {
        var path = MakePath(module, layer, file, member, treeItem.Line);

        // Get current state
        var isEnabled = parentEnabled && _lookup[path];

        // Create leaf node with checkbox (no children)
        ImGui.PushID(treeItem.Line);
        ImGui.Indent();
        ImGui.PushFont(FontRegistry.Instance.MonoFont);

        var icon = treeItem.Type switch
        {
            MetaTreeItem.ItemType.Log => $"{IconFonts.FontAwesome6.Terminal}",
            MetaTreeItem.ItemType.Draw => $"{IconFonts.FontAwesome6.Shapes}",
            MetaTreeItem.ItemType.Plot => $"{IconFonts.FontAwesome6.ChartLine}",
            _ => $"{IconFonts.FontAwesome6.Square}" // Default to square icon for other types
        };

        _dirty |= ImGui.Checkbox($"{icon} Line {treeItem.Line}", ref isEnabled);

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
    }
}