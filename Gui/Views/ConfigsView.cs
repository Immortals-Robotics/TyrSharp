using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Network;
using Tyr.Gui.Backend;

namespace Tyr.Gui.Views;

public class ConfigsView
{
    private ImGuiTextFilterPtr _filter = ImGui.ImGuiTextFilter();
    private bool IsFiltering => _filter.IsActive();

    private ImGuiTreeNodeFlags TreeNodeFlags => IsFiltering ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;

    private readonly Dictionary<Type, (string[] Names, Array Values)> _enumCache = [];

    public void Draw()
    {
        if (ImGui.Begin($"{IconFonts.FontAwesome6.Gear} Configs"))
        {
            DrawSearchBar();
            DrawTree(Registry.Tree);

            // Add status bar at bottom
            if (IsFiltering)
            {
                ImGui.Separator();

                // Count how many items are shown/filtered
                var totalItems = Registry.Configurables.Count;
                var visibleItems = IsFiltering ? CountMatchingFields(Registry.Tree) : totalItems;

                ImGui.TextColored(Color.Zinc400, $"{visibleItems} of {totalItems} items matching");
            }
        }

        ImGui.End();
    }

    private void DrawSearchBar()
    {
        ImGui.PushItemWidth(-24); // Make space for the clear button
        _filter.Draw("##search");
        ImGui.PopItemWidth();
        ImGui.SameLine();
        if (IsFiltering)
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.Xmark}##clear"))
            {
                _filter.Clear();
            }
        }
        else
        {
            ImGui.TextColored(Color.Zinc600, $"{IconFonts.FontAwesome6.MagnifyingGlass}");
        }

        ImGui.Separator();
    }

    private void DrawTree(Dictionary<string, object> tree, int depth = 0)
    {
        foreach (var (key, value) in tree)
        {
            switch (value)
            {
                case Configurable configurable:
                    // Check if any of its fields match the search
                    var fieldsMatch = configurable.Entries.Any(field => _filter.PassFilter(field.Name));
                    if (fieldsMatch)
                    {
                        DrawConfigurable(key, configurable);
                    }

                    break;

                case Dictionary<string, object> subTree:
                {
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
    }

    private void DrawConfigurable(string name, Configurable configurable)
    {
        var nodeOpen = ImGui.TreeNodeEx($"{IconFonts.FontAwesome6.Gears} {name}", TreeNodeFlags);

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
        {
            ImGui.BeginTooltip();

            ImGui.PushFont(FontRegistry.Instance.MonoFont);
            ImGui.TextColored(Color.Zinc400, $"{configurable.Type.FullName}");
            ImGui.PopFont();

            if (!string.IsNullOrEmpty(configurable.Comment))
            {
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 15.0f);
                ImGui.TextUnformatted(configurable.Comment);
                ImGui.PopTextWrapPos();
            }

            ImGui.EndTooltip();
        }

        if (nodeOpen)
        {
            if (ImGui.BeginTable("fields", 3, ImGuiTableFlags.BordersH))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                ImGui.TableSetupColumn("Reset", ImGuiTableColumnFlags.WidthFixed, 15f);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 1.5f);

                foreach (var field in configurable.Entries)
                {
                    // Only show fields that match the search criteria when filtering
                    if (!_filter.PassFilter(field.Name)) continue;

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

        // Name
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(field.Name);

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
        {
            ImGui.BeginTooltip();

            ImGui.PushFont(FontRegistry.Instance.MonoFont);
            ImGui.TextColored(Color.Zinc400, $"{field.Type.FullName}");
            ImGui.PopFont();

            if (!string.IsNullOrEmpty(field.Comment))
            {
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 15.0f);
                ImGui.TextUnformatted(field.Comment);
                ImGui.PopTextWrapPos();
            }

            ImGui.EndTooltip();
        }

        // Reset button
        ImGui.TableNextColumn();
        if (ImGui.SmallButton($"{IconFonts.FontAwesome6.RotateLeft}"))
        {
            field.Value = field.DefaultValue;
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
        {
            ImGui.BeginTooltip();
            ImGui.Text("Reset to");
            ImGui.SameLine();
            ImGui.PushFont(FontRegistry.Instance.MonoFont);
            ImGui.TextColored(Color.Amber, $"{field.DefaultValue}");
            ImGui.PopFont();
            ImGui.EndTooltip();
        }

        // Value
        ImGui.TableNextColumn();
        DrawFieldEditor(field);

        ImGui.PopID();
    }

    private void DrawFieldEditor(ConfigEntry field)
    {
        ImGui.PushFont(FontRegistry.Instance.MonoFont);
        switch (field.Value)
        {
            case int intValue:
                if (ImGui.InputInt("", ref intValue))
                {
                    field.Value = (intValue);
                }

                break;

            case float floatValue:
                if (ImGui.InputFloat("", ref floatValue))
                {
                    field.Value = (floatValue);
                }

                break;

            case double doubleValue:
                var tmpValue = (float)doubleValue;
                if (ImGui.InputFloat("", ref tmpValue))
                {
                    field.Value = ((double)tmpValue);
                }

                break;

            case bool boolValue:
                if (ImGui.Checkbox("", ref boolValue))
                {
                    field.Value = (boolValue);
                }

                break;

            case string stringValue:
                if (ImGui.InputText("", ref stringValue, 256))
                {
                    field.Value = stringValue;
                }

                break;

            case Address addressValue:
                var ip = addressValue.Ip;
                var port = addressValue.Port;
                ImGui.InputText("##ip", ref ip, 256, ImGuiInputTextFlags.CharsDecimal);
                ImGui.SameLine();
                ImGui.Text(":");
                ImGui.SameLine();
                ImGui.InputInt("##port", ref port);

                field.Value = new Address { Ip = ip, Port = port };
                break;

            case Enum enumValue:
                var enumType = enumValue.GetType();
                if (!_enumCache.TryGetValue(enumType, out var enumData))
                {
                    enumData.Names = Enum.GetNames(enumType);
                    enumData.Values = Enum.GetValues(enumType);

                    _enumCache[enumType] = enumData;
                }

                var index = Array.IndexOf(enumData.Values, enumValue);
                if (ImGui.Combo("", ref index, enumData.Names, enumData.Names.Length))
                {
                    field.Value = enumData.Values.GetValue(index)!;
                }

                break;

            default:
                // For other types, just display as string
                Log.ZLogWarning($"Unsupported config type: {field.Type}");
                ImGui.TextDisabled($"{field.Value}");
                break;
        }

        ImGui.PopFont();
    }

    // Helper method to check if any children in the tree match the search
    private bool ChildrenMatchSearch(Dictionary<string, object> tree)
    {
        if (!IsFiltering) return true;

        foreach (var value in tree.Values)
        {
            switch (value)
            {
                case Configurable configurable:
                    if (configurable.Entries.Any(field => _filter.PassFilter(field.Name)))
                        return true;
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
                    count += configurable.Entries.Count(field => _filter.PassFilter(field.Name));
                    break;
                case Dictionary<string, object> subTree:
                    count += CountMatchingFields(subTree);
                    break;
            }
        }

        return count;
    }
}