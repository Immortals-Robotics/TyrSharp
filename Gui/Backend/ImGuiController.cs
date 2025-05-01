using System.Runtime.CompilerServices;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.GLFW;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Hexa.NET.ImPlot;

namespace Tyr.Gui.Backend;

internal class ImGuiController : IDisposable
{
    private readonly GlfwWindow _window;
    private readonly ImGuiContextPtr _imguiCtx;
    private ImPlotContextPtr _plotCtx;

    public ImGuiController(GlfwWindow window)
    {
        _window = window;
        _imguiCtx = ImGui.CreateContext();

        ImGui.SetCurrentContext(_imguiCtx);
        ImPlot.SetImGuiContext(_imguiCtx);

        _plotCtx = ImPlot.CreateContext();
        ImPlot.SetCurrentContext(_plotCtx);
        ImPlot.StyleColorsDark(ImPlot.GetStyle());

        // Setup ImGui config.
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard; // Enable Keyboard Controls
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad; // Enable Gamepad Controls
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable; // Enable Docking
        io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable; // Enable Multi-Viewport / Platform Windows
        io.ConfigViewportsNoAutoMerge = false;
        io.ConfigViewportsNoTaskBarIcon = false;

        ImGuiImplGLFW.SetCurrentContext(_imguiCtx);
        if (!ImGuiImplGLFW.InitForOpenGL(
                Unsafe.BitCast<Hexa.NET.GLFW.GLFWwindowPtr, GLFWwindowPtr>(window.Handle),
                true))
        {
            throw new Exception("Failed Init GLFW ImGui");
        }

        ImGuiImplOpenGL3.SetCurrentContext(_imguiCtx);
        if (!ImGuiImplOpenGL3.Init("#version 150"))
            throw new Exception("Failed Init GL3 ImGui");
    }

    public void NewFrame()
    {
        _window.MakeContextCurrent();
        ImGuiImplOpenGL3.NewFrame();
        ImGuiImplGLFW.NewFrame();
        ImGui.NewFrame();

        ImGui.DockSpaceOverViewport();
    }

    public void Render()
    {
        ImGui.Render();
        ImGui.EndFrame();

        _window.MakeContextCurrent();
        ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());
        if ((ImGui.GetIO().ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            ImGui.UpdatePlatformWindows();
            ImGui.RenderPlatformWindowsDefault();
        }
    }

    public void Dispose()
    {
        ImGuiImplOpenGL3.Shutdown();
        ImGuiImplGLFW.Shutdown();

        ImPlot.SetCurrentContext(null);
        ImPlot.SetImGuiContext(null);

        ImPlot.DestroyContext(_plotCtx);

        ImGui.SetCurrentContext(null);
        ImGui.DestroyContext(_imguiCtx);
    }
}