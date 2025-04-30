using Hexa.NET.GLFW;
using Hexa.NET.OpenGL;
using Tyr.Common.Debug.Drawing;

namespace Tyr.Gui.Backend;

internal class GlfwWindow : IDisposable
{
    public GLFWwindowPtr Handle { get; }
    private readonly GL _gl;

    static GlfwWindow() => GLFW.Init();

    public GlfwWindow(int w, int h, string title)
    {
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MAJOR, 3);
        GLFW.WindowHint(GLFW.GLFW_CONTEXT_VERSION_MINOR, 2);
        GLFW.WindowHint(GLFW.GLFW_OPENGL_PROFILE, GLFW.GLFW_OPENGL_CORE_PROFILE); // 3.2+ only

        GLFW.WindowHint(GLFW.GLFW_FOCUSED, 1); // Make window focused on start
        GLFW.WindowHint(GLFW.GLFW_RESIZABLE, 1); // Make window resizable

        Handle = GLFW.CreateWindow(w, h, title, null, null);
        if (Handle.IsNull) throw new Exception("GLFW window failed");
        MakeContextCurrent();
        _gl = new GL(new BindingsContext(Handle));
    }

    public void SetVSync(bool enabled)
    {
        MakeContextCurrent();
        GLFW.SwapInterval(enabled ? 1 : 0);
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