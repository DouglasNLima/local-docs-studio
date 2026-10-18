using System.Security.Cryptography;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

// Diagnostic private-runtime experiment only. No product dependency or package uses it.
// The source runtime is read-only; the caller supplies a separate workspace output.
if (args.Length != 2) throw new ArgumentException("Supply original and distinct experimental PresentationFramework paths.");
var source = Path.GetFullPath(args[0]); var target = Path.GetFullPath(args[1]);
if (source.Equals(target, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(source) != "PresentationFramework.dll")
    throw new ArgumentException("Preserve the original framework assembly.");
using var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(source)!);
resolver.AddSearchDirectory(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory());
using var assembly = AssemblyDefinition.ReadAssembly(source, new ReaderParameters { InMemory = true, AssemblyResolver = resolver });
var module = assembly.MainModule;
var marker = module.GetType("MS.Internal.PtsHost.ListMarkerLine") ?? throw new IOException("Unknown WPF marker implementation.");
var client = module.GetType("MS.Internal.PtsHost.ListParaClient") ?? throw new IOException("Unknown WPF list implementation.");
var format = marker.Methods.Single(method => method.Name == "FormatAndDrawVisual");
var validate = client.Methods.Single(method => method.Name == "ValidateVisual");
format.Body.SimplifyMacros(); validate.Body.SimplifyMacros();
Directory.CreateDirectory(Path.GetDirectoryName(target)!);
var before = new { Format = format.Body.Instructions.Select(i => i.ToString()).ToArray(), Validate = validate.Body.Instructions.Select(i => i.ToString()).ToArray() };
File.WriteAllText(target + ".before.json", JsonSerializer.Serialize(before, new JsonSerializerOptions { WriteIndented = true }));
var formatterCall = format.Body.Instructions.Single(i => i.Operand is MethodReference m && m.Name == "FormatLine");
var store = formatterCall.Next;
if (store.OpCode != OpCodes.Stloc || store.Operand is not VariableDefinition line) throw new IOException("Unknown marker local.");
var start = format.Body.ExceptionHandlers.Single().TryStart;
while (start.OpCode == OpCodes.Nop) start = start.Next;
if (start.OpCode != OpCodes.Ldarg_0 && !(start.OpCode == OpCodes.Ldarg && start.Operand == format.Body.ThisParameter)) throw new IOException("Unknown formatter expression.");
var afterStore = store.Next;
var dispose = format.Body.Instructions.Single(i => i.Operand is MethodReference m && m.Name == "Dispose");
var loadForDispose = dispose.Previous;
if (loadForDispose.OpCode != OpCodes.Ldloc || loadForDispose.Operand != line) throw new IOException("Unknown line disposal.");
var lineDispose = (MethodReference)dispose.Operand;
var enabled = new FieldDefinition("_lensDiagnosticSymbolReuse", FieldAttributes.Assembly, module.TypeSystem.Boolean);
var cached = new FieldDefinition("_lensDiagnosticSymbolLine", FieldAttributes.Private, line.VariableType);
var hits = new FieldDefinition("_lensDiagnosticReuseCount", FieldAttributes.Private | FieldAttributes.Static, module.TypeSystem.Int64);
marker.Fields.Add(enabled); marker.Fields.Add(cached); marker.Fields.Add(hits);
var increment = module.ImportReference(typeof(System.Threading.Interlocked).GetMethod("Increment", [typeof(long).MakeByRefType()])!);
var il = format.Body.GetILProcessor();
var cacheCheck = Insert(il, start,
    Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, enabled), Instruction.Create(OpCodes.Brfalse, start),
    Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, cached), Instruction.Create(OpCodes.Brfalse, start),
    Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, cached), Instruction.Create(OpCodes.Stloc, line),
    Instruction.Create(OpCodes.Ldsflda, hits), Instruction.Create(OpCodes.Call, increment), Instruction.Create(OpCodes.Pop), Instruction.Create(OpCodes.Br, afterStore));
