using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Tyr.Common.Config;

namespace Tyr.Common.Runner;

[Configurable]
public static partial class ProcessScheduling
{
    [ConfigEntry("Windows process priority class applied to the whole Tyr process.")]
    public static ProcessPriorityClass PriorityClass { get; set; } = ProcessPriorityClass.AboveNormal;

    [ConfigEntry("Windows execution-speed throttling for the process. Keep this false to avoid background slowdowns.")]
    public static bool PowerThrottling { get; set; } = false;

    [ConfigEntry("When true on Windows, the OS may ignore high-resolution timer requests for this process.")]
    public static bool IgnoreTimerResolution { get; set; } = false;

    [ConfigEntry("Prevents macOS App Nap from throttling the process while the app is doing real-time work.")]
    public static bool DisableAppNap { get; set; } = true;

    private static bool _initialized;
    private static nint _macOsActivity;

    public static void Initialize()
    {
        if (_initialized) return;

        _initialized = true;

        try
        {
            Registry.Get(typeof(ProcessScheduling)).OnUpdated += _ => Apply();
        }
        catch (KeyNotFoundException)
        {
            // If config registration is not ready yet, we still apply the startup defaults below.
        }

        Apply();
    }

    public static void Apply()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                ApplyWindows();
            }
            else if (OperatingSystem.IsMacOS())
            {
                ApplyMacOs();
            }
        }
        catch (Exception exception)
        {
            Log.ZLogWarning($"Failed to apply process scheduling settings: {exception.Message}");
        }
    }

    private static void ApplyWindows()
    {
        using var process = Process.GetCurrentProcess();

        if (process.PriorityClass != PriorityClass)
        {
            process.PriorityClass = PriorityClass;
        }

        var state = new ProcessPowerThrottlingState
        {
            Version = ProcessPowerThrottlingCurrentVersion,
            ControlMask = ProcessPowerThrottlingExecutionSpeed | ProcessPowerThrottlingIgnoreTimerResolution,
            StateMask = (PowerThrottling ? ProcessPowerThrottlingExecutionSpeed : 0u)
                        | (IgnoreTimerResolution ? ProcessPowerThrottlingIgnoreTimerResolution : 0u)
        };

        if (!SetProcessInformation(
                process.Handle,
                ProcessInformationClass.ProcessPowerThrottling,
                ref state,
                (uint)Marshal.SizeOf<ProcessPowerThrottlingState>()))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static void ApplyMacOs()
    {
        if (!DisableAppNap)
        {
            EndMacOsActivity();
            return;
        }

        if (_macOsActivity != 0) return;

        var processInfoClass = objc_getClass("NSProcessInfo");
        var processInfo = objc_msgSend(processInfoClass, sel_registerName("processInfo"));
        var reason = CFStringCreateWithCString(nint.Zero, "Real-time robotics processing", MacOsStringEncodingUtf8);
        var activity = objc_msgSend(
            processInfo,
            sel_registerName("beginActivityWithOptions:reason:"),
            MacOsActivityUserInitiatedAllowingIdleSystemSleep,
            reason);

        CFRelease(reason);

        if (activity == 0)
        {
            throw new InvalidOperationException("NSProcessInfo beginActivityWithOptions returned null");
        }

        _macOsActivity = objc_retain(activity);
    }

    private static void EndMacOsActivity()
    {
        if (_macOsActivity == 0) return;

        var processInfoClass = objc_getClass("NSProcessInfo");
        var processInfo = objc_msgSend(processInfoClass, sel_registerName("processInfo"));
        objc_msgSend(processInfo, sel_registerName("endActivity:"), _macOsActivity);
        objc_release(_macOsActivity);
        _macOsActivity = 0;
    }

    private const uint ProcessPowerThrottlingCurrentVersion = 1;
    private const uint ProcessPowerThrottlingExecutionSpeed = 0x1;
    private const uint ProcessPowerThrottlingIgnoreTimerResolution = 0x4;
    private const uint MacOsStringEncodingUtf8 = 0x08000100;
    private const nuint MacOsActivityUserInitiatedAllowingIdleSystemSleep = 0x00EFFFFF;

    private enum ProcessInformationClass
    {
        ProcessPowerThrottling = 4
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessPowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessInformation(
        nint hProcess,
        ProcessInformationClass processInformationClass,
        ref ProcessPowerThrottlingState processInformation,
        uint processInformationSize);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern nint objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern nint sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend(nint receiver, nint selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend(nint receiver, nint selector, nuint options, nint reason);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend(nint receiver, nint selector, nint arg1);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern nint objc_retain(nint value);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern void objc_release(nint value);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern nint CFStringCreateWithCString(nint alloc, string value, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(nint value);
}
