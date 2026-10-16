using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace LensDocsStudio.Native.Validation;

internal static class NativeClipboardApartmentChecks
{
    public static void Run(string output)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using (NativeUiClipboard.Start())
                {
                    var attempts = 0;
                    var read = NativeUiClipboard.Read(() =>
                    {
                        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) throw new IOException("Clipboard retry left its owner apartment.");
                        if (++attempts < 3) throw new COMException("Simulated clipboard contention", unchecked((int)0x800401D0));
                        return "Exact Café — observed text";
                    });
                    if (read != "Exact Café — observed text" || attempts != 3) throw new IOException("Transient clipboard reads did not recover exact data.");
                    attempts = 0; var persistentRejected = false;
                    try { NativeUiClipboard.Read<string>(() => { attempts++; throw new COMException("Persistent clipboard contention", unchecked((int)0x800401D0)); }); }
                    catch (COMException ex) when (ex.HResult == unchecked((int)0x800401D0) && attempts == 5) { persistentRejected = true; }
                    if (!persistentRejected || attempts != 5) throw new IOException("Clipboard contention retries were not bounded.");
                    attempts = 0; var unrelatedRejected = false;
                    try { NativeUiClipboard.Read<string>(() => { attempts++; throw new COMException("Unrelated COM failure", unchecked((int)0x80004005)); }); }
                    catch (COMException ex) when (ex.HResult == unchecked((int)0x80004005) && attempts == 1) { unrelatedRejected = true; }
                    if (!unrelatedRejected || attempts != 1) throw new IOException("An unrelated clipboard failure was retried.");
                    var pixels = new byte[] { 0, 0, 255, 255, 0, 255, 0, 255 };
                    var bitmap = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 8);
                    var source = new DataObject();
                    source.SetData(DataFormats.UnicodeText, "Café — exact\r\ntext", false);
                    source.SetData(DataFormats.Html, "<strong>Exact HTML</strong>", false);
                    source.SetData(DataFormats.Bitmap, bitmap, false);
                    source.SetData("Lens.Validation.Bytes", new byte[] { 4, 2, 0, 255 }, false);
                    var prepared = NativeUiClipboard.PrepareDataObject(source);
                    if (!prepared.GetFormats(false).Order().SequenceEqual(source.GetFormats(false).Order())
                        || (string?)prepared.GetData(DataFormats.UnicodeText, false) != "Café — exact\r\ntext"
                        || (string?)prepared.GetData(DataFormats.Html) != "<strong>Exact HTML</strong>"
                        || prepared.GetData("Lens.Validation.Bytes") is not byte[] bytes || !bytes.SequenceEqual(new byte[] { 4, 2, 0, 255 }))
                        throw new IOException("Clipboard apartment transfer lost explicit formats or exact fixture data.");
                    if (prepared.GetData(DataFormats.Bitmap) is not BitmapSource { IsFrozen: true, PixelWidth: 2, PixelHeight: 1 } transferred || bitmap.IsFrozen)
                        throw new IOException("Clipboard apartment transfer leaked thread-owned pixels or mutated the original image.");
                    var copied = new byte[8]; transferred.CopyPixels(copied, 8, 0);
                    if (!copied.SequenceEqual(pixels)) throw new IOException("Cross-apartment image pixels changed.");
                    prepared.SetData(DataFormats.UnicodeText, "Updated exact text", false);
                    if ((string?)prepared.GetData(DataFormats.UnicodeText) != "Updated exact text"
                        || (string?)source.GetData(DataFormats.UnicodeText) != "Café — exact\r\ntext"
                        || !ReferenceEquals(prepared, NativeUiClipboard.PrepareDataObject(prepared)))
                        throw new IOException("Detached clipboard data could not be updated or changed its original fixture.");
                }
                completion.SetResult();
            }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true, Name = "Clipboard apartment validation" };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start();
        if (!completion.Task.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Clipboard apartment validation did not finish.");
        completion.Task.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(output, "native-clipboard-apartments.json"), JsonSerializer.Serialize(new { Passed = true,
            Checks = new[] { "MTA/STA transfer preserves explicit Unicode, HTML, custom binary and exact image pixels",
                "Returned pixels are frozen, originals remain unchanged and data-object updates stay on the owning apartment",
                "Transient read contention returns exact data after two retries on the owner STA",
                "Persistent clipboard contention stops after five attempts",
                "Unrelated COM errors propagate without retry" },
            Limitation = "In-memory data objects only; this check never reads or writes the Windows clipboard. Desktop delivery requires Windows CI." }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
