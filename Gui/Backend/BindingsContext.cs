using Hexa.NET.SDL3;

namespace Tyr.Gui.Backend;

internal unsafe class BindingsContext(SDLWindowPtr window, SDLGLContext context) : HexaGen.Runtime.IGLContext
{
    public nint Handle => (nint)window.Handle;

    public bool IsCurrent => SDL.GLGetCurrentContext() == context;

    public void Dispose() { }

    public nint GetProcAddress(string procName) => (nint)SDL.GLGetProcAddress(procName);

    public bool IsExtensionSupported(string extensionName) => SDL.GLExtensionSupported(extensionName);

    public void MakeCurrent() => SDL.GLMakeCurrent(window, context);

    public void SwapBuffers() => SDL.GLSwapWindow(window);

    public void SwapInterval(int interval) => SDL.GLSetSwapInterval(interval);

    public bool TryGetProcAddress(string procName, out nint procAddress)
    {
        procAddress = (nint)SDL.GLGetProcAddress(procName);
        return procAddress != 0;
    }
}
