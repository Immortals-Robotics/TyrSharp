using System.Collections;
using System.Numerics;
using System.Reflection;
using Hexa.NET.ImGui;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Math;
using Tyr.Common.Network;
using Tyr.Common.Time;

namespace Tyr.Gui.Views;

public partial class ConfigsView
{
    private (bool, object?) DrawFieldEditor(object? obj)
    {
        return obj switch
        {
            int intValue => (DrawFieldEditorInt(ref intValue), intValue),
            float floatValue => (DrawFieldEditorFloat(ref floatValue), floatValue),
            double doubleValue => (DrawFieldEditorDouble(ref doubleValue), doubleValue),
            bool boolValue => (DrawFieldEditorBool(ref boolValue), boolValue),
            Vector2 vector2Value => (DrawFieldEditorVector2(ref vector2Value), vector2Value),
            Vector3 vector3Value => (DrawFieldEditorVector3(ref vector3Value), vector3Value),
            Vector4 vector4Value => (DrawFieldEditorVector4(ref vector4Value), vector4Value),
            string stringValue => (DrawFieldEditorString(ref stringValue), stringValue),
            Address addressValue => (DrawFieldEditorAddress(ref addressValue), addressValue),
            Enum enumValue => (DrawFieldEditorEnum(ref enumValue), enumValue),
            Color colorValue => (DrawFieldEditorColor(ref colorValue), colorValue),
            Angle angleValue => (DrawFieldEditorAngle(ref angleValue), angleValue),
            DeltaTime deltaTimeValue => (DrawFieldEditorDeltaTime(ref deltaTimeValue), deltaTimeValue),
            Timestamp timeValue => (DrawFieldEditorTime(ref timeValue), timeValue),
            IDictionary dictionaryValue => (DrawFieldEditorDictionary(ref dictionaryValue), dictionaryValue),
            IList listValue => (DrawFieldEditorList(ref listValue), listValue),
            _ => (DrawFieldEditorObject(obj), obj)
        };
    }

