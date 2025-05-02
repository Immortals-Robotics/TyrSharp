using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Math;
using Tyr.Common.Network;
using Tyr.Common.Time;

namespace Tyr.Gui.Views;

public partial class ConfigsView
{
    private static void DrawFieldEditorTime(ConfigEntry field, Timestamp value)
    {
        var ns = value.Nanoseconds;
        unsafe
        {
            var nsPtr = &ns;
            ImGui.InputScalar("", ImGuiDataType.S64, nsPtr);
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            field.Value = Timestamp.FromNanoseconds(ns);
        }

        ImGui.SameLine();
        ImGui.Text("ns");
    }

    private static void DrawFieldEditorDeltaTime(ConfigEntry field, DeltaTime value)
    {
        var seconds = value.Seconds;
        ImGui.InputDouble("", ref seconds);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            field.Value = DeltaTime.FromSeconds(seconds);
        }

        ImGui.SameLine();
        ImGui.Text("s");
    }

    private static void DrawFieldEditorAngle(ConfigEntry field, Angle value)
    {
        var radians = value.Rad;
        ImGui.SliderAngle("", ref radians);
        if (ImGui.IsItemEdited())
        {
            field.Value = Angle.FromRad(radians);
        }
    }

    private static void DrawFieldEditorColor(ConfigEntry field, Color value)
    {
        var colorVec = value.RGBA;
        ImGui.ColorEdit4("", ref colorVec,
            ImGuiColorEditFlags.DisplayHex | ImGuiColorEditFlags.AlphaBar);
        if (ImGui.IsItemEdited())
        {
            field.Value = new Color(colorVec);
        }
    }

    private void DrawFieldEditorEnum(ConfigEntry field, Enum value)
    {
        var enumType = value.GetType();
        if (!_enumCache.TryGetValue(enumType, out var enumData))
        {
            enumData.Names = Enum.GetNames(enumType);
            enumData.Values = Enum.GetValues(enumType);

            _enumCache[enumType] = enumData;
        }

        var index = Array.IndexOf(enumData.Values, value);
        ImGui.Combo("", ref index, enumData.Names, enumData.Names.Length);
        if (ImGui.IsItemEdited())
        {
            field.Value = enumData.Values.GetValue(index)!;
        }
    }

    private static void DrawFieldEditorAddress(ConfigEntry field, Address value)
    {
        var changed = false;
        var ip = value.Ip;
        var port = value.Port;
        ImGui.InputText("##ip", ref ip, 256, ImGuiInputTextFlags.CharsDecimal);
        changed |= ImGui.IsItemDeactivatedAfterEdit();
        ImGui.SameLine();
        ImGui.Text(":");
        ImGui.SameLine();
        ImGui.InputInt("##port", ref port);
        changed |= ImGui.IsItemDeactivatedAfterEdit();

        if (changed)
        {
            field.Value = new Address { Ip = ip, Port = port };
        }
    }

    private static void DrawFieldEditorString(ConfigEntry field, string value)
    {
        ImGui.InputText("", ref value, 256);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            field.Value = value;
        }
    }

    private static void DrawFieldEditorBool(ConfigEntry field, bool value)
    {
        ImGui.Checkbox("", ref value);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            field.Value = value;
        }
    }

    private static void DrawFieldEditorDouble(ConfigEntry field, double value)
    {
        ImGui.InputDouble("", ref value);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            field.Value = value;
        }
    }

    private static void DrawFieldEditorFloat(ConfigEntry field, float value)
    {
        ImGui.InputFloat("", ref value);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            field.Value = value;
        }
    }

    private static void DrawFieldEditorInt(ConfigEntry field, int value)
    {
        ImGui.InputInt("", ref value);
        if ((ImGui.IsItemEdited() && ImGui.IsItemClicked()) // changed using the +/- buttons
            || ImGui.IsItemDeactivatedAfterEdit()) // or using the text field
        {
            field.Value = value;
        }
    }
}