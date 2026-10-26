using System.Reflection;
using System.Runtime.InteropServices;
using Foundation;
using SherpaOnnx;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Tells the managed sherpa-onnx binding where its native library is on iOS (Slice 49). The
/// binding P/Invokes <c>sherpa-onnx-c-api</c>, a name that exists on Android and desktop but
/// not on iOS, where the engine ships as the <c>SherpaOnnxC.framework</c> bundle that
/// <c>NativeReference</c> (see the app csproj) copies into the app's <c>Frameworks</c> folder.
/// </summary>
internal static class IosSherpaLibrary
{
    private const string BindingLibraryName = "sherpa-onnx-c-api";
    private const string FrameworkName = "SherpaOnnxC";

    private static readonly object Gate = new();
    private static bool s_installed;

    /// <summary>Installs the resolver once; call before the first sherpa-onnx type is used.</summary>
    internal static void EnsureResolver()
    {
        lock (Gate)
        {
            if (s_installed)
            {
                return;
            }

            NativeLibrary.SetDllImportResolver(typeof(OfflineTts).Assembly, Resolve);
            s_installed = true;
        }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != BindingLibraryName)
        {
            return IntPtr.Zero;
        }

        string path = Path.Combine(NSBundle.MainBundle.BundlePath, "Frameworks", $"{FrameworkName}.framework", FrameworkName);
        return NativeLibrary.TryLoad(path, out IntPtr handle) ? handle : IntPtr.Zero;
    }
}
