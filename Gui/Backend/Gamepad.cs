using System.Runtime.InteropServices;
using Hexa.NET.SDL3;

namespace Tyr.Gui.Backend;

internal static unsafe class Gamepad
{
    public static bool IsConnected(SDLGamepadPtr gamepad)
    {
        return gamepad.Handle != null;
    }

    public static void Refresh(ref SDLGamepadPtr gamepad)
    {
        int count = 0;
        int* gamepads = SDL.GetGamepads(ref count);

        if (count == 0)
        {
            if (gamepads != null)
            {
                SDL.Free(gamepads);
            }

            Close(ref gamepad);
            return;
        }

        if (gamepad.Handle == null)
        {
            gamepad = SDL.OpenGamepad(gamepads[0]);
        }

        if (gamepads != null)
        {
            SDL.Free(gamepads);
        }
    }

    public static void Close(ref SDLGamepadPtr gamepad)
    {
        if (gamepad.Handle != null)
        {
            SDL.CloseGamepad(gamepad);
            gamepad = default;
        }
    }

    public static string GetName(SDLGamepadPtr gamepad)
    {
        if (!IsConnected(gamepad))
        {
            return "No Gamepad";
        }

        return Marshal.PtrToStringAnsi((nint)SDL.GetGamepadName(gamepad)) ?? "Unknown Gamepad";
    }

    public static float GetStickAxis(SDLGamepadPtr gamepad, SDLGamepadAxis axis, float deadzone)
    {
        if (!IsConnected(gamepad))
        {
            return 0f;
        }

        var raw = SDL.GetGamepadAxis(gamepad, axis);
        var normalized = Math.Clamp(raw / 32767f, -1f, 1f);
        var magnitude = MathF.Abs(normalized);

        if (magnitude <= deadzone)
        {
            return 0f;
        }

        var scaled = (magnitude - deadzone) / (1f - deadzone);
        return MathF.CopySign(scaled, normalized);
    }

    public static float GetTriggerAxis(SDLGamepadPtr gamepad, SDLGamepadAxis axis, float deadzone)
    {
        if (!IsConnected(gamepad))
        {
            return 0f;
        }

        var normalized = Math.Clamp(SDL.GetGamepadAxis(gamepad, axis) / 32767f, 0f, 1f);
        if (normalized <= deadzone)
        {
            return 0f;
        }

        return (normalized - deadzone) / (1f - deadzone);
    }
}
