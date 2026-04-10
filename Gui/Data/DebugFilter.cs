using Cysharp.Text;
using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Debug;
using Tyr.Common.Debug.Drawing;
using Tyr.Gui.Backend;
using StrSpan = System.ReadOnlySpan<char>;

namespace Tyr.Gui.Data;

[Configurable]
public sealed partial class DebugFilter(Tyr.Common.Debug.Db.IDebugDb debugDb) : IDisposable
{
    // Dictionary to track the enabled state of each node in the tree
    // Format: "module" or "module/file" or "module/layer/file" or "module/layer/file/member" or "module/layer/file/member/line"
    [ConfigEntry(StorageType.User, editable: false)]
    private static Dictionary<string, bool> FilterState { get; set; } = [];

    private readonly Dictionary<string, bool>.AlternateLookup<StrSpan> _lookup =
        FilterState.GetAlternateLookup<StrSpan>();

    private readonly Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, HashSet<MetaTreeItem>>>>> _metaTrees = [];
    private Utf16ValueStringBuilder _stringBuilder = ZString.CreateStringBuilder();

    private bool _dirty;
    private bool _snapshotDirty = true;
    private DebugFilterSnapshot? _snapshot;

    private StrSpan MakePath(string module, string? layer = null,
        string? file = null, string? member = null, int? line = null)
    {
        _stringBuilder.Clear();
        AppendNormalizedPathPart(module);

        if (layer != null)
        {
            _stringBuilder.Append('/');
            AppendNormalizedPathPart(layer);
        }

        if (file != null)
        {
            _stringBuilder.Append('/');
            AppendNormalizedPathPart(file);
        }

        if (member != null)
        {
            _stringBuilder.Append('/');
            AppendNormalizedPathPart(member);
        }

        if (line != null)
        {
            _stringBuilder.Append('/');
            _stringBuilder.Append(line.Value);
        }

        return _stringBuilder.AsSpan();
    }

    public bool IsEnabled(Meta meta) =>
        IsEnabled(meta.Module, meta.Layer, meta.File, meta.Member, meta.Line);

    public bool IsEnabled(string module, string? layer = null,
        string? file = null, string? member = null, int? line = null)
    {
        _stringBuilder.Clear();
        AppendNormalizedPathPart(module);
        if (!IsEnabledInternal(_stringBuilder.AsSpan())) return false;

        if (layer == null) return true;
        _stringBuilder.Append('/');
        AppendNormalizedPathPart(layer);
        if (!IsEnabledInternal(_stringBuilder.AsSpan())) return false;

        if (file == null) return true;
        _stringBuilder.Append('/');
        AppendNormalizedPathPart(file);
        if (!IsEnabledInternal(_stringBuilder.AsSpan())) return false;

        if (member == null) return true;
        _stringBuilder.Append('/');
        AppendNormalizedPathPart(member);
        if (!IsEnabledInternal(_stringBuilder.AsSpan())) return false;

        if (line == null) return true;
        _stringBuilder.Append('/');
        _stringBuilder.Append(line.Value);
        if (!IsEnabledInternal(_stringBuilder.AsSpan())) return false;

        return true;

        bool IsEnabledInternal(StrSpan path) => !_lookup.TryGetValue(path, out var enabled) || enabled;
    }

    private void AppendNormalizedPathPart(string value)
    {
        if (!value.Contains('.'))
        {
            _stringBuilder.Append(value);
            return;
        }

        foreach (var c in value)
            _stringBuilder.Append(c == '.' ? '_' : c);
    }

    public DebugFilterSnapshot Snapshot()
    {
        if (_snapshotDirty || _snapshot is null)
        {
            _snapshot?.Dispose();
            _snapshot = new DebugFilterSnapshot(FilterState.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.Ordinal));
            _snapshotDirty = false;
        }

