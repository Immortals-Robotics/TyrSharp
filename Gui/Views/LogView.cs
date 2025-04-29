using System.Globalization;
using Hexa.NET.ImGui;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;

namespace Tyr.Gui.Views;

public class LogView(DebugFramer debugFramer, DebugFilter filter)
{
    public void Draw(PlaybackTime time)
    {
        if (ImGui.Begin($"{IconFonts.FontAwesome6.Terminal} Logs"))
        {
            const ImGuiTableFlags flags = ImGuiTableFlags.Resizable | ImGuiTableFlags.Hideable |
                                          ImGuiTableFlags.HighlightHoveredColumn |
                                          ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH;

            if (ImGui.BeginTable("logs", 6, flags))
            {
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthStretch, 1.5f);
                ImGui.TableSetupColumn("Module", ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn("Level", ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn("File", ImGuiTableColumnFlags.WidthStretch, 2f);
                ImGui.TableSetupColumn("Function", ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn("Text", ImGuiTableColumnFlags.WidthStretch, 10f);

                ImGui.TableHeadersRow();

                ImGui.PushFont(FontRegistry.Instance.MonoFont);

                foreach (var (module, framer) in debugFramer.Modules)
                {
                    if (!filter.IsEnabled(module)) continue;

                    var frame = time.Live ? framer.LatestFrame : framer.GetFrame(time.Time);
                    if (frame == null) continue;

                    foreach (var log in frame.Logs)
                    {
                        if (!filter.IsEnabled(log.Meta)) continue;

                        ImGui.TableNextRow();

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(log.Timestamp.ToDateTime()
                            .ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(log.Meta.ModuleName);

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(log.Level.ToString());

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted($"{Path.GetFileName(log.Meta.FilePath)}:{log.Meta.LineNumber}");

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(log.Meta.MemberName);

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(log.Message);
                    }
                }

                ImGui.PopFont();

                ImGui.EndTable();
            }
        }

        ImGui.End();
    }
}