foreach (var handler in format.Body.ExceptionHandlers) { if (handler.TryStart == start) handler.TryStart = cacheCheck; }
Insert(il, afterStore,
    Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, enabled), Instruction.Create(OpCodes.Brfalse, afterStore),
    Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldloc, line), Instruction.Create(OpCodes.Stfld, cached));
Insert(il, loadForDispose, Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, enabled), Instruction.Create(OpCodes.Brtrue, dispose.Next));

var markerLocal = validate.Body.Variables.Single(v => v.VariableType.FullName == marker.FullName);
var listLocal = validate.Body.Variables.Single(v => v.VariableType.FullName == "System.Windows.Documents.List");
var listStore = validate.Body.Instructions.Single(i => i.OpCode == OpCodes.Stloc && i.Operand == listLocal);
var listType = module.GetType("System.Windows.Documents.List");
var styleGetter = listType.Methods.Single(m => m.Name == "get_MarkerStyle");
var markerStyle = styleGetter.ReturnType.Resolve() ?? throw new IOException("Unknown marker enum.");
var disc = Convert.ToInt32(markerStyle.Fields.Single(f => f.Name == "Disc").Constant);
var vi = validate.Body.GetILProcessor();
Insert(vi, listStore.Next, Instruction.Create(OpCodes.Ldloc, markerLocal), Instruction.Create(OpCodes.Ldloc, listLocal),
    Instruction.Create(OpCodes.Callvirt, styleGetter), Instruction.Create(OpCodes.Ldc_I4, disc), Instruction.Create(OpCodes.Ceq), Instruction.Create(OpCodes.Stfld, enabled));

// One cached TextLine per existing ValidateVisual scope; numbered markers use the
// original path. Release before the existing marker-handle disposal, never globally.
var release = new MethodDefinition("ReleaseLensDiagnosticSymbolLine", MethodAttributes.Assembly | MethodAttributes.HideBySig, module.TypeSystem.Void);
marker.Methods.Add(release);
var ri = release.Body.GetILProcessor(); var done = Instruction.Create(OpCodes.Ret);
foreach (var instruction in new[] { Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, cached), Instruction.Create(OpCodes.Brfalse, done),
    Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, cached), Instruction.Create(OpCodes.Callvirt, lineDispose),
    Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldnull), Instruction.Create(OpCodes.Stfld, cached), done }) ri.Append(instruction);
var markerDispose = validate.Body.Instructions.Single(i => i.Operand is MethodReference m && m.Name == "Dispose" && m.DeclaringType.Namespace == "MS.Internal.PtsHost");
Insert(vi, markerDispose.Previous, Instruction.Create(OpCodes.Ldloc, markerLocal), Instruction.Create(OpCodes.Call, release));
format.Body.OptimizeMacros(); validate.Body.OptimizeMacros(); release.Body.OptimizeMacros();
module.Attributes |= ModuleAttributes.ILOnly;
// The output loses Microsoft binary signature validity and is never a production runtime.
Directory.CreateDirectory(Path.GetDirectoryName(target)!);
assembly.Write(target);
File.WriteAllText(target + ".experiment.json", JsonSerializer.Serialize(new {
    EligibleForProductAcceptance = false, FrameworkSignature = "Modified; not Microsoft-signed or trusted",
    Scope = "Native Disc marker TextLine reused within one existing ValidateVisual; all native List/ListItem/content objects retained",
    SourceSha256 = Hash(source), OutputSha256 = Hash(target), Assembly = assembly.Name.FullName,
    ReadyToRunPolicy = "Disable ReadyToRun equally on original and modified processes; this experiment is separate from stock-runtime acceptance",
    Before = before,
    After = new { Format = format.Body.Instructions.Select(i => i.ToString()).ToArray(), Validate = validate.Body.Instructions.Select(i => i.ToString()).ToArray() }
}, new JsonSerializerOptions { WriteIndented = true }));
static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
static Instruction Insert(ILProcessor il, Instruction before, params Instruction[] instructions) { foreach (var instruction in instructions) il.InsertBefore(before, instruction); return instructions[0]; }
