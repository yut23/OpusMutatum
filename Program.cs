using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpusMutatum {

	class Program {

		// For intermediary or devExe
		static string PathToLightning = "./Lightning.exe";

		// for merge
		static string PathToMonoMod = "./MonoMod.exe";

		// for devToRun
		static string PathToMod = "";

		// for strings (applied to intermediary exe)
		static string PathToIntermediaryLightning = "./IntermediaryLightning.exe";

		static List<string> MappingPaths = new List<string>();
		static List<string> IntermediaryPaths = new List<string>();
		static List<string> StringsPaths = new List<string>();

		static AssemblyDefinition LightningAssembly;

		static Dictionary<string, string> Intermediary = new Dictionary<string, string>();

		static void Main(string[] args){
			ArgumentParsingMode current = ArgumentParsingMode.Argument;
			RunAction action = RunAction.Run;
			foreach(var arg in args){
				switch(current) {
					case ArgumentParsingMode.Argument:
						// check if its "run", "strings", "intermediary", merge", "setup", "devExe", "devToRun"
						// or "--mappings", "--intermediary", "--strings", "--lightning", "--monomod", "--intermediaryPath"
						if(arg.Equals("run"))
							action = RunAction.Run;
						else if(arg.Equals("strings"))
							action = RunAction.Strings;
						else if(arg.Equals("intermediary"))
							action = RunAction.Intermediary;
						else if(arg.Equals("merge"))
							action = RunAction.Merge;
						else if(arg.Equals("setup"))
							action = RunAction.Setup;
						else if(arg.Equals("devExe"))
							action = RunAction.DevExe;
						else if(arg.Equals("devToRun")) {
							action = RunAction.DevToRun;
							current = ArgumentParsingMode.ModPath;
						}else if(arg.Equals("--mappings"))
							current = ArgumentParsingMode.MappingPath;
						else if(arg.Equals("--intermediary"))
							current = ArgumentParsingMode.IntermediaryPath;
						else if(arg.Equals("--strings"))
							current = ArgumentParsingMode.StringsPath;
						else if(arg.Equals("--lightning"))
							current = ArgumentParsingMode.LightningPath;
						else if(arg.Equals("--monomod"))
							current = ArgumentParsingMode.MonoModPath;
						break;
					case ArgumentParsingMode.MappingPath:
						MappingPaths.Add(arg);
						current = ArgumentParsingMode.Argument;
						break;
					case ArgumentParsingMode.IntermediaryPath:
						IntermediaryPaths.Add(arg);
						current = ArgumentParsingMode.Argument;
						break;
					case ArgumentParsingMode.StringsPath:
						StringsPaths.Add(arg);
						current = ArgumentParsingMode.Argument;
						break;
					case ArgumentParsingMode.LightningPath:
						PathToLightning = arg;
						current = ArgumentParsingMode.Argument;
						break;
					case ArgumentParsingMode.MonoModPath:
						PathToMonoMod = arg;
						current = ArgumentParsingMode.Argument;
						break;
					case ArgumentParsingMode.ModPath:
						PathToMod = arg;
						current = ArgumentParsingMode.Argument;
						break;
					default:
						Console.WriteLine("Invalid argument \"" + arg + "\"!");
						break;
				}
			}

			try {
				switch(action) {
					case RunAction.Strings:
						HandleStrings();
						break;
					case RunAction.Intermediary:
						HandleIntermediary();
						break;
					case RunAction.Merge:
						HandleMerge();
						break;
					case RunAction.Setup:
						HandleStrings();
						HandleIntermediary();
						HandleMerge();
						break;
					case RunAction.DevExe:
						HandleDevExe();
						break;
					case RunAction.DevToRun:
						HandleDevToRun();
						break;
					case RunAction.Run:
					default:
						HandleRun();
						break;
				}
			} catch(Exception e) {
				Console.WriteLine("Error executing task:");
				Console.WriteLine(e.Message);
				Console.WriteLine(e.StackTrace);
			}
			Console.WriteLine("Done.");
		}

		static void HandleRun(){
			// just run MONOMODDED_IntermediaryLightning.exe
		}

		static void HandleStrings(){
			Console.WriteLine("Dumping strings...");
			LoadLightning();
			// take Lightning.exe, find all refs to string parser: "#=qQ3boY4a6o2O2sPtKvJtj_Q6y77XoLuLRv$4EsOcRQr4="."#=qhwVTryR65imID$n_uKTBPA=="
			var module = LightningAssembly.MainModule;
			var parse = module.FindMethod("\u0023\u003Dq2EPbWi4HZqbkGDFcUMi56ZzB8VAbZoGcTMgcY2RDY4U\u003D", "\u0023\u003DqhM7pxz5HnSshiTag67xAZg\u003D\u003D");
			
			Console.WriteLine("Finding keys...");
			// get all the keys this way
			var refs = new List<Instruction>();
			foreach(var type in CollectNestedTypes(LightningAssembly.MainModule.Types)) 
				foreach(var method in type.Methods) 
					if(method.HasBody) 
						foreach(var instr in method.Body.Instructions) 
							if(instr.OpCode.Code == Code.Call && instr.Operand is MethodReference operand && operand.Resolve().Equals(parse))
								refs.Add(instr);

			var keys = new List<int>();

			foreach(var instr in refs)
				keys.Add((int)instr.Previous.Operand);

			Console.WriteLine($"Found {keys.Count()}");
			
			var mainMethod = module.FindMethod("\u0023\u003DqY_2YbNnFUotr2XEajflqbg\u003D\u003D", "\u0023\u003Dqv9BkCthwDUsuiNqUYPHVfA\u003D\u003D");
			var proc = mainMethod.Body.GetILProcessor();
			var first = proc.Body.Instructions.First();

			var stringt = module.TypeSystem.String;

			// we want String.Concat(String?, String?, String?)
			Console.WriteLine("Resolving Concat method...");
			var concat = module.ImportReference(stringt.Resolve().Methods.First(f => f.Parameters.Count == 3 && f.Parameters.All(p => p.ParameterType.FullName.Equals(stringt.FullName))));
			Console.WriteLine("Getting StreamWriter class...");
			var streamWriter = module.ImportReference(typeof(StreamWriter)).Resolve();
			Console.WriteLine("Getting StreamWriter constructor...");
			var streamWriterConstructor = module.ImportReference(streamWriter.Methods.First(m => m.Name.Equals(".ctor") && m.Parameters.Count() == 1 && m.Parameters.All(param => param.ParameterType.FullName.Equals(stringt.FullName))));
			Console.WriteLine("Getting WriteLine method...");
			var writeLine = module.ImportReference(streamWriter.BaseType.Resolve().Methods.First(m => m.Name.Equals("WriteLine") && m.Parameters.Count == 1 && m.Parameters.All(p => p.ParameterType.FullName.Equals(stringt.FullName))));
			Console.WriteLine("Getting Dispose method...");
			var dispose = module.ImportReference(streamWriter.FindMethod("Dispose"));

			Console.WriteLine("Creating string dumper...");
			proc.InsertBefore(first, proc.Create(OpCodes.Ldstr, "./out.csv"));
			proc.InsertBefore(first, proc.Create(OpCodes.Newobj, streamWriterConstructor));
			foreach(var key in keys) {
				proc.InsertBefore(first, proc.Create(OpCodes.Dup));
				proc.InsertBefore(first, proc.Create(OpCodes.Ldstr, key.ToString()));
				proc.InsertBefore(first, proc.Create(OpCodes.Ldstr, "~,~"));
				proc.InsertBefore(first, proc.Create(OpCodes.Ldc_I4, key));
				proc.InsertBefore(first, proc.Create(OpCodes.Call, parse));
				proc.InsertBefore(first, proc.Create(OpCodes.Call, concat));
				proc.InsertBefore(first, proc.Create(OpCodes.Callvirt, writeLine));
			}
			proc.InsertBefore(first, proc.Create(OpCodes.Ldc_I4_1));
			proc.InsertBefore(first, proc.Create(OpCodes.Callvirt, dispose));
			proc.InsertBefore(first, proc.Create(OpCodes.Ret));

			Directory.CreateDirectory("./StringDumping");
			module.Write("./StringDumping/Lightning.exe");
		}

		static void HandleIntermediary(){
			Console.WriteLine("Generating intermediary EXE...");
			LoadLightning();
			// take Lightning.exe, remap to Intermediary
			CollectIntermediary();
			foreach(var type in CollectNestedTypes(LightningAssembly.MainModule.Types)){
				type.Name = GetIntermediaryForName(type.Name);
				if(type.IsNested) {
					type.IsNestedPublic = true;
				} else {
					type.IsPublic = true;
				}
				foreach(var method in type.Methods){
					// rtspecialname is applied to constructors and operators
					if(!method.IsRuntimeSpecialName)
						method.Name = GetIntermediaryForName(method.Name);
					foreach(var param in method.Parameters)
						param.Name = GetIntermediaryForName(param.Name);
					// references to members in classes with generic parameters don't get remapped automatically
					// so here we update those references ourself
					if(method.Body != null && method.Body.Instructions != null)
						foreach(var instr in method.Body.Instructions){
							if(instr != null && instr.Operand is MethodReference mref && !mref.IsWindowsRuntimeProjection && Intermediary.ContainsKey(mref.Name)) {
								try {
									mref.Name = GetIntermediaryForName(mref.Name);
								} catch(Exception) {
									// it's a "method specification", just replace
									if(!mref.Name.Equals(GetIntermediaryForName(mref.Name))) {
										instr.Operand = new MethodReference(GetIntermediaryForName(mref.Name), mref.ReturnType, mref.DeclaringType);
									}
								}
								// TODO: also take the oppurtunity to replace references to "class_19.method_67" with the actual string
							}

							if(instr != null && instr.Operand is FieldReference fref && Intermediary.ContainsKey(fref.Name))
								fref.Name = (GetIntermediaryForName(fref.Name));//instr.Operand = new FieldReference(GetIntermediaryForName(fref.Name), fref.FieldType, fref.DeclaringType);
						}
					// TODO: map locals
				}
				foreach(var field in type.Fields)
					field.Name = GetIntermediaryForName(field.Name);
				foreach(var generic in type.GenericParameters)
					generic.Name = GetIntermediaryForName(generic.Name);
			}

			LightningAssembly.Write("IntermediaryLightning.exe");
		}

		static void LoadLightning(){
			Console.WriteLine("Reading Lightning.exe...");
			LightningAssembly = AssemblyDefinition.ReadAssembly(PathToLightning);
			Console.WriteLine(LightningAssembly == null ? "Failed to load Lightning.exe" : "Found Lightning executable: " + LightningAssembly.FullName);
		}

		static Collection<TypeDefinition> CollectNestedTypes(Collection<TypeDefinition> topLevel){
			var types = new Collection<TypeDefinition>();
			foreach(var type in topLevel)
				VisitTypes(type, t => types.Add(t));
			return types;
		}

		static void CollectIntermediary(){
			// if a name is a valid CSharp name, it is its own intermediary
			// parse preset intermediary?

			// or gen intermediary
			int classIndex = 0, enumIndex = 0, interfaceIndex = 0, methodIndex = 0, structIndex = 0, delegateIndex = 0, fieldIndex = 0, genericIndex = 0, paramIndex = 0;
			foreach(var type in CollectNestedTypes(LightningAssembly.MainModule.Types)){
				if(!Intermediary.ContainsKey(type.Name) && !type.IsRuntimeSpecialName){
					// its a delegate if it descends from System.MulticastDelegate
					if(type.BaseType?.FullName?.Equals("System.MulticastDelegate") ?? false){
						Intermediary.Add(type.Name, "delegate_" + delegateIndex);
						delegateIndex++;
					}else if(type.IsInterface){
						Intermediary.Add(type.Name, "interface_" + interfaceIndex);
						interfaceIndex++;
					}else if(type.IsEnum){
						Intermediary.Add(type.Name, "enum_" + enumIndex);
						enumIndex++;
					}else if(type.IsValueType){
						Intermediary.Add(type.Name, "struct_" + structIndex);
						structIndex++;
					} else {
						Intermediary.Add(type.Name, "class_" + classIndex);
						classIndex++;
					}
				}
				foreach(var method in type.Methods){
					if(!Intermediary.ContainsKey(method.Name) && !method.IsRuntimeSpecialName){
						Intermediary.Add(method.Name, "method_" + methodIndex);
						methodIndex++;
					}
					foreach(var param in method.Parameters){
						if(!Intermediary.ContainsKey(param.Name)){
							Intermediary.Add(param.Name, "param_" + paramIndex);
							paramIndex++;
						}
					}
					// TODO: map locals
				}
				foreach(var field in type.Fields){
					if(!Intermediary.ContainsKey(field.Name) && !field.IsRuntimeSpecialName){
						Intermediary.Add(field.Name, "field_" + fieldIndex);
						fieldIndex++;
					}
				}
				foreach(var generic in type.GenericParameters){
					if(!Intermediary.ContainsKey(generic.Name)){
						Intermediary.Add(generic.Name, "generic_" + genericIndex);
						genericIndex++;
					}
				}
			}
		}

		static string GetIntermediaryForName(string name){
			// if its already valid or its not in intermediary, leave it
			if(!Intermediary.ContainsKey(name) || Regex.Match(name, "^[a-zA-Z_][a-zA-Z0-9_]*$").Success)
				return name;
			// return intermediary
			return Intermediary[name];
		}

		static void VisitTypes(TypeDefinition top, Action<TypeDefinition> act){
			act(top);
			foreach(var type in top.NestedTypes)
				VisitTypes(type, act);
		}

		static void HandleMerge(){
			// run MonoMod.exe IntermediaryLightning.exe
		}

		static void HandleDevExe(){
			// take IntermediaryLightning.exe, remap to named
		}

		static void HandleDevToRun(){
			// take modded DLL, remap to intermediary
		}

		enum ArgumentParsingMode{
			Argument, MappingPath, IntermediaryPath, StringsPath, LightningPath, MonoModPath, ModPath
		}

		enum RunAction{
			Run, Strings, Intermediary, Merge, Setup, DevExe, DevToRun
		}
	}
}
