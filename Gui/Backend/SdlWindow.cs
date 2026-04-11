using Hexa.NET.OpenGL;
using Hexa.NET.SDL3;
using System.Reflection;
using System.Runtime.InteropServices;
using Tyr.Common.Debug.Drawing;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Gui.Backend;

internal unsafe class SdlWindow : IDisposable
{
    private readonly SDLWindowPtr _handle;
    public SDLWindowPtr Handle => _handle;
    public SDLGLContext GlContext { get; }
    private readonly GL _gl;
    private bool _shouldClose;
    private readonly uint _windowId;
    private readonly List<SDLEvent> _events = [];

    static SdlWindow()
    {
        SDL.SetHint(SDL.SDL_HINT_MOUSE_FOCUS_CLICKTHROUGH, "1");
        SDL.SetHint(SDL.SDL_HINT_MOUSE_AUTO_CAPTURE, "1");
        SDL.Init((uint)(SDLInitFlags.Events | SDLInitFlags.Video | SDLInitFlags.Gamepad));
    }

    public SdlWindow(string title, int width, int height, int? x = null, int? y = null, bool? maximized = null)
    {
        SDL.GLSetAttribute(SDLGLAttr.ContextMajorVersion, 3);
        SDL.GLSetAttribute(SDLGLAttr.ContextMinorVersion, 2);
        SDL.GLSetAttribute(SDLGLAttr.ContextProfileMask, 1); // Core profile

        if (width == 0) width = 1280;
        if (height == 0) height = 720;

        var flags = (ulong)(SDLWindowFlags.Opengl | SDLWindowFlags.Resizable | SDLWindowFlags.HighPixelDensity);
        if (maximized == true) flags |= (ulong)SDLWindowFlags.Maximized;

        _handle = SDL.CreateWindow(title, width, height, flags);
        if (_handle.Handle == null) throw new Exception("SDL3 window creation failed");

        SetWindowIcon();

        _windowId = SDL.GetWindowID(_handle);

        if (x.HasValue && y.HasValue)
            SDL.SetWindowPosition(_handle, x.Value, y.Value);

        GlContext = SDL.GLCreateContext(_handle);
        SDL.GLMakeCurrent(_handle, GlContext);

        _gl = new GL(new BindingsContext(_handle, GlContext));
    }

    private void SetWindowIcon()
    {
        var resourceName = $"{Assembly.GetExecutingAssembly().GetName().Name}.Assets.AppIcon.png";
        using var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (iconStream is null)
            return;

        using var memory = new MemoryStream((int)iconStream.Length);
        iconStream.CopyTo(memory);
        var iconBytes = memory.ToArray();

        fixed (byte* iconPtr = iconBytes)
        {
            var io = SDL.IOFromConstMem((IntPtr)iconPtr, (nuint)iconBytes.Length);
            if (io.Handle == null)
                throw new InvalidOperationException($"Failed to open icon stream from embedded resource '{resourceName}': {GetSdlError()}");

            var surface = SDL.LoadSurfaceIO(io, true);
            if (surface.Handle == null)
                throw new InvalidOperationException($"Failed to decode embedded icon resource '{resourceName}': {GetSdlError()}");

            SDL.SetWindowIcon(_handle, surface);
            SDL.DestroySurface(surface);
        }
    }

    private static string GetSdlError() => Marshal.PtrToStringUTF8((IntPtr)SDL.GetError()) ?? "unknown SDL error";

    public void SetVSync(bool enabled) => SDL.GLSetSwapInterval(enabled ? 1 : 0);

    public (int width, int height) GetSize()
    {
        int w = 0, h = 0;
        SDL.GetWindowSize(_handle, ref w, ref h);
        return (w, h);
    }

    public void SetSize(int w, int h) => SDL.SetWindowSize(_handle, w, h);

    public (int x, int y) GetPos()
    {
        int x = 0, y = 0;
        SDL.GetWindowPosition(_handle, ref x, ref y);
        return (x, y);
    }

    public void SetPos(int x, int y) => SDL.SetWindowPosition(_handle, x, y);

    public bool GetMaximized() =>
        (SDL.GetWindowFlags(_handle) & (ulong)SDLWindowFlags.Maximized) != 0;

    public void SetMaximized(bool maximized)
    {
        if (maximized) SDL.MaximizeWindow(_handle);
        else SDL.RestoreWindow(_handle);
    }

    public void MakeContextCurrent() => SDL.GLMakeCurrent(_handle, GlContext);

    public bool ShouldClose => _shouldClose;

    public IReadOnlyList<SDLEvent> Events => _events;

    public void PollEvents()
    {
        _events.Clear();
        SDL.PumpEvents();
        SDLEvent e = default;
        while (SDL.PollEvent(ref e))
        {
            _events.Add(e);
            switch ((SDLEventType)e.Type)
            {
                case SDLEventType.Quit:
                case SDLEventType.Terminating:
                    _shouldClose = true;
                    break;
                case SDLEventType.WindowCloseRequested:
                    if (e.Window.WindowID == _windowId)
                        _shouldClose = true;
                    break;
            }
        }
    }

    public void Clear(Color color)
    {
        MakeContextCurrent();
        _gl.ClearColor(color.R, color.G, color.B, color.A);
        _gl.Clear(GLClearBufferMask.ColorBufferBit);
    }

    public void SwapBuffers()
    {
        MakeContextCurrent();
        SDL.GLSwapWindow(_handle);
    }

    public void Dispose()
    {
        _gl.Dispose();
        SDL.GLDestroyContext(GlContext);
        SDL.DestroyWindow(_handle);
        SDL.Quit();
    }
}
