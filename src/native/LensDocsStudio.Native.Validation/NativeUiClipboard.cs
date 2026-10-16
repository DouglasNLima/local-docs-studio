using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace LensDocsStudio.Native.Validation;

// UIA runs on a windowless MTA thread. OLE clipboard objects stay on a pumping STA thread.
internal static class NativeUiClipboard
{
    private static Dispatcher? dispatcher;
    internal static ApartmentState Apartment => Invoke(() => Thread.CurrentThread.GetApartmentState());
    public static IDisposable Start()
    {
        if (dispatcher is not null) throw new InvalidOperationException("The validation clipboard apartment is already running.");
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { ready.SetResult(Dispatcher.CurrentDispatcher); Dispatcher.Run(); })
            { IsBackground = true, Name = "Native validation clipboard" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        dispatcher = ready.Task.GetAwaiter().GetResult();
        return new Lifetime(thread, dispatcher);
    }
    public static void Clear() => Invoke(Clipboard.Clear);
    public static string GetText() => Read(Clipboard.GetText);
    public static void SetText(string text) => Invoke(() => Clipboard.SetText(text));
    public static bool ContainsData(string format) => Read(() => Clipboard.ContainsData(format));
    public static object? GetData(string format) => Read(() => Transfer(Clipboard.GetData(format)));
    public static BitmapSource? GetImage() => Read(() => (BitmapSource?)Transfer(Clipboard.GetImage()));
    public static void SetImage(BitmapSource image)
    { var frozen = (BitmapSource)Transfer(image)!; Invoke(() => Clipboard.SetImage(frozen)); }
    public static IDataObject? GetDataObject() => Read(() => Clipboard.GetDataObject() is { } value ? new ApartmentData(value) : null);
    public static void SetDataObject(object value, bool copy)
    {
        if (value is IDataObject data)
        { var prepared = (ApartmentData)PrepareDataObject(data); Invoke(() => Clipboard.SetDataObject(prepared.Inner, copy)); }
        else Invoke(() => Clipboard.SetDataObject(value, copy));
    }
    internal static IDataObject PrepareDataObject(IDataObject data)
    {
        if (data is ApartmentData) return data;
        // Fixture data may contain unfrozen pixels created by the MTA test. Detach them before transfer.
        var entries = data.GetFormats(false).Select(format => (format, value: Transfer(data.GetData(format, false)))).ToArray();
        return Invoke(() =>
        {
            var snapshot = new DataObject();
            foreach (var (format, value) in entries) if (value is not null) snapshot.SetData(format, value, false);
            return new ApartmentData(snapshot);
        });
    }
    private static object? Transfer(object? value)
    {
        if (value is Freezable { IsFrozen: false } freezable)
        {
            var clone = freezable.CloneCurrentValue();
            if (!clone.CanFreeze) throw new InvalidOperationException("Clipboard fixture pixels cannot cross apartments safely.");
            clone.Freeze(); return clone;
        }
        return value;
    }
    internal static T Read<T>(Func<T> action)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return Invoke(action); }
            // Another process may briefly hold OpenClipboard. Retry only this read,
            // never a write or user action, and leave the owning STA free to pump.
            catch (COMException ex) when (ex.HResult == unchecked((int)0x800401D0) && attempt < 4) { Thread.Sleep(50); }
        }
    }
    private static T Invoke<T>(Func<T> action) => (dispatcher ?? throw new InvalidOperationException("Start the validation clipboard apartment first.")).Invoke(action);
    private static void Invoke(Action action) => (dispatcher ?? throw new InvalidOperationException("Start the validation clipboard apartment first.")).Invoke(action);
    private sealed class Lifetime(Thread thread, Dispatcher owner) : IDisposable
    {
        public void Dispose()
        {
            owner.BeginInvokeShutdown(DispatcherPriority.Background);
            if (!thread.Join(TimeSpan.FromSeconds(10))) throw new TimeoutException("The validation clipboard apartment did not stop.");
            dispatcher = null;
        }
    }
    private sealed class ApartmentData(IDataObject inner) : IDataObject
    {
        public IDataObject Inner { get; } = inner;
        public object? GetData(string format, bool autoConvert) => Read(() => Transfer(Inner.GetData(format, autoConvert)));
        public object? GetData(string format) => Read(() => Transfer(Inner.GetData(format)));
        public object? GetData(Type format) => Read(() => Transfer(Inner.GetData(format)));
        public bool GetDataPresent(string format, bool autoConvert) => Read(() => Inner.GetDataPresent(format, autoConvert));
        public bool GetDataPresent(string format) => Read(() => Inner.GetDataPresent(format));
        public bool GetDataPresent(Type format) => Read(() => Inner.GetDataPresent(format));
        public string[] GetFormats(bool autoConvert) => Read(() => Inner.GetFormats(autoConvert));
        public string[] GetFormats() => Read(Inner.GetFormats);
        public void SetData(string format, object data, bool autoConvert) { var item = Transfer(data); Invoke(() => Inner.SetData(format, item, autoConvert)); }
        public void SetData(string format, object data) { var item = Transfer(data); Invoke(() => Inner.SetData(format, item)); }
        public void SetData(Type format, object data) { var item = Transfer(data); Invoke(() => Inner.SetData(format, item)); }
        public void SetData(object data) { var item = Transfer(data); Invoke(() => Inner.SetData(item)); }
    }
}
