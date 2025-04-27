using System.Numerics;
using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Network;
using Tyr.Gui.Backend;

namespace Tyr.Gui.Views;

public class ConfigsView
{
    private string _searchText = string.Empty;
    private bool IsFiltering => !string.IsNullOrWhiteSpace(_searchText);

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

                ImGui.TextDisabled($"{visibleItems} of {totalItems} items matching");
            }
        }

        ImGui.End();
    }

    private void DrawSearchBar()
    {
        // Start with a search icon
        ImGui.Text($"{IconFonts.FontAwesome6.MagnifyingGlass}");
        ImGui.SameLine();

        ImGui.PushItemWidth(-24); // Make space for the clear button
        ImGui.InputTextWithHint("##search", "Search configs...", ref _searchText, 256);
        ImGui.PopItemWidth();

        // Clear button
        if (IsFiltering)
        {
            ImGui.SameLine();
            if (ImGui.Button($"{IconFonts.FontAwesome6.Xmark}##clear"))
            {
                _searchText = string.Empty;
            }
        }

        ImGui.Separator();
    }

    private void DrawTree(Dictionary<string, object> tree)
    {
        foreach (var (key, value) in tree)
        {
            switch (value)
            {
                case Configurable configurable:
                    // Check if any of its fields match the search
                    var fieldsMatch = configurable.Entries.Any(field => MatchesSearch(field.Name));
                    if (fieldsMatch)
                    {
                        DrawConfigurable(key, configurable);
                    }

                    break;

                case Dictionary<string, object> subTree:
                {
                    if (ChildrenMatchSearch(subTree))
                    {
                        if (ImGui.TreeNodeEx(key, IsFiltering ? ImGuiTreeNodeFlags.DefaultOpen : 0))
                        {
                            DrawTree(subTree);
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
        var shouldOpen = IsFiltering && configurable.Entries.Any(field => MatchesSearch(field.Name));

        var nodeOpen = ImGui.TreeNodeEx($"{IconFonts.FontAwesome6.Gears} {name}",
            shouldOpen ? ImGuiTreeNodeFlags.DefaultOpen : 0);

        // type name
        ImGui.SameLine();
        ImGui.PushFont(FontRegistry.Instance.MonoFont);
        ImGui.TextDisabled($" : {configurable.TypeName}");
        ImGui.PopFont();

        if (nodeOpen)
        {
            if (ImGui.BeginTable("fields", 3, ImGuiTableFlags.BordersInnerH))
            {
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                ImGui.TableSetupColumn("Reset", ImGuiTableColumnFlags.WidthFixed, 15f);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 1.5f);

                foreach (var field in configurable.Entries)
                {
                    // Only show fields that match the search criteria when filtering
                    if (!MatchesSearch(field.Name)) continue;

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
            ImGui.TextDisabled($"{field.Type.FullName}");
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
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.2f, 1.0f), $"{field.DefaultValue}");
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

                field.Value = new Address{Ip = ip, Port = port};
                break;

            case Enum enumValue:
                var names = Enum.GetNames(enumValue.GetType());
                var values = Enum.GetValues(enumValue.GetType());
                var index = Array.IndexOf(values, enumValue);
                if (ImGui.Combo("", ref index, names, names.Length))
                {
                    field.Value = values.GetValue(index)!;
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

        foreach (var (key, value) in tree)
        {
            switch (value)
            {
                case Configurable configurable:
                    if (configurable.Entries.Any(field => MatchesSearch(field.Name)))
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
        foreach (var (key, value) in tree)
        {
            switch (value)
            {
                case Configurable configurable:
                    count += configurable.Entries.Count(field => MatchesSearch(field.Name));
                    break;
                case Dictionary<string, object> subTree:
                    count += CountMatchingFields(subTree);
                    break;
            }
        }

        return count;
    }


    // Helper method to check if a string matches the search text
    private bool MatchesSearch(string text)
    {
        if (!IsFiltering) return true;
        if (string.IsNullOrEmpty(text)) return false;

        return ImGui.ImGuiTextFilter(_searchText).PassFilter(text);
    }
}