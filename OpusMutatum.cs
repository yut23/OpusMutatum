using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpusMutatum {

	public class OpusMutatum {

		// For intermediary or devExe
		static string PathToLightning = "./Lightning.exe";
		static string PathToModdedLightning = "./ModdedLightning.exe";

		// for merge
		static string PathToMonoMod = "./MonoMod.exe";

		static List<string> MappingPaths = new List<string>();
		static List<string> IntermediaryPaths = new List<string>();
		static List<string> StringsPaths = new List<string>();

		static AssemblyDefinition LightningAssembly, ModdedLightningAssembly;

		static Dictionary<string, string> Intermediary = new Dictionary<string, string>();
		static Dictionary<string, string> Mappings = new Dictionary<string, string>();
		static Dictionary<int, string> Strings = new Dictionary<int, string>();

		static void Main(string[] args) {
			ArgumentParsingMode current = ArgumentParsingMode.Argument;
			RunAction action = RunAction.Setup;
			foreach(var arg in args) {
				switch(current) {
					case ArgumentParsingMode.Argument:
						// check if its "run", "strings", "intermediary", merge", "setup", "devExe"
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
						else if(arg.Equals("--mappings"))
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
					default:
						Console.WriteLine("Invalid argument \"" + arg + "\"!");
						break;
				}
			}

			if(StringsPaths.Count == 0) {
				StringsPaths.Add("./StringDumping/out.csv");
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
					case RunAction.Run:
					default:
						HandleRun();
						break;
				}
			} catch(Exception e) {
				Console.WriteLine("Error executing task:");
				Console.WriteLine(e.ToString());
			}
			Console.WriteLine("Done.");
			// keep command line open
			Console.ReadKey();
		}

		static void HandleRun() {
			// just run MONOMODDED_IntermediaryLightning.exe
		}

		static void HandleStrings() {
			// TODO: some config file for main method & parse method?
			
			Console.WriteLine("Dumping strings...");
			LoadLightning();
			// take Lightning.exe, find all refs to string parser: "#=qQ3boY4a6o2O2sPtKvJtj_Q6y77XoLuLRv$4EsOcRQr4="."#=qhwVTryR65imID$n_uKTBPA=="
			var module = LightningAssembly.MainModule;
			var parse = module.FindMethod("#=q7nvcBd_hWOx6ogq743lZkyDITddtOR9ugDU9NV1hD8Y=", "#=qb3HWBkVlFVubfVOAwuy8rw==");

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

			var mainMethod = module.FindMethod("#=qbZYLMl8F9alVNlRAO03dOw==", "#=qAqM7sFzcD4RfaoNvmBH0bw==");
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

			Console.WriteLine("Running string dumper...");
			// run the string dumper automatically
			RunAndWait(Path.Combine(Directory.GetCurrentDirectory(), "StringDumping", "Lightning.exe"), "");
		}

		static void HandleIntermediary() {
			// TODO: MonoMod relinking?
			Console.WriteLine("Generating intermediary EXE...");
			LoadLightning();
			LoadStrings();
			// take Lightning.exe, remap to Intermediary
			CollectIntermediary();
			List<(Instruction, int)> stringsToBeInlined = new List<(Instruction, int)>();
			DoRemap(GetIntermediaryForName, Intermediary.ContainsKey, CollectNestedTypes(LightningAssembly.MainModule.Types),
				(mref, instr) => {
					if(mref.Name.Equals("method_67") && mref.Parameters.Count == 1)
						if(instr.Previous.OpCode == OpCodes.Ldc_I4)
							stringsToBeInlined.Add((instr, (int)instr.Previous.Operand));
				},
				type => {
					if(type.IsNested)
						type.IsNestedPublic = true;
					else
						type.IsPublic = true;

				});
			if(stringsToBeInlined.Count > 0)
				foreach(var stringFunc in stringsToBeInlined)
					if(Strings.ContainsKey(stringFunc.Item2)) {
						stringFunc.Item1.Previous.Set(OpCodes.Nop, null);
						stringFunc.Item1.Set(OpCodes.Ldstr, Strings[stringFunc.Item2]);
					}

			LightningAssembly.Write("IntermediaryLightning.exe");
		}

		static void LoadLightning() {
			Console.WriteLine("Reading Lightning.exe...");
			LightningAssembly = AssemblyDefinition.ReadAssembly(PathToLightning);
			Console.WriteLine(LightningAssembly == null ? "Failed to load Lightning.exe" : "Found Lightning executable: " + LightningAssembly.FullName);
		}

		static void LoadModdedLightning() {
			Console.WriteLine("Reading modded Lightning.exe...");
			ModdedLightningAssembly = AssemblyDefinition.ReadAssembly(PathToModdedLightning);
			Console.WriteLine(ModdedLightningAssembly == null ? $"Failed to load modded Lightning.exe at \"{PathToModdedLightning}\"" : "Found modded Lightning executable: " + ModdedLightningAssembly.FullName);
		}

		static void LoadStrings() {
			if(StringsPaths.Count > 0) {
				foreach(var path in StringsPaths) {
					if(!File.Exists(path))
						continue;
					string[] lines = File.ReadAllLines(path);
					bool hadSplit = true; // multi-line strings
					int lastIndex = 0;
					foreach(string line in lines) {
						string[] split = line.Split(new string[] { "~,~" }, StringSplitOptions.None);
						if(split.Length > 1) {
							// if we *can* split on this line, then we're definitely at the first line of a string
							hadSplit = true;
							try {
								lastIndex = int.Parse(split[0]);
								if(!string.IsNullOrWhiteSpace(split[1]))
									Strings[lastIndex] = split[1];
							} catch(FormatException) { }
						} else if(!hadSplit) {
							// if this line isn't blank (or even if it is), then we're continuing a previous multi-line string, so append
							Strings[lastIndex] = Strings[lastIndex] + "\n" + line;
						}
					}
					// these are ridden with special characters
					// we can't just trim normally, see "fmt " breaking WAV loading
					// so we manually regex replace: [^a-zA-Z0-9_.:\n;'*()+<>\\{}# ,~/$\[\]\-©!"?&’\t=—@%●●●●…—……]
					// this kills other languages, a better solution is needed in the future
					foreach(int key in Strings.Keys.ToList())
						Strings[key] = Regex.Replace(Strings[key], "[^a-zA-Z0-9_.:\n;'*()+<>\\\\{}# ,~/$\\[\\]\\-©!\" ? &’\t =—@%●●●●…—……]", "");
				}
				Console.WriteLine("Loaded " + Strings.Count + " strings.");
			}
		}

		public static void DoRemap(Func<string, TypeDefinition, string> remapper, Func<string, bool> remapChecker, Collection<TypeDefinition> types, Action<MethodReference, Instruction> onMethodReference, Action<TypeDefinition> onTypeDefinition) {
			foreach(var type in types) {
				type.Name = remapper(type.Name, type);
				onTypeDefinition(type);
				foreach(var method in type.Methods) {
					// rtspecialname is applied to constructors and operators
					if(!method.IsRuntimeSpecialName)
						method.Name = remapper(method.Name, type);
					foreach(var param in method.Parameters)
						param.Name = remapper(param.Name, type);
					// references to members in classes with generic parameters don't get remapped automatically
					// so here we update those references ourself
					if(method.Body != null && method.Body.Instructions != null) {
						foreach(var instr in method.Body.Instructions) {
							if(instr != null && instr.Operand is MethodReference mref && !mref.IsWindowsRuntimeProjection) {
								if(remapChecker(mref.Name)) {
									if(mref.IsGenericInstance)
										mref = ((GenericInstanceMethod)mref).GetElementMethod();
									mref.Name = remapper(mref.Name, type);
								}
								// also take the oppurtunity to replace references to "class_19.method_67" with the actual string
								onMethodReference(mref, instr);
							}

							if(instr != null && instr.Operand is FieldReference fref && remapChecker(fref.Name))
								fref.Name = remapper(fref.Name, type);
						}
					}
					foreach(var attr in method.CustomAttributes)
						if(attr.HasConstructorArguments)
							foreach(var arg in attr.ConstructorArguments)
								if(arg.Type.Name.Equals("Type"))
									(arg.Value as TypeReference).Name = remapper((arg.Value as TypeReference).Name, type);
					// TODO: map locals
				}
				foreach(var field in type.Fields)
					field.Name = remapper(field.Name, type);
				foreach(var generic in type.GenericParameters)
					generic.Name = remapper(generic.Name, type);
			}
		}

		static Collection<TypeDefinition> CollectNestedTypes(Collection<TypeDefinition> topLevel) {
			var types = new Collection<TypeDefinition>();
			foreach(var type in topLevel)
				VisitTypes(type, t => types.Add(t));
			return types;
		}

		static void CollectIntermediary() {
			// if a name is a valid CSharp name, it is its own intermediary
			// parse preset intermediary?

			// or gen intermediary
			int classIndex = 0, enumIndex = 0, interfaceIndex = 0, methodIndex = 0, structIndex = 0, delegateIndex = 0, fieldIndex = 0, genericIndex = 0, paramIndex = 0;
			foreach(var type in CollectNestedTypes(LightningAssembly.MainModule.Types)) {
				if(!Intermediary.ContainsKey(type.Name) && !type.IsRuntimeSpecialName) {
					// its a delegate if it descends from System.MulticastDelegate
					if(type.BaseType?.FullName?.Equals("System.MulticastDelegate") ?? false) {
						Intermediary.Add(type.Name, "delegate_" + delegateIndex);
						delegateIndex++;
					} else if(type.IsInterface) {
						Intermediary.Add(type.Name, "interface_" + interfaceIndex);
						interfaceIndex++;
					} else if(type.IsEnum) {
						Intermediary.Add(type.Name, "enum_" + enumIndex);
						enumIndex++;
					} else if(type.IsValueType) {
						Intermediary.Add(type.Name, "struct_" + structIndex);
						structIndex++;
					} else {
						Intermediary.Add(type.Name, "class_" + classIndex);
						classIndex++;
					}
				}
				foreach(var method in type.Methods) {
					if(!Intermediary.ContainsKey(method.Name) && !method.IsRuntimeSpecialName) {
						Intermediary.Add(method.Name, "method_" + methodIndex);
						methodIndex++;
					}
					foreach(var param in method.Parameters) {
						if(!Intermediary.ContainsKey(param.Name)) {
							Intermediary.Add(param.Name, "param_" + paramIndex);
							paramIndex++;
						}
					}
					// TODO: map locals
				}
				foreach(var field in type.Fields) {
					if(!Intermediary.ContainsKey(field.Name) && !field.IsRuntimeSpecialName) {
						Intermediary.Add(field.Name, "field_" + fieldIndex);
						fieldIndex++;
					}
				}
				foreach(var generic in type.GenericParameters) {
					if(!Intermediary.ContainsKey(generic.Name)) {
						Intermediary.Add(generic.Name, "generic_" + genericIndex);
						genericIndex++;
					}
				}
			}
		}

		static string GetIntermediaryForName(string name, TypeDefinition owner) {
			// if its already valid or its not in intermediary, leave it
			if(!Intermediary.ContainsKey(name) || Regex.Match(name, "^[a-zA-Z_\\`][a-zA-Z0-9_\\`]*$").Success)
				return name;
			// return intermediary
			return Intermediary[name];
		}

		static void VisitTypes(TypeDefinition top, Action<TypeDefinition> act) {
			act(top);
			foreach(var type in top.NestedTypes)
				VisitTypes(type, act);
		}

		static void HandleMerge() {
			// run "./MonoMod.exe IntermediaryLightning.exe Quintessential.dll ModdedLightning.exe"
			// then "./MonoMod.RuntimeDetour.HookGen.exe ModdedLightning.exe"
			if(File.Exists("./MonoMod.exe")) {
				if(File.Exists("./Quintessential.dll")) {
					// TODO: check if there's already quintessential with this version
					Console.WriteLine("Modding Lightning...");
					RunAndWait(Path.Combine(Directory.GetCurrentDirectory(), "MonoMod.exe"), "IntermediaryLightning.exe Quintessential.dll ModdedLightning.exe");
					if(!File.Exists("./ModdedLightning.exe")) {
						Console.WriteLine("Failed to mod!");
						return;
					}
					if(File.Exists("./MonoMod.RuntimeDetour.HookGen.exe")) {
						Console.WriteLine("Generating hooks...");
						RunAndWait(Path.Combine(Directory.GetCurrentDirectory(), "MonoMod.RuntimeDetour.HookGen.exe"), "ModdedLightning.exe");
					}
				} else {
					Console.WriteLine("Quintessential not found, skipping merging.");
				}
			} else {
				Console.WriteLine("MonoMod not found, skipping merging.");
			}
		}

		static void HandleDevExe() {
			// take ModdedLightning.exe, remap to named
			Console.WriteLine("Generating dev EXE...");
			LoadModdedLightning();
			LoadMappings();
			DoRemap(GetNamedForIntermediary, Mappings.ContainsKey, CollectNestedTypes(ModdedLightningAssembly.MainModule.Types), (mref, instr) => { }, typeDef => { });
			ModdedLightningAssembly.Write("DevLightning.exe");
		}

		static void RunAndWait(string file, string param){
			Console.WriteLine("Running " + file);
			if(!File.Exists(file)) {
				Console.WriteLine("Failed to run " + file + ", file not found.");
				return;
			}
			Process process = new Process();
			process.StartInfo.FileName = "\"" + (file) + "\"";
			process.StartInfo.Arguments = "\"" + (param) + "\"";
			process.Start();
			process.WaitForExit();

		}

		static string GetNamedForIntermediary(string intermediary, TypeDefinition owner) {
			if(!Mappings.ContainsKey(intermediary))
				return intermediary;
			string name = Mappings[intermediary];
			if(name.Contains(".")) {
				string[] split = name.Split('.');
				name = split[split.Length - 1];
			}

			return name;
		}

		static void LoadMappings() {
			foreach(var path in MappingPaths) {
				if(!File.Exists(path))
					continue;
				string[] lines = File.ReadAllLines(path);
				foreach(var line in lines) {
					if(string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
						continue;
					if(!line.Contains(","))
						Console.WriteLine($"Invalid line in {path}: \"{line}\", missing comma.");
					string[] parts = line.Split(',');
					Mappings[parts[0]] = parts[1];
				}
			}
		}

		enum ArgumentParsingMode{
			Argument, MappingPath, IntermediaryPath, StringsPath, LightningPath, MonoModPath
		}

		enum RunAction{
			Run, Strings, Intermediary, Merge, Setup, DevExe
		}
	}
}
