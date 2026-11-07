using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LensDocsStudio.Native.UxProbe;

// Optional desktop-wide timing only. This does not identify an application's
// submitted surface, prove monitor scan-out, flush DWM or request a WPF render.
internal static class CompositionObservation
{
    internal static object? Read()
    {
        if (Environment.GetEnvironmentVariable("LENS_NATIVE_COMPOSITION_TRACE") != "1") return null;
        var timing = new Timing { Size = (uint)Marshal.SizeOf<Timing>() };
        if (timing.Size != 292 || Marshal.OffsetOf<Timing>(nameof(Timing.Compose)).ToInt32() != 48)
            throw new InvalidOperationException("DWM timing structure differs from the Windows SDK ABI.");
        var started = Stopwatch.GetTimestamp();
        var result = DwmGetCompositionTimingInfo(0, ref timing);
        return new { started, finished = Stopwatch.GetTimestamp(), result, timing.Size,
            timing.RefreshNumerator, timing.RefreshDenominator, timing.RefreshPeriod,
            timing.VBlank, timing.Refresh, timing.Compose, timing.Frame, timing.RefreshFrame,
            timing.FrameSubmitted, timing.FrameConfirmed, timing.RefreshConfirmed,
            timing.FramesLate, timing.FramesOutstanding,
            scope = "Desktop-wide composition counters; no per-window presentation or physical scan-out guarantee" };
    }

    // dwmapi.h uses pshpack1.h; QPC_TIME and DWM_FRAME_COUNT are UINT64.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Timing
    {
        public uint Size, RefreshNumerator, RefreshDenominator;
        public ulong RefreshPeriod;
        public uint ComposeNumerator, ComposeDenominator;
        public ulong VBlank, Refresh;
        public uint DxRefresh;
        public ulong Compose, Frame;
        public uint DxPresent;
        public ulong RefreshFrame, FrameSubmitted;
        public uint DxPresentSubmitted;
        public ulong FrameConfirmed;
        public uint DxPresentConfirmed;
        public ulong RefreshConfirmed;
        public uint DxRefreshConfirmed;
        public ulong FramesLate;
        public uint FramesOutstanding;
        public ulong FrameDisplayed, FrameDisplayedTime, RefreshFrameDisplayed,
            FrameComplete, FrameCompleteTime, FramePending, FramePendingTime,
            FramesDisplayed, FramesComplete, FramesPending, FramesAvailable,
            FramesDropped, FramesMissed, RefreshNextDisplayed, RefreshNextPresented,
            RefreshesDisplayed, RefreshesPresented, RefreshStarted, PixelsReceived,
            PixelsDrawn, BuffersEmpty;
    }
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetCompositionTimingInfo(nint window, ref Timing timing);
}
