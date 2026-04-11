using Hexa.NET.ImGui;
using Tyr.Gui.Data;
using Tyr.Gui.Views;

namespace Tyr.Gui.Backend;

internal static unsafe class DefaultLayout
{
    public const uint DockSpaceId = 0x08BD597D;

    private const float RightColumnRatio = 906f / 2832f;
    private const float PlaybackRowRatio = 59f / 1521f;
    private const float BottomPaneRatio = 479f / (1521f - 59f);
    private const float RightBottomPaneRatio = 575f / 1521f;

    public static void Apply()
    {
        var viewport = ImGui.GetMainViewport();
        var viewportPos = viewport.Pos;
        var viewportSize = viewport.Size;

        ImGuiP.DockBuilderRemoveNode(DockSpaceId);
        ImGuiP.DockBuilderAddNode(DockSpaceId);
        ImGuiP.DockBuilderSetNodePos(DockSpaceId, viewportPos);
        ImGuiP.DockBuilderSetNodeSize(DockSpaceId, viewportSize);

        uint leftNodeId = DockSpaceId;
        uint rightNodeId = 0;
        ImGuiP.DockBuilderSplitNode(DockSpaceId, ImGuiDir.Right, RightColumnRatio, &rightNodeId, &leftNodeId);

        uint playbackNodeId = 0;
        uint leftContentNodeId = 0;
        ImGuiP.DockBuilderSplitNode(leftNodeId, ImGuiDir.Up, PlaybackRowRatio, &playbackNodeId, &leftContentNodeId);

        uint fieldNodeId = 0;
        uint bottomNodeId = 0;
        ImGuiP.DockBuilderSplitNode(leftContentNodeId, ImGuiDir.Down, BottomPaneRatio, &bottomNodeId, &fieldNodeId);

        uint rightTopNodeId = 0;
        uint rightBottomNodeId = 0;
        ImGuiP.DockBuilderSplitNode(rightNodeId, ImGuiDir.Down, RightBottomPaneRatio, &rightBottomNodeId, &rightTopNodeId);

        ImGuiP.DockBuilderDockWindow(PlaybackControl.WindowTitle, playbackNodeId);
        ImGuiP.DockBuilderDockWindow(SslLogPlayerView.WindowTitle, playbackNodeId);
        
        ImGuiP.DockBuilderDockWindow(FieldView.WindowTitle, fieldNodeId);
        ImGuiP.DockBuilderDockWindow(LogView.WindowTitle, bottomNodeId);
        ImGuiP.DockBuilderDockWindow(PlotView.WindowTitle, bottomNodeId);

        ImGuiP.DockBuilderDockWindow(ConfigsView.WindowTitle, rightTopNodeId);
        ImGuiP.DockBuilderDockWindow(DebugFilter.WindowTitle, rightTopNodeId);

        ImGuiP.DockBuilderDockWindow(GameControllerView.WindowTitle, rightBottomNodeId);
        ImGuiP.DockBuilderDockWindow(SessionsView.WindowTitle, rightBottomNodeId);
        ImGuiP.DockBuilderDockWindow(RobotStatusView.WindowTitle, rightBottomNodeId);
        ImGuiP.DockBuilderDockWindow(RobotDebugView.WindowTitle, rightBottomNodeId);

        ImGuiP.DockBuilderFinish(DockSpaceId);
    }
}
