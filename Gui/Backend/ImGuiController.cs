using System.Runtime.CompilerServices;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.GLFW;
using Hexa.NET.ImGui.Backends.OpenGL3;

namespace Tyr.Gui.Backend;

internal class ImGuiController : IDisposable
{
    private readonly GlfwWindow _window;
    private readonly ImGuiContextPtr _ctx;

    public ImGuiController(GlfwWindow window)
    {
        _window = window;
        _ctx = ImGui.CreateContext();
        ImGui.SetCurrentContext(_ctx);

        // Setup ImGui config.
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard; // Enable Keyboard Controls
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad; // Enable Gamepad Controls
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable; // Enable Docking
        io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable; // Enable Multi-Viewport / Platform Windows
        io.ConfigViewportsNoAutoMerge = false;
        io.ConfigViewportsNoTaskBarIcon = true;

        ImGuiImplGLFW.SetCurrentContext(_ctx);
        if (!ImGuiImplGLFW.InitForOpenGL(
                Unsafe.BitCast<Hexa.NET.GLFW.GLFWwindowPtr, GLFWwindowPtr>(window.Handle),
                true))
        {
            throw new Exception("Failed Init GLFW ImGui");
        }

        ImGuiImplOpenGL3.SetCurrentContext(_ctx);
        if (!ImGuiImplOpenGL3.Init("#version 150"))
            throw new Exception("Failed Init GL3 ImGui");
    }

    public void NewFrame()
    {
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
        ImGui.DestroyContext(_ctx);
    }
}