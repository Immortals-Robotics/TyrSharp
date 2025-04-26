using System.Numerics;
using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Network;
using Tyr.Gui.Backend;

namespace Tyr.Gui.Views;

public class ConfigsView
{
    // Search field buffer
    private string _searchText = string.Empty;

    // Flag to track if we're filtering
    private bool IsFiltering => !string.IsNullOrWhiteSpace(_searchText);

    public void Draw()
    {
        if (ImGui.Begin("Configs"))
        {
            // Add search bar at the top
            DrawSearchBar();

            // Draw the filtered tree
            DrawTree(Registry.Tree);
        }

        ImGui.End();
    }

    private void DrawSearchBar()
    {
        // Create a search input with hint text
        ImGui.PushItemWidth(-1); // Make the input field fill the available width
        ImGui.InputTextWithHint("##search", "Search configs...", ref _searchText, 256);
        ImGui.PopItemWidth();

        ImGui.Separator();
    }

    private void DrawTree(Dictionary<string, object> tree)
    {
        foreach (var (key, value) in tree)
        {
            var keyMatch = MatchesSearch(key);

            switch (value)
            {
                case Configurable configurable:
                    // Check if any of its fields match the search
                    var fieldsMatch = configurable.Entries.Any(field => MatchesSearch(field.Name));

                    if (keyMatch || fieldsMatch)
                    {
                        DrawConfigurable(key, configurable);
                    }

                    break;

                case Dictionary<string, object> subTree:
                {
                    var childrenMatch = ChildrenMatchSearch(subTree);

                    if (keyMatch || childrenMatch)
                    {
                        if (ImGui.TreeNodeEx(key, IsFiltering && childrenMatch ? ImGuiTreeNodeFlags.DefaultOpen : 0))
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

        if (ImGui.TreeNodeEx($"{name} ({configurable.TypeName})",
                shouldOpen ? ImGuiTreeNodeFlags.DefaultOpen : 0))
        {
            if (ImGui.BeginTable("fields", 3, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("R", ImGuiTableColumnFlags.WidthFixed, 15f);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1.0f);
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

        // Reset button
        ImGui.TableNextColumn();
        if (ImGui.SmallButton($"{IconFonts.FontAwesome6.RotateLeft}"))
        {
            field.Value = field.DefaultValue;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.PushFont(FontRegistry.Instance.MonoFont);
            ImGui.SetTooltip($"{field.DefaultValue}");
            ImGui.PopFont();
        }

        // Name
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(field.Name);

        if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(field.Comment))
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(field.Comment);
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
                ImGui.InputText("##ip", ref ip, 256);
                ImGui.SameLine();
                ImGui.Text(":");
                ImGui.SameLine();
                ImGui.InputInt("##port", ref port);

                field.Value = addressValue;
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
            if (MatchesSearch(key)) return true;

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

    // Helper method to check if a string matches the search text
    private bool MatchesSearch(string text)
    {
        if (!IsFiltering) return true;

        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(_searchText))
            return false;

        return text.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }
}