using Cysharp.Text;
using Hexa.NET.ImGui;
using Microsoft.Extensions.Logging;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Debug = Tyr.Common.Debug;

namespace Tyr.Gui.Views;

public sealed class LogView(DebugFramer debugFramer, DebugFilter filter) : IDisposable
{
    private Utf8ValueStringBuilder _stringBuilder = ZString.CreateUtf8StringBuilder();

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
                        _stringBuilder.Clear();
                        _stringBuilder.AppendFormat("{0:D2}:{1:D2}:{2:D2}.{3:D3}\0",
                            (int)log.Timestamp.NormalizedHours,
                            (int)log.Timestamp.NormalizedMinutes,
                            (int)log.Timestamp.NormalizedSeconds,
                            (int)log.Timestamp.NormalizedMilliseconds);
                        ImGui.TextUnformatted(_stringBuilder.AsSpan());

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(log.Meta.ModuleName);

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(Debug.EnumCache<LogLevel>.GetName(log.Level));

                        ImGui.TableNextColumn();
                        _stringBuilder.Clear();
                        _stringBuilder.AppendFormat("{0}: {1}\0",
                            Debug.PathCache.GetFileName(log.Meta.FilePath), log.Meta.LineNumber);
                        ImGui.TextUnformatted(_stringBuilder.AsSpan());

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

    public void Dispose()
    {
        _stringBuilder.Dispose();
    }
}