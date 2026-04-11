using System.Diagnostics;
using Hexa.NET.ImGui;
using Tomlet;
using Tomlet.Models;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Gui.Backend;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Gui.Views;

public partial class ConfigsView
{
    public static readonly string WindowTitle = $"{IconFonts.FontAwesome6.Gear} Configs";

    private ImGuiTextFilterPtr _filter = ImGui.ImGuiTextFilter();

    // Popup filter state
    private bool _filterProject = true;
    private bool _filterUser = true;
    // 0=none, 1=modified from default, 2=modified from git, 3=either
    private int _modifiedFilter = 0;

    private bool IsAnyFilterActive =>
        _filter.IsActive() || !_filterProject || !_filterUser || _modifiedFilter != 0;

    private ImGuiTreeNodeFlags TreeNodeFlags =>
        IsAnyFilterActive ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;

    private readonly Dictionary<Type, (string[] Names, Array Values)> _enumCache = [];
    private readonly Dictionary<ConfigEntry, object> _gitBaseline = [];
    private bool HasGitBaseline => _gitBaseline.Count > 0;

    public ConfigsView(string? projectConfigPath = null)
    {
        if (projectConfigPath != null)
            _gitBaseline = BuildGitBaseline(projectConfigPath);
    }

    private static Dictionary<ConfigEntry, object> BuildGitBaseline(string configPath)
    {
        var result = new Dictionary<ConfigEntry, object>();

        var relativePath = RunGit($"ls-files --full-name \"{configPath}\"")?.Trim();
        if (string.IsNullOrEmpty(relativePath)) return result;

        var content = RunGit($"show HEAD:{relativePath}");
        if (content == null) return result;

        TomlDocument document;
        try { document = new TomlParser().Parse(content); }
        catch { return result; }

        foreach (var configurable in Registry.Configurables.Values)
        {
            var fullPath = $"{configurable.Namespace}.{configurable.Type.Name}";
            var tomlPath = string.Join('.', fullPath.Split('.').Skip(1));

            if (!document.TryGetValue(tomlPath, out var tableValue) || tableValue is not TomlTable table)
                continue;

            foreach (var entry in configurable.Entries)
            {
                if (entry.StorageType != StorageType.Project) continue;
                if (!table.TryGetValue(entry.Name, out var tomlValue)) continue;

                try
                {
                    var committed = TomletMain.To(entry.Type, tomlValue);
                    if (committed != null) result[entry] = committed;
                }
                catch { /* skip entries that can't be deserialized */ }
            }
        }

        return result;
    }

    private static string? RunGit(string args)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("git", args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch { return null; }
    }

    public void Draw()
    {
        if (ImGui.Begin(WindowTitle))
        {
            DrawSearchBar();
            DrawTree(Registry.Tree);

            if (IsAnyFilterActive)
            {
                ImGui.Separator();
                var totalItems = Registry.Configurables.Values.Sum(c => c.Entries.Count);
                var visibleItems = CountMatchingFields(Registry.Tree);
                ImGui.TextColored(Color.Zinc400, $"{visibleItems} of {totalItems} items matching");
            }
        }

        ImGui.End();
    }

