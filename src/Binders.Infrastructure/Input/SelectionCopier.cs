using System.Runtime.InteropServices;
using Binders.Application.Input;

namespace Binders.Infrastructure.Input;

/// <summary>
/// Copies the focused window's selection by synthesizing Ctrl+C. The Windows
/// keys are released first so the synthetic input isn't read as a Win+C chord
/// (the global hotkey that triggers this still has Ctrl+Win held down).
/// </summary>
public sealed class SelectionCopier : ISelectionCopier
{
    private static readonly TimeSpan ClipboardSettleDelay = TimeSpan.FromMilliseconds(150);

    public async Task CopyAsync()
    {
        NativeMethods.Input[] sequence =
        [
            KeyUp(NativeMethods.VkLeftWin),
            KeyUp(NativeMethods.VkRightWin),
            KeyDown(NativeMethods.VkControl),
            KeyDown(NativeMethods.VkC),
            KeyUp(NativeMethods.VkC),
            KeyUp(NativeMethods.VkControl),
        ];

        NativeMethods.SendInput(
            (uint)sequence.Length, sequence, Marshal.SizeOf<NativeMethods.Input>());

        await Task.Delay(ClipboardSettleDelay);
    }

    private static NativeMethods.Input KeyDown(ushort virtualKey) => Key(virtualKey, isKeyUp: false);

    private static NativeMethods.Input KeyUp(ushort virtualKey) => Key(virtualKey, isKeyUp: true);

    private static NativeMethods.Input Key(ushort virtualKey, bool isKeyUp) => new()
    {
        Type = NativeMethods.InputKeyboard,
        Data = new NativeMethods.InputUnion
        {
            Keyboard = new NativeMethods.KeyboardInput
            {
                VirtualKey = virtualKey,
                Flags = isKeyUp ? NativeMethods.KeyEventKeyUp : 0,
            },
        },
    };
}
