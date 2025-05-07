using Hexa.NET.GLFW;
using Hexa.NET.OpenGL;
using Tyr.Common.Debug.Drawing;

namespace Tyr.Gui.Backend;

internal class GlfwWindow : IDisposable
{
    public GLFWwindowPtr Handle { get; }
    private readonly GL _gl;

    static GlfwWindow() => GLFW.Init();

    public GlfwWindow(string title, int width, int height, int? x = null, int? y = null, bool? maximized = null)
    {
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MAJOR, 3);
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MINOR, 2);
        GLFW.WindowHint(GLFW.GLFW_OPENGL_PROFILE, GLFW.GLFW_OPENGL_CORE_PROFILE); // 3.2+ only

        GLFW.WindowHint(GLFW.GLFW_FOCUSED, 1); // Make window focused on start
        GLFW.WindowHint(GLFW.GLFW_RESIZABLE, 1); // Make window resizable

        if (x.HasValue) GLFW.WindowHint(GLFW.GLFW_POSITION_X, x.Value);
        if (y.HasValue) GLFW.WindowHint(GLFW.GLFW_POSITION_Y, y.Value);
        if (maximized.HasValue)
        {
            GLFW.WindowHint(GLFW.GLFW_MAXIMIZED, maximized.Value ? GLFW.GLFW_TRUE : GLFW.GLFW_FALSE);
        }

        if (width == 0) width = 1280;
        if (height == 0) height = 720;

        Handle = GLFW.CreateWindow(width, height, title, null, null);
        if (Handle.IsNull) throw new Exception("GLFW window failed");
        MakeContextCurrent();
        _gl = new GL(new BindingsContext(Handle));
    }

    public void SetVSync(bool enabled)
    {
        MakeContextCurrent();
        GLFW.SwapInterval(enabled ? 1 : 0);
    }

    public (int width, int height) GetSize()
    {
        MakeContextCurrent();
        int width = 0, height = 0;
        GLFW.GetWindowSize(Handle, ref width, ref height);
        return (width, height);
    }

    public void SetSize(int w, int h)
    {
        MakeContextCurrent();
        GLFW.SetWindowSize(Handle, w, h);
    }

    public (int x, int y) GetPos()
    {
        MakeContextCurrent();
        int x = 0, y = 0;
        GLFW.GetWindowPos(Handle, ref x, ref y);
        return (x, y);
    }

    public void SetPos(int w, int h)
    {
        MakeContextCurrent();
        GLFW.SetWindowPos(Handle, w, h);
    }

    public bool GetMaximized()
    {
        MakeContextCurrent();
        return GLFW.GetWindowAttrib(Handle, GLFW.GLFW_MAXIMIZED) == GLFW.GLFW_TRUE;
    }

    public void SetMaximized(bool maximized)
    {
        MakeContextCurrent();
        if (maximized)
            GLFW.MaximizeWindow(Handle);
        else
            GLFW.RestoreWindow(Handle);
    }


    public void MakeContextCurrent() => GLFW.MakeContextCurrent(Handle);

    public bool ShouldClose => GLFW.WindowShouldClose(Handle) == 1;

    public void PollEvents() => GLFW.PollEvents();

    public void Clear(Color color)
    {
        MakeContextCurrent();
        _gl.ClearColor(color.R, color.G, color.B, color.A);
        _gl.Clear(GLClearBufferMask.ColorBufferBit);
    }

    public void SwapBuffers()
    {
        MakeContextCurrent();
        GLFW.SwapBuffers(Handle);
    }

    public void Dispose()
    {
        _gl.Dispose();
        GLFW.DestroyWindow(Handle);
        GLFW.Terminate();
    }
}