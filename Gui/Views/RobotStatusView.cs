using Hexa.NET.ImGui;
using Tyr.Common.Data;
using Tyr.Common.Time;
using Tyr.Gui.Backend;
using Tyr.Soccer;
using System.Numerics;

namespace Tyr.Gui.Views;

public sealed class RobotStatusView : IDisposable
{
    public static readonly string WindowTitle = $"{IconFonts.FontAwesome6.Robot} Robot Status";

    public void Draw()
    {
        if (ImGui.Begin(WindowTitle))
        {
            DrawTeamTable(TeamColor.Yellow);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            DrawTeamTable(TeamColor.Blue);
        }

        ImGui.End();
    }

    private void DrawTeamTable(TeamColor color)
    {
        ImGui.TextUnformatted($"{color} Team");
        var robots = TeamRunner.GetOwnRobots(color);
        if (robots == null || robots.Count == 0)
        {
            ImGui.TextDisabled("No robots initialized.");
            return;
        }

        if (ImGui.BeginTable($"robot_status_{color}", 5,
                ImGuiTableFlags.Resizable | ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 30);
            ImGui.TableSetupColumn("Battery", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Last Update", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Ball", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableHeadersRow();

            bool anyRobot = false;
            var now = Timestamp.Now;
            Log.ZLogDebug($"Robot {robots} connected");

            int i = 0;
            foreach (var robot in robots)
            {
                var status = robot.HardwareStatus;
                Log.ZLogDebug($"Robot {status.RobotId} , {i}");
                i += 1;
                if (status.LastUpdate == Timestamp.Zero) continue;

                var diff = now - status.LastUpdate;
                if (diff.Seconds > 10.0) continue; // Hide disconnected robots

                anyRobot = true;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(status.RobotId.ToString());


                ImGui.TableNextColumn();
                if (status.Power != null)
                {
                    float voltage = status.Power.V24Voltage;
                    // Color battery based on voltage (rough estimate)
                    Vector4 batteryColor = voltage > 24.0f ? new Vector4(0.2f, 1.0f, 0.2f, 1.0f) :
                        voltage > 22.0f ? new Vector4(1.0f, 0.8f, 0.2f, 1.0f) :
                        new Vector4(1.0f, 0.2f, 0.2f, 1.0f);
                    ImGui.TextColored(batteryColor, $"{voltage:F2}V");
                }
                else
                {
                    ImGui.TextDisabled("N/A");
                }

                ImGui.TableNextColumn();
                if (diff.Seconds > 5.0)
                {
                    ImGui.TextColored(new Vector4(1.0f, 0.2f, 0.2f, 1.0f), $"{diff.Seconds:F1}s ago");
                }
                else
                {
                    ImGui.TextUnformatted($"{diff.Seconds:F2}s ago");
                }

                ImGui.TableNextColumn();
                if (status.IrSensor != null && status.IrSensor.Blocked)
                {
                    ImGui.TextColored(new Vector4(0.2f, 1.0f, 0.2f, 1.0f), IconFonts.FontAwesome6.Circle);
                }
            }

            if (!anyRobot)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TextDisabled("No connected robots.");
            }

            ImGui.EndTable();
        }
    }

    public void Dispose()
    {
    }
}