    private bool DrawFieldEditorObject(object? obj)
    {
        var type = obj?.GetType();
        if (type == null || obj == null)
        {
            ImGui.TextDisabled("null");
            return false;
        }

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var dirty = false;

        if (ImGui.CollapsingHeader(type.Name))
        {
            if (ImGui.BeginTable("props", 2, ImGuiTableFlags.BordersH))
            {
                ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthStretch, 0.4f);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 0.6f);

                foreach (var prop in props)
                {
                    if (!prop.CanRead || !prop.CanWrite) continue;

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(prop.Name);

                    ImGui.TableNextColumn();
                    ImGui.PushID(prop.Name);
                    var value = prop.GetValue(obj);
                    var (changed, newValue) = DrawFieldEditor(value);
                    if (changed)
                    {
                        prop.SetValue(obj, newValue);
                        dirty = true;
                    }

                    ImGui.PopID();
                }

                ImGui.EndTable();
            }
        }

        return dirty;
    }

    private bool DrawFieldEditorDictionary(ref IDictionary dictionary)
    {
        var keyType = dictionary.GetType().GetGenericArguments()[0].Name;
        var valueType = dictionary.GetType().GetGenericArguments()[1].Name;

        ImGui.PushItemFlag((ImGuiItemFlags)ImGuiItemFlagsPrivate.Disabled, false);
        var open = ImGui.CollapsingHeader($"{keyType} -> {valueType} [{dictionary.Count}]");
        ImGui.PopItemFlag();

        var dirty = false;

        if (open)
        {
            if (ImGui.BeginTable("fields", 2, ImGuiTableFlags.BordersH))
            {
                ImGui.TableSetupColumn("Key", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                ImGui.TableHeadersRow();

                foreach (DictionaryEntry entry in dictionary)
                {
                    var key = entry.Key.ToString() ?? string.Empty;

                    ImGui.PushID(key);
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.Text(key);

                    ImGui.TableNextColumn();
                    var (changed, value) = DrawFieldEditor(entry.Value);
                    if (changed)
                    {
                        dirty = true;
                        dictionary[entry.Key] = value;
                    }

                    ImGui.PopID();
                }

                ImGui.EndTable();
            }
        }

        return dirty;
    }

    private bool DrawFieldEditorList(ref IList list)
    {
        var dirty = false;

        var elementType = list.GetType().IsArray
            ? list.GetType().GetElementType()?.Name
            : list.GetType().GetGenericArguments().FirstOrDefault()?.Name ?? "unknown";

        ImGui.PushItemFlag((ImGuiItemFlags)ImGuiItemFlagsPrivate.Disabled, false);

        if (ImGui.CollapsingHeader($"{elementType} [{list.Count}]"))
        {
            if (ImGui.BeginTable("array", 2, ImGuiTableFlags.BordersH))
            {
                ImGui.TableSetupColumn("Index", ImGuiTableColumnFlags.WidthFixed, ImGui.GetFontSize() * 2);
                ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch, 1f);

                for (var i = 0; i < list.Count; i++)
                {
                    ImGui.PushID(i);
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.Text(i.ToString());

                    ImGui.TableNextColumn();
                    var (changed, value) = DrawFieldEditor(list[i]);
                    if (changed)
                    {
                        dirty = true;
                        list[i] = value;
                    }

                    ImGui.PopID();
                }

                ImGui.EndTable();
            }
        }

        ImGui.PopItemFlag();

        return dirty;
    }

    private static bool DrawFieldEditorTime(ref Timestamp value)
    {
        var ns = value.Nanoseconds;
        unsafe
        {
            var nsPtr = &ns;
            ImGui.InputScalar("", ImGuiDataType.S64, nsPtr);
        }

        var dirty = ImGui.IsItemDeactivatedAfterEdit();
        if (dirty)
        {
            value = Timestamp.FromNanoseconds(ns);
        }

        ImGui.SameLine();
        ImGui.Text("ns");

        return dirty;
    }

    private static bool DrawFieldEditorDeltaTime(ref DeltaTime value)
    {
        var seconds = value.Seconds;
        ImGui.InputDouble("", ref seconds);

        var dirty = ImGui.IsItemDeactivatedAfterEdit();
        if (dirty)
        {
            value = DeltaTime.FromSeconds(seconds);
        }

        ImGui.SameLine();
        ImGui.Text("s");

        return dirty;
    }

    private static bool DrawFieldEditorAngle(ref Angle value)
    {
        var radians = value.Rad;
        ImGui.SliderAngle("", ref radians);

        var dirty = ImGui.IsItemEdited();
        if (dirty)
        {
            value = Angle.FromRad(radians);
        }

        return dirty;
    }

    private static bool DrawFieldEditorColor(ref Color value)
    {
        var colorVec = value.RGBA;
        ImGui.ColorEdit4("", ref colorVec,
            ImGuiColorEditFlags.DisplayHex | ImGuiColorEditFlags.AlphaBar);

        var dirty = ImGui.IsItemEdited();
        if (dirty)
        {
            value = new Color(colorVec);
        }

        return dirty;
    }

    private bool DrawFieldEditorEnum(ref Enum value)
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

        var dirty = ImGui.IsItemEdited();
        if (dirty)
        {
            value = (Enum)enumData.Values.GetValue(index)!;
        }

        return dirty;
    }

    private static bool DrawFieldEditorAddress(ref Address value)
    {
        var dirty = false;
        var ip = value.Ip;
        var port = value.Port;
        ImGui.InputText("##ip", ref ip, 256, ImGuiInputTextFlags.CharsDecimal);
        dirty |= ImGui.IsItemDeactivatedAfterEdit();
        ImGui.SameLine();
        ImGui.Text(":");
        ImGui.SameLine();
        ImGui.InputInt("##port", ref port);
        dirty |= ImGui.IsItemDeactivatedAfterEdit();

        if (dirty)
        {
            value = new Address { Ip = ip, Port = port };
        }

        return dirty;
    }

    private static bool DrawFieldEditorString(ref string value)
    {
        ImGui.InputText("", ref value, 256);
        return ImGui.IsItemDeactivatedAfterEdit();
    }

    private static bool DrawFieldEditorVector4(ref Vector4 value)
    {
        ImGui.InputFloat4("", ref value);
        return ImGui.IsItemDeactivatedAfterEdit();
    }

    private static bool DrawFieldEditorVector3(ref Vector3 value)
    {
        ImGui.InputFloat3("", ref value);
        return ImGui.IsItemDeactivatedAfterEdit();
    }

    private static bool DrawFieldEditorVector2(ref Vector2 value)
    {
        ImGui.InputFloat2("", ref value);
        return ImGui.IsItemDeactivatedAfterEdit();
    }

    private static bool DrawFieldEditorBool(ref bool value)
    {
        ImGui.Checkbox("", ref value);
        return ImGui.IsItemDeactivatedAfterEdit();
    }

    private static bool DrawFieldEditorDouble(ref double value)
    {
        ImGui.InputDouble("", ref value);
        return ImGui.IsItemDeactivatedAfterEdit();
    }

    private static bool DrawFieldEditorFloat(ref float value)
    {
        ImGui.InputFloat("", ref value);
        return ImGui.IsItemDeactivatedAfterEdit();
    }

    private static bool DrawFieldEditorInt(ref int value)
    {
        ImGui.InputInt("", ref value);
        return (ImGui.IsItemEdited() && ImGui.IsItemClicked()) // changed using the +/- buttons
               || ImGui.IsItemDeactivatedAfterEdit(); // or using the text field
    }
}