    private void DrawSearchBar()
    {
        var filterPopupActive = !_filterProject || !_filterUser || _modifiedFilter != 0;

        ImGui.PushItemWidth(-50f);
        _filter.Draw("##search");
        ImGui.PopItemWidth();

        ImGui.SameLine();
        if (_filter.IsActive())
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.Xmark}##clear"))
                _filter.Clear();
        }
        else
        {
            ImGui.TextColored(Color.Zinc600, $"{IconFonts.FontAwesome6.MagnifyingGlass}");
        }

        ImGui.SameLine();
        if (filterPopupActive)
            ImGui.PushStyleColor(ImGuiCol.Text, Color.Amber.RGBA);

        if (ImGui.Button($"{IconFonts.FontAwesome6.Filter}##openfilter"))
            ImGui.OpenPopup("##filterpopup");

        if (filterPopupActive)
            ImGui.PopStyleColor();

        if (ImGui.BeginPopup("##filterpopup"))
        {
            DrawFilterPopup();
            ImGui.EndPopup();
        }

        ImGui.Separator();
    }

    private void DrawFilterPopup()
    {
        ImGui.TextColored(Color.Zinc400, "Storage");
        ImGui.Checkbox($"{IconFonts.FontAwesome6.Database}  Project", ref _filterProject);
        ImGui.Checkbox($"{IconFonts.FontAwesome6.LaptopFile}  User", ref _filterUser);

        ImGui.Separator();
        ImGui.TextColored(Color.Zinc400, "Modified");

        if (ImGui.RadioButton("None", _modifiedFilter == 0)) _modifiedFilter = 0;
        if (ImGui.RadioButton("From default", _modifiedFilter == 1)) _modifiedFilter = 1;
        if (HasGitBaseline)
        {
            if (ImGui.RadioButton("From git", _modifiedFilter == 2)) _modifiedFilter = 2;
            if (ImGui.RadioButton("Either", _modifiedFilter == 3)) _modifiedFilter = 3;
        }
    }

    // Whether an entry passes all active filters (text + popup).
    // configurableNameMatches: whether the parent configurable's name matched the text filter.
    private bool EntryPassesFilter(ConfigEntry entry, bool configurableNameMatches)
    {
        if (_filter.IsActive() && !configurableNameMatches && !_filter.PassFilter(entry.Name))
            return false;

        if (entry.StorageType == StorageType.Project && !_filterProject) return false;
        if (entry.StorageType == StorageType.User && !_filterUser) return false;

        if (_modifiedFilter != 0)
        {
            var isModifiedFromDefault = !Equals(entry.Value, entry.DefaultValue);
            _gitBaseline.TryGetValue(entry, out var committed);
            var isModifiedFromGit = committed != null && !Equals(entry.Value, committed);

            var passes = _modifiedFilter switch
            {
                1 => isModifiedFromDefault,
                2 => isModifiedFromGit,
                3 => isModifiedFromDefault || isModifiedFromGit,
                _ => true,
            };
            if (!passes) return false;
        }

        return true;
    }

    private bool ConfigurableHasVisibleEntries(Configurable configurable)
    {
        var nameMatch = _filter.PassFilter(configurable.Type.Name);
        return configurable.Entries.Any(e => EntryPassesFilter(e, nameMatch));
    }

    private void DrawTree(Dictionary<string, object> tree, int depth = 0)
    {
        foreach (var (key, value) in tree)
        {
            switch (value)
            {
                case Configurable configurable:
                    if (ConfigurableHasVisibleEntries(configurable))
                        DrawConfigurable(key, configurable);
                    break;

                case Dictionary<string, object> subTree:
                    if (ChildrenMatchSearch(subTree))
                    {
                        var icon = depth == 0
                            ? $"{IconFonts.FontAwesome6.CubesStacked}"
                            : $"{IconFonts.FontAwesome6.Cube}";
                        if (ImGui.TreeNodeEx($"{icon} {key}", TreeNodeFlags))
                        {
                            DrawTree(subTree, depth + 1);
                            ImGui.TreePop();
                        }
                    }
                    break;
            }
        }
    }

    private void DrawConfigurable(string name, Configurable configurable)
    {
        var hasModifiedEntries = HasGitBaseline && configurable.Entries.Any(e =>
            _gitBaseline.TryGetValue(e, out var committed) && !Equals(e.Value, committed));

        if (hasModifiedEntries)
            ImGui.PushStyleColor(ImGuiCol.Text, Color.Amber.RGBA);

        var nodeOpen = ImGui.TreeNodeEx($"{IconFonts.FontAwesome6.Gears} {name}", TreeNodeFlags);

        if (hasModifiedEntries)
            ImGui.PopStyleColor();

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
        {
            ImGui.BeginTooltip();
            ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);
            ImGui.TextColored(Color.Zinc400, $"{configurable.Type.FullName}");
            ImGui.PopFont();
            if (!string.IsNullOrEmpty(configurable.Description))
            {
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 15.0f);
                ImGui.TextUnformatted(configurable.Description);
                ImGui.PopTextWrapPos();
            }
            ImGui.EndTooltip();
        }

        if (nodeOpen)
        {
            var ownFilterMatch = _filter.PassFilter(configurable.Type.Name);
            if (ImGui.BeginTable("fields", 4, ImGuiTableFlags.BordersH))
            {
                ImGui.TableSetupColumn("icon", ImGuiTableColumnFlags.WidthFixed, 15f);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                ImGui.TableSetupColumn("Reset", ImGuiTableColumnFlags.WidthFixed, HasGitBaseline ? 46f : 22f);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 1.5f);

                foreach (var field in configurable.Entries)
                {
                    if (!EntryPassesFilter(field, ownFilterMatch)) continue;
                    ImGui.TableNextRow();
                    DrawField(field);
                }

                ImGui.EndTable();
            }

            ImGui.TreePop();
        }
    }

    private void DrawField(ConfigEntry field)
    {
        ImGui.PushID(field.Name);

        _gitBaseline.TryGetValue(field, out var committedValue);
        var isModifiedFromGit = committedValue != null && !Equals(field.Value, committedValue);
        var isModifiedFromDefault = !Equals(field.Value, field.DefaultValue);

        // Icon
        ImGui.TableNextColumn();
        var icon = field.StorageType switch
        {
            StorageType.Project => IconFonts.FontAwesome6.Database,
            StorageType.User => IconFonts.FontAwesome6.LaptopFile,
            _ => IconFonts.FontAwesome6.Question,
        };
        if (isModifiedFromGit)
            ImGui.TextColored(Color.Amber, icon);
        else
            ImGui.TextUnformatted(icon);

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            ImGui.SetTooltip($"{field.StorageType} config");

        // Name
        ImGui.TableNextColumn();
        if (isModifiedFromGit)
            ImGui.TextColored(Color.Amber, field.Name);
        else
            ImGui.TextUnformatted(field.Name);

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
        {
            ImGui.BeginTooltip();
            ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);
            ImGui.TextColored(Color.Zinc400, $"{field.Type.FullName}");
            ImGui.PopFont();
            if (!string.IsNullOrEmpty(field.Description))
            {
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 15.0f);
                ImGui.TextUnformatted(field.Description);
                ImGui.PopTextWrapPos();
            }
            if (isModifiedFromGit)
            {
                ImGui.Separator();
                ImGui.TextUnformatted("Committed:");
                ImGui.SameLine();
                ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);
                ImGui.TextColored(Color.Amber, $"{committedValue}");
                ImGui.PopFont();
            }
            ImGui.EndTooltip();
        }

        ImGui.BeginDisabled(!field.Editable);

        // Reset buttons — only shown when applicable
        ImGui.TableNextColumn();
        var anyResetShown = false;

        if (isModifiedFromDefault)
        {
            if (ImGui.SmallButton($"{IconFonts.FontAwesome6.RotateLeft}"))
                field.Value = field.DefaultValue;

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("Reset to");
                ImGui.SameLine();
                ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);
                ImGui.TextColored(Color.Amber, $"{field.DefaultValue}");
                ImGui.PopFont();
                ImGui.EndTooltip();
            }

            anyResetShown = true;
        }

        if (HasGitBaseline && isModifiedFromGit)
        {
            if (anyResetShown) ImGui.SameLine();

            if (ImGui.SmallButton($"{IconFonts.FontAwesome6.CodeBranch}"))
            {
                field.Value = committedValue!;
                field.MarkChanged();
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted("Reset to committed:");
                ImGui.SameLine();
                ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);
                ImGui.TextColored(Color.Amber, $"{committedValue}");
                ImGui.PopFont();
                ImGui.EndTooltip();
            }
        }

        // Value
        ImGui.TableNextColumn();
        ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);
        var (changed, newVal) = DrawFieldEditor(field.Value);
        if (changed)
        {
            field.Value = newVal!;
            field.MarkChanged();
        }
        ImGui.PopFont();

        ImGui.EndDisabled();
        ImGui.PopID();
    }

    private bool ChildrenMatchSearch(Dictionary<string, object> tree)
    {
        if (!IsAnyFilterActive) return true;

        foreach (var value in tree.Values)
        {
            switch (value)
            {
                case Configurable configurable:
                    if (ConfigurableHasVisibleEntries(configurable)) return true;
                    break;
                case Dictionary<string, object> subTree:
                    if (ChildrenMatchSearch(subTree)) return true;
                    break;
            }
        }

        return false;
    }

    private int CountMatchingFields(Dictionary<string, object> tree)
    {
        var count = 0;
        foreach (var value in tree.Values)
        {
            switch (value)
            {
                case Configurable configurable:
                {
                    var nameMatch = _filter.PassFilter(configurable.Type.Name);
                    count += configurable.Entries.Count(e => EntryPassesFilter(e, nameMatch));
                    break;
                }
                case Dictionary<string, object> subTree:
                    count += CountMatchingFields(subTree);
                    break;
            }
        }
        return count;
    }
}
