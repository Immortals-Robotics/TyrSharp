using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Network;
using Tyr.Gui.Backend;

namespace Tyr.Gui.Views;

public class ConfigsView
{
    public void Draw()
    {
        if (ImGui.Begin("Configs"))
        {
            DrawTree(Registry.ToDictionary());
        }

        ImGui.End();
    }

    private void DrawTree(Dictionary<string, object> tree)
    {
        foreach (var (key, value) in tree)
        {
            if (value is Configurable configurable)
            {
                DrawConfigurable(key, configurable);
            }
            else if (value is Dictionary<string, object> subTree)
            {
                if (ImGui.TreeNode(key))
                {
                    DrawTree(subTree);
                    ImGui.TreePop();
                }
            }
        }
    }

    private void DrawConfigurable(string name, Configurable configurable)
    {
        if (ImGui.TreeNode($"{name} ({configurable.TypeName})"))
        {
            if (ImGui.BeginTable("fields", 3, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("R", ImGuiTableColumnFlags.WidthFixed, 15f);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                
                // Draw configurable fields
                foreach (var field in configurable.Entries)
                {
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

        ImGui.TableNextColumn();
        ImGui.PushFont(FontRegistry.Instance.IconFont);
        if (ImGui.SmallButton($"{IconFonts.FontAwesome6.RotateLeft}"))
        {
            field.Value = field.DefaultValue;
        }
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip($"{field.DefaultValue}");
        }

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(field.Name);
        if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(field.Comment))
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(field.Comment);
            ImGui.EndTooltip();
        }
        
        
        ImGui.TableNextColumn();
        
        // Draw appropriate editor based on field type
        DrawFieldEditor(field);
        
        ImGui.PopID();
    }

    private void DrawFieldEditor(ConfigEntry field)
    {
        // This is a simplified version - you would need to implement proper editors
        // for each type that ConfigurableField can represent
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

            default:
                // For other types, just display as string
                ImGui.TextDisabled($"{field.Value}");
                break;
        }
    }
}