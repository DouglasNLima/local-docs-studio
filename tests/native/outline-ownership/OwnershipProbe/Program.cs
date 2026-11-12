using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;
using LensDocsStudio.Native.Rendering;

internal static class Program
{
 [STAThread] private static int Main(string[] args)
 {
  var output = Path.GetFullPath(args[0]);
  if (Directory.Exists(output)) throw new IOException("Use fresh diagnostic output.");
  Directory.CreateDirectory(output);
  var state = Path.Combine(output, "state"); Directory.CreateDirectory(state);
  Environment.SetEnvironmentVariable("LENS_NATIVE_VIEWPORT_DIAGNOSTICS", "1");
  File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(Theme: "Light", AutomaticUpdateChecks: false, ShowOutline: true, Desktop: new DesktopPreferences(FollowCaret: false, SyncScroll: false, SyncSelection: false))));
  var fixture = Path.Combine(output, "caret.md");
  File.Copy(Path.GetFullPath(args[1]), fixture);
  var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
  foreach(var name in new[]{"CommandIcons", "DesktopStyles", "FormStyles"})
   app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/LensDocsStudio.Native;component/Resources/{name}.xaml") });
  SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
  ThemeService.Apply(app.Resources, "Light"); var dialogs = new DesktopDialogs();
  using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state,"writes")),new SessionStore(state),new WorkspaceService(),dialogs);
  var window = new MainWindow(model, dialogs, []) { Width = 1280, Height = 800 };
  const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
  window.Loaded -= (RoutedEventHandler)typeof(MainWindow).GetMethod("WindowLoaded", flags)!.CreateDelegate(typeof(RoutedEventHandler),window);
  window.Closing -= (CancelEventHandler)typeof(MainWindow).GetMethod("WindowClosing", flags)!.CreateDelegate(typeof(CancelEventHandler),window);
  object? Call(string name, params object[] values) => typeof(MainWindow).GetMethod(name,flags)!.Invoke(window,values);
  object? Field(string name) => typeof(MainWindow).GetField(name, flags)!.GetValue(window);
  var editor=(TextEditor)window.FindName("Editor"); var outline=(TreeView)window.FindName("Outline");
  var preview=(FlowDocumentReader)window.FindName("Preview");
  var observations=new List<object>(); var phase="initialise";
  var checks=new List<string>();
  void Check(bool condition,string label) { if(!condition) throw new IOException(label); checks.Add(label); }
  void Trace(string operation, string? stack=null) {
   if(observations.Count >= 500) return;
   observations.Add(new { Phase=phase,Operation=operation,Active=model.ActiveDocument?.Id,Displayed=Field("displayedTab") is DocumentViewModel d?d.Id:(Guid?)null,
    Parsed=Field("parsedDocumentId"),Rendered=Field("renderedDocumentId"),Updating=Field("updatingOutline"),ViewportGeneration=Field("viewportGeneration"),SyncGeneration=Field("syncGeneration"),
    Caret=editor.CaretOffset,Selection=editor.SelectionStart,Length=editor.SelectionLength,SourceLength=editor.Document.TextLength,
    EditorVertical=editor.VerticalOffset,Page=preview.PageNumber,EditorFocus=editor.IsKeyboardFocusWithin,OutlineFocus=outline.IsKeyboardFocusWithin,
    Focus=Keyboard.FocusedElement?.GetType().Name,Stack=stack });
  }
  editor.TextArea.Caret.PositionChanged+=(_,_)=>Trace("caret-changed",Environment.StackTrace);
  editor.TextArea.SelectionChanged+=(_,_)=>Trace("selection-changed",Environment.StackTrace);
  // Wrap the original delegate without changing its arguments or event delivery.
  var selected=(RoutedPropertyChangedEventHandler<object>?)typeof(MainWindow).GetMethod("OutlineSelected",flags)?.CreateDelegate(typeof(RoutedPropertyChangedEventHandler<object>),window);
  if(selected is not null) outline.SelectedItemChanged-=selected;
  outline.SelectedItemChanged+=(sender,e)=> { Trace("selected-before " + (e.NewValue as OutlineHeadingViewModel)?.Title); selected?.Invoke(sender,e);Trace("selected-after"); };
  Task Drain()=>window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle).Task;
  async Task Parse(){await (Task)Call("UpdatePreviewAsync",false)!;await Drain();}
  double PreviewScroll() {
   var scrolling=PreviewSemanticAnchors.Descendant<FlowDocumentScrollViewer>(preview)!;
   return ((ScrollViewer)scrolling.Template.FindName("PART_ContentHost",scrolling)).VerticalOffset;
  }
  window.Show(); var code=1;
  _=window.Dispatcher.BeginInvoke(async()=>{
   try {
    await model.InitialiseAsync([fixture]); await Parse();
    phase="passive-keyboard-edit";editor.Focus();editor.Select(editor.Document.TextLength,0);Trace("before-text-input");
    TextCompositionManager.StartComposition(new TextComposition(InputManager.Current,editor.TextArea,"e"));
    Trace("after-text-input");await Parse();Trace("after-passive-parse");
    var source=File.ReadAllText(fixture)+"e";
    var caretPass=editor.Text==source&&editor.CaretOffset==source.Length&&editor.SelectionLength==0&&editor.IsKeyboardFocusWithin;
    Check(caretPass,"Passive parse preserves exact composed source, natural caret, empty selection and editor focus");
    editor.Select(100,7); var selectedCaret=editor.CaretOffset;Call("RefreshOutline");await Drain();
    Check(editor.SelectionStart==100&&editor.SelectionLength==7&&editor.CaretOffset==selectedCaret&&editor.IsKeyboardFocusWithin,
     "Passive highlight preserves a non-empty editor selection and its focus");
    phase="document-A";
    var aPath=Path.Combine(output,"A.md");var bPath=Path.Combine(output,"B.md");
    File.Copy(Path.GetFullPath(args[2]),aPath);File.Copy(Path.GetFullPath(args[3]),bPath);
    await model.OpenPathAsync(aPath);await Parse();
    var a=model.ActiveDocument!;
    editor.Focus();editor.Select(0,0);await Drain();
    editor.ScrollToLine(195);await Drain();
    var result=(PreviewResult)Field("rendered")!;
    var sourceOffset=editor.Text.IndexOf("## Region 065",StringComparison.Ordinal);
    await PreviewNavigationService.BringIntoViewAsync(preview,result.SourceElements[sourceOffset].ContentStart);await Drain();
    Trace("A-reading");var aCaret=editor.CaretOffset;var aVertical=editor.VerticalOffset;var aPreview=Call("VisiblePreviewSource");
    phase="document-B";await model.OpenPathAsync(bPath);await Parse();Trace("B-settled");
    var bPreview=Call("VisiblePreviewSource");var bScroll=PreviewScroll();
    Check(editor.CaretOffset==0&&editor.VerticalOffset==0&&bScroll==0,"New document begins at its own editor and preview top");
    phase="return-A";model.ActiveDocument=a;await Parse();Trace("A-restored");
    var viewportPass=editor.CaretOffset==aCaret&&Math.Abs(editor.VerticalOffset-aVertical)<1&&Equals(Call("VisiblePreviewSource"),aPreview);
    Check(viewportPass,"Returning document restores independent editor and preview reading positions with caret at the top");
    var b=model.Documents.Single(d=>d!=a&&d.FilePath==bPath);
    // Establish B's ordinary semantic restoration before testing a pending A
    // request. Its reading anchor is one third down the viewport; restoring
    // that anchor can adjust a few pixels even though the first heading stays visible.
    model.ActiveDocument=b;await Parse();var bRestoredScroll=PreviewScroll();
    Check(Equals(Call("VisiblePreviewSource"),bPreview),"Ordinary B restoration preserves its exact reading anchor");
    model.ActiveDocument=a;await Parse();
    OutlineHeadingViewModel[] Nodes()=>((IEnumerable<OutlineHeadingViewModel>)Field("outlineHeadings")!).ToArray();
    TreeViewItem Row(OutlineHeadingViewModel node) {
     ItemsControl owner=node.Parent is null?outline:Row(node.Parent);owner.UpdateLayout();
     return (TreeViewItem)owner.ItemContainerGenerator.ContainerFromItem(node);
    }
    phase="explicit-pointer";
    editor.Focus();editor.Select(100,7);
    var root=Nodes()[0];var row=Row(root);row.Focus();row.IsSelected=true;
    Check(editor.CaretOffset!=root.Heading.SourceStart&&editor.SelectionLength==7,"Pointer activation starts with a distinct non-empty editor selection");
    row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=Mouse.MouseUpEvent});
    await Drain();
    Check(editor.CaretOffset==root.Heading.SourceStart&&editor.SelectionLength==0&&outline.IsKeyboardFocusWithin,
     "Explicit header pointer activation navigates without taking keyboard focus from the Outline");
    phase="explicit-keyboard";
    var child=Nodes()[2];row=Row(child);row.Focus();row.IsSelected=true;
    row.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),Environment.TickCount,Key.Enter){RoutedEvent=Keyboard.KeyDownEvent});await Drain();
    Check(editor.CaretOffset==child.Heading.SourceStart&&editor.SelectionLength==0&&row.IsKeyboardFocused,
     "Explicit Enter navigates the selected child and retains row focus");
    phase="pending-A-to-B";
    Call("NavigateOutlineHeading",Nodes()[65]);model.ActiveDocument=b;
    await Drain();await Parse();
    File.WriteAllText(Path.Combine(output,"pending-switch.json"),JsonSerializer.Serialize(new {
     OwnerMatches=model.ActiveDocument==b,EditorMatches=editor.Document==b.EditorDocument,editor.CaretOffset,editor.VerticalOffset,
     ExpectedReadingAnchor=bPreview,ActualReadingAnchor=Call("VisiblePreviewSource"),ExpectedScroll=bRestoredScroll,ActualScroll=PreviewScroll(),
     FirstHeadingRect=((PreviewResult)Field("rendered")!).SourceElements[0].ContentStart.GetCharacterRect(System.Windows.Documents.LogicalDirection.Forward).ToString()
    },new JsonSerializerOptions{WriteIndented=true}));
    // VisiblePreviewSource is the reading anchor one third down the viewport,
    // not the top source offset. Compare B's captured anchor AND physical scroll.
    Check(model.ActiveDocument==b&&editor.Document==b.EditorDocument&&editor.CaretOffset==0&&editor.VerticalOffset==0
     &&Equals(Call("VisiblePreviewSource"),bPreview)&&PreviewScroll()==bRestoredScroll,
     "Pending Outline navigation cannot move the incoming document editor or preview");
    phase="pending-A-B-A";
    model.ActiveDocument=a;await Parse();
    Call("NavigateOutlineHeading",Nodes()[65]);model.ActiveDocument=b;model.ActiveDocument=a;
    editor.Select(0,0);editor.ScrollToVerticalOffset(0);await Parse();
    Check(editor.CaretOffset==0&&editor.VerticalOffset==0,
     "A-B-A viewport generations reject a previously queued editor navigation");
    phase="pending-source-replaced";
    Call("NavigateOutlineHeading",Nodes()[65]);editor.Document.Insert(0,"New unsaved prefix\n");
    editor.Select(0,0);editor.ScrollToVerticalOffset(0);await Parse();
    Check(editor.CaretOffset==0&&editor.SelectionLength==0&&editor.VerticalOffset==0,
     "A source edit supersedes pending Outline caret and scroll delivery");
    Check(editor.Text=="New unsaved prefix\n"+File.ReadAllText(aPath)&&File.ReadAllText(bPath)==b.EditorDocument.Text,
     "Navigation preserves exact edited and unchanged document sources");
    Check(File.ReadAllBytes(aPath).SequenceEqual(File.ReadAllBytes(Path.GetFullPath(args[2])))
     &&File.ReadAllBytes(bPath).SequenceEqual(File.ReadAllBytes(Path.GetFullPath(args[3])))
     &&File.ReadAllBytes(fixture).SequenceEqual(File.ReadAllBytes(Path.GetFullPath(args[1]))),
     "All saved fixture bytes remain unchanged");
    File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=true,CaretPass=caretPass,ViewportPass=viewportPass,ActualHighContrast=SystemParameters.HighContrast,Checks=checks},new JsonSerializerOptions{WriteIndented=true}));
    code=caretPass&&viewportPass?0:1;
   }catch(Exception error){File.WriteAllText(Path.Combine(output,"error.txt"),error.ToString());File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=false,Checks=checks,Error=error.ToString()}));}
   finally{File.WriteAllText(Path.Combine(output,"events.json"),JsonSerializer.Serialize(observations,new JsonSerializerOptions{WriteIndented=true}));window.Close();app.Shutdown();}
  });app.Run();return code;
 }
}
