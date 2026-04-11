using System.Runtime.InteropServices;
using System.Text;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.OpenGL3;
using Hexa.NET.ImGui.Backends.SDL3;
using Hexa.NET.ImPlot;

namespace Tyr.Gui.Backend;

internal unsafe class ImGuiController : IDisposable
{
    private readonly SdlWindow _window;
    private readonly ImGuiContextPtr _imguiCtx;
    private readonly ImPlotContextPtr _plotCtx;

    public ImGuiController(SdlWindow window)
    {
        _window = window;
        _imguiCtx = ImGui.CreateContext();

        ImGui.SetCurrentContext(_imguiCtx);
        ImPlot.SetImGuiContext(_imguiCtx);

        _plotCtx = ImPlot.CreateContext();
        ImPlot.SetCurrentContext(_plotCtx);
        ImPlot.StyleColorsDark(ImPlot.GetStyle());

        var io = ImGui.GetIO();
        io.IniFilename = null; // disable file-based save/load; we manage ini ourselves
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
        io.ConfigViewportsNoAutoMerge = false;
        io.ConfigViewportsNoTaskBarIcon = false;
    }

    public bool WantsIniSave => ImGui.GetIO().WantSaveIniSettings;

    public void LoadIniFromMemory(string ini)
    {
        var bytes = Encoding.UTF8.GetBytes(ini);
        fixed (byte* ptr = bytes)
            ImGui.LoadIniSettingsFromMemory(ptr, (nuint)bytes.Length);
    }

    public string SaveIniToMemory()
    {
        var ptr = ImGui.SaveIniSettingsToMemory();
        ImGui.GetIO().WantSaveIniSettings = false;
        return Marshal.PtrToStringUTF8((nint)ptr) ?? "";
    }

    public void InitializeBackends()
    {
        ImGui.SetCurrentContext(_imguiCtx);
        ImPlot.SetImGuiContext(_imguiCtx);
        ImPlot.SetCurrentContext(_plotCtx);

        ImGuiImplSDL3.SetCurrentContext(_imguiCtx);
        if (!ImGuiImplSDL3.InitForOpenGL(
                new SDLWindowPtr((SDLWindow*)_window.Handle.Handle),
                (void*)_window.GlContext.Handle))
        {
            throw new Exception("Failed to init SDL3 ImGui backend");
        }

        ImGuiImplOpenGL3.SetCurrentContext(_imguiCtx);
        if (!ImGuiImplOpenGL3.Init((byte*)null))
            throw new Exception("Failed to init OpenGL3 ImGui backend");
    }

    public void NewFrame()
    {
        _window.MakeContextCurrent();

        foreach (var e in _window.Events)
        {
            var evt = e;
            ImGuiImplSDL3.ProcessEvent((SDLEvent*)&evt);
        }

        ImGuiImplOpenGL3.NewFrame();
        ImGuiImplSDL3.NewFrame();
        ImGui.NewFrame();

        ImGui.DockSpaceOverViewport(DefaultLayout.DockSpaceId);
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
        ImGuiImplSDL3.Shutdown();

        ImPlot.SetCurrentContext(null);
        ImPlot.SetImGuiContext(null);
        ImPlot.DestroyContext(_plotCtx);

        ImGui.SetCurrentContext(null);
        ImGui.DestroyContext(_imguiCtx);
    }
}
