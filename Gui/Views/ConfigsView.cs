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
            // Draw configurable fields
            foreach (var field in configurable.Entries)
            {
                DrawField(field);
            }

            ImGui.TreePop();
        }
    }

    private void DrawField(ConfigEntry field)
    {
        ImGui.PushID(field.Name);
        
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

        ImGui.SameLine();

        // Draw appropriate editor based on field type
        DrawFieldEditor(field);

        if (!string.IsNullOrEmpty(field.Comment))
        {
            ImGui.SameLine();

            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(field.Comment);
                ImGui.EndTooltip();
            }
        }
        
        ImGui.PopID();
    }

    private void DrawFieldEditor(ConfigEntry field)
    {
        // This is a simplified version - you would need to implement proper editors
        // for each type that ConfigurableField can represent
        switch (field.Value)
        {
            case int intValue:
                if (ImGui.InputInt(field.Name, ref intValue))
                {
                    field.Value = (intValue);
                }

                break;

            case float floatValue:
                if (ImGui.InputFloat(field.Name, ref floatValue))
                {
                    field.Value = (floatValue);
                }

                break;

            case double doubleValue:
                var tmpValue = (float)doubleValue;
                if (ImGui.InputFloat(field.Name, ref tmpValue))
                {
                    field.Value = ((double)tmpValue);
                }

                break;

            case bool boolValue:
                if (ImGui.Checkbox(field.Name, ref boolValue))
                {
                    field.Value = (boolValue);
                }

                break;

            case string stringValue:
                if (ImGui.InputText(field.Name, ref stringValue, 256))
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
                ImGui.SameLine();
                ImGui.TextUnformatted(field.Name);
                
                field.Value = addressValue;
                break;

            default:
                // For other types, just display as string
                ImGui.TextDisabled($"{field.Name}: {field.Value}");
                break;
        }
    }
}