        return _snapshot;
    }

    public void Draw()
    {
        if (ImGui.Begin($"{IconFonts.FontAwesome6.Filter} Debug Filter"))
        {
            _dirty = false;

            RegisterModules();

            foreach (var moduleName in debugDb.QueryModules())
            {
                if (!_metaTrees.TryGetValue(moduleName, out var metaTree) || metaTree.Count == 0) continue;

                DrawModuleNode(moduleName, metaTree);
            }

            if (_dirty)
            {
                _snapshotDirty = true;
                Configurable.MarkChanged(StorageType.User);
            }
        }

        ImGui.End();
    }

    // Register all modules, files, and functions
    private void RegisterModules()
    {
        foreach (var module in debugDb.QueryModules())
        {
            _dirty |= FilterState.TryAdd(module, true);

            RegisterMetaTree(module,
                debugDb.QuerySourceLocations<Tyr.Common.Debug.Logging.Entry>(module),
                MetaTreeItem.ItemType.Log);
            RegisterMetaTree(module,
                debugDb.QuerySourceLocations<Tyr.Common.Debug.Drawing.Command>(module),
                MetaTreeItem.ItemType.Draw);
            RegisterMetaTree(module,
                debugDb.QuerySourceLocations<Tyr.Common.Debug.Plotting.Command>(module),
                MetaTreeItem.ItemType.Plot);

            if (!_metaTrees.TryGetValue(module, out var metaTree))
                continue;

            foreach (var (layer, files) in metaTree)
            {
                var defaultValue = !Meta.IsDebugLayer(layer);
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

    private void RegisterMetaTree(string module, IEnumerable<Meta> metas, MetaTreeItem.ItemType type)
    {
        foreach (var meta in metas)
            AddToMetaTree(module, meta, type);
    }

    private void AddToMetaTree(string module, Meta meta, MetaTreeItem.ItemType type)
    {
        if (meta is not { File: not null, Member: not null })
            return;

        if (!_metaTrees.TryGetValue(module, out var metaTree))
        {
            metaTree = [];
            _metaTrees[module] = metaTree;
        }

        if (!metaTree.TryGetValue(meta.Layer, out var fileDict))
        {
            fileDict = [];
            metaTree[meta.Layer] = fileDict;
        }

        if (!fileDict.TryGetValue(meta.File, out var functionDict))
        {
            functionDict = [];
            fileDict[meta.File] = functionDict;
        }

        if (!functionDict.TryGetValue(meta.Member, out var lineSet))
        {
            lineSet = [];
            functionDict[meta.Member] = lineSet;
        }

        lineSet.Add(MetaTreeItem.GetOrCreate(type, meta.Line, meta.Expression));
    }

    private void DrawModuleNode(string module,
        Dictionary<string, Dictionary<string, Dictionary<string, HashSet<MetaTreeItem>>>> metaTree)
    {
        // Get current state
        var isEnabled = FilterState[module];

        // Create tree node with checkbox
        ImGui.PushID(module);
        var isOpen = ImGui.TreeNode("");
        ImGui.SameLine();
        _dirty |= ImGui.Checkbox("##enabled", ref isEnabled);
        ImGui.SameLine();
        ImGui.TextUnformatted(IconFonts.FontAwesome6.CubesStacked);
        ImGui.SameLine();
        ImGui.TextUnformatted(module);

        FilterState[module] = isEnabled;

        // Draw child nodes if open
        if (isOpen)
        {
            ImGui.BeginDisabled(!isEnabled);

            // Draw files
            foreach (var (layer, files) in metaTree)
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
            var debugLayer = Meta.IsDebugLayer(layer);
            var layerName = debugLayer ? layer[Meta.DebugLayerPrefix.Length..] : layer;
            // Create tree node with checkbox
            ImGui.PushID(layer);
            isOpen = ImGui.TreeNode("");
            ImGui.SameLine();

            ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);
            if (debugLayer) ImGui.PushStyleColor(ImGuiCol.Text, Color.Yellow200);
            _dirty |= ImGui.Checkbox("##enabled", ref isEnabled);
            ImGui.SameLine();
            ImGui.TextUnformatted(IconFonts.FontAwesome6.LayerGroup);
            ImGui.SameLine();
            ImGui.TextUnformatted(layerName);
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
        ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);
        _dirty |= ImGui.Checkbox("##enabled", ref isEnabled);
        ImGui.SameLine();
        ImGui.TextUnformatted(IconFonts.FontAwesome6.FileCode);
        ImGui.SameLine();
        ImGui.TextUnformatted(displayName);
        ImGui.PopFont();

        // Show full path as tooltip
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
        {
            ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);
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
        ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);
        _dirty |= ImGui.Checkbox("##enabled", ref isEnabled);
        ImGui.SameLine();
        ImGui.TextUnformatted(IconFonts.FontAwesome6.Code);
        ImGui.SameLine();
        ImGui.Text($"{member}()");
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
        ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);

        var icon = treeItem.Type switch
        {
            MetaTreeItem.ItemType.Log => IconFonts.FontAwesome6.Terminal,
            MetaTreeItem.ItemType.Draw => IconFonts.FontAwesome6.Shapes,
            MetaTreeItem.ItemType.Plot => IconFonts.FontAwesome6.ChartLine,
            _ => IconFonts.FontAwesome6.Square // Default to square icon for other types
        };

        _dirty |= ImGui.Checkbox("##enabled", ref isEnabled);
        ImGui.SameLine();
        ImGui.TextUnformatted(icon);
        ImGui.SameLine();
        ImGui.Text($"Line {treeItem.Line}");

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
        _snapshot?.Dispose();
        _stringBuilder.Dispose();
    }
}
