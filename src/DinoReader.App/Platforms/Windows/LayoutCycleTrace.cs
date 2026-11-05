#if DEBUG || DINO_TEST_BOOKS
using System.Runtime.InteropServices;
using System.Text;

namespace DinoReader;

/// <summary>
/// Slice 264 layout-cycle trace, kept on purpose in DEBUG and DINO_TEST_BOOKS builds only (the VM check reads window-trace.log; store builds exclude this file's code): WinUI writes [LayoutCycleTracing] lines with OutputDebugString only. With no debugger attached, this listens the way DebugView does
/// (the DBWIN_BUFFER shared memory and its two events) and copies those lines of this process to window-trace.log, so a layout cycle names its element.
/// </summary>
internal static class LayoutCycleTrace
{
	private const string BufferName = "DBWIN_BUFFER";
	private const string ReadyName = "DBWIN_BUFFER_READY";
	private const string DataName = "DBWIN_DATA_READY";
	private const uint PageReadWrite = 0x04;
	private const uint MapRead = 0x04;
	private const int BufferBytes = 4096;
	private const int TextOffset = 4;
	private const uint Infinite = 0xFFFFFFFF;

	[DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr CreateEventW(IntPtr attrs, bool manual, bool initial, string name);

	[DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr CreateFileMappingW(IntPtr file, IntPtr attrs, uint protect, uint high, uint low, string name);

	[DllImport("kernel32", SetLastError = true)]
	private static extern IntPtr MapViewOfFile(IntPtr mapping, uint access, uint high, uint low, UIntPtr bytes);

	[DllImport("kernel32", SetLastError = true)]
	private static extern bool SetEvent(IntPtr handle);

	[DllImport("kernel32", SetLastError = true)]
	private static extern uint WaitForSingleObject(IntPtr handle, uint millis);

	internal static void Start()
	{
		var thread = new Thread(Listen) { IsBackground = true, Name = "layout-cycle-trace" };
		thread.Start();
	}

	private static void Listen()
	{
		IntPtr ready = CreateEventW(IntPtr.Zero, false, false, ReadyName);
		IntPtr data = CreateEventW(IntPtr.Zero, false, false, DataName);
		IntPtr mapping = CreateFileMappingW(new IntPtr(-1), IntPtr.Zero, PageReadWrite, 0, BufferBytes, BufferName);
		IntPtr view = mapping == IntPtr.Zero ? IntPtr.Zero : MapViewOfFile(mapping, MapRead, 0, 0, UIntPtr.Zero);
		WindowTrace.Write($"LayoutCycleTrace listener ready={ready != IntPtr.Zero} data={data != IntPtr.Zero} view={view != IntPtr.Zero} err={Marshal.GetLastWin32Error()}");
		if (view == IntPtr.Zero || ready == IntPtr.Zero || data == IntPtr.Zero)
		{
			return;
		}

		int own = Environment.ProcessId;
		while (true)
		{
			SetEvent(ready);
			WaitForSingleObject(data, Infinite);
			if (Marshal.ReadInt32(view) == own)
			{
				WindowTrace.Write("DBG " + ReadText(view));
			}
		}
	}

	private static string ReadText(IntPtr view)
	{
		var bytes = new byte[BufferBytes - TextOffset];
		Marshal.Copy(IntPtr.Add(view, TextOffset), bytes, 0, bytes.Length);
		int end = Array.IndexOf(bytes, (byte)0);
		return Encoding.UTF8.GetString(bytes, 0, end < 0 ? bytes.Length : end).TrimEnd('\r', '\n');
	}
}
#endif
