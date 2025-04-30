using Cysharp.Text;
using Hexa.NET.ImGui;
using Microsoft.Extensions.Logging;
using Tyr.Common.Debug.Drawing;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Debug = Tyr.Common.Debug;

namespace Tyr.Gui.Views;

public sealed class LogView(DebugFramer debugFramer, DebugFilter filter) : IDisposable
{
    private Utf8ValueStringBuilder _stringBuilder = ZString.CreateUtf8StringBuilder();

    public void Draw(PlaybackTime time)
    {
        if (ImGui.Begin($"{IconFonts.FontAwesome6.Terminal} Logs", ImGuiWindowFlags.AlwaysVerticalScrollbar))
        {
            const ImGuiTableFlags flags = ImGuiTableFlags.Resizable | ImGuiTableFlags.Hideable | ImGuiTableFlags.Reorderable |
                                          ImGuiTableFlags.HighlightHoveredColumn |
                                          ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH;

            if (ImGui.BeginTable("logs", 7, flags))
            {
                ImGui.TableSetupColumn("Icon",
                    ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHide |
                    ImGuiTableColumnFlags.NoHeaderLabel | ImGuiTableColumnFlags.NoResize,
                    ImGui.GetFontSize());
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthStretch, 1.5f);
                ImGui.TableSetupColumn("Module", ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn("Level", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultHide, 1f);
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
                        if (string.IsNullOrWhiteSpace(log.Message)) continue;

                        ImGui.TableNextRow();

                        var color = log.Level switch
                        {
                            LogLevel.Trace => Color.BlueGrey100,
                            LogLevel.Debug => Color.TealA100,
                            LogLevel.Information => Color.LightBlueA100,
                            LogLevel.Warning => Color.YellowA200,
                            LogLevel.Error => Color.RedA200,
                            LogLevel.Critical => Color.PurpleA100,
                            _ => Color.White,
                        };
                        ImGui.PushStyleColor(ImGuiCol.Text, color.RGBA);

                        ImGui.TableNextColumn();
                        var icon = log.Level switch
                        {
                            LogLevel.Trace => IconFonts.FontAwesome6.UserSecret,
                            LogLevel.Debug => IconFonts.FontAwesome6.Bug,
                            LogLevel.Information => IconFonts.FontAwesome6.CircleExclamation,
                            LogLevel.Warning => IconFonts.FontAwesome6.TriangleExclamation,
                            LogLevel.Error => IconFonts.FontAwesome6.Radiation,
                            LogLevel.Critical => IconFonts.FontAwesome6.SkullCrossbones,
                            _ => IconFonts.FontAwesome6.Question,
                        };

                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetFontSize() / 4f);
                        ImGui.TextUnformatted(icon);

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

                        ImGui.PopStyleColor();
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