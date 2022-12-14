using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;
using HarmonyLib;
using MonoMod.Utils;
using MonoMod.Utils.Cil;
using UnityEngine;

namespace Profiler
{
    [SuppressUnmanagedCodeSecurity]
    class ModBase : Mod  {
        
        // Begin a zone for a particular method uniquely identified by zoneID
        [System.Runtime.InteropServices.DllImport("TracyProfiler.so")]
        [SuppressUnmanagedCodeSecurity]
        internal static extern void BeginZone(long zoneId);
        
        // End the zone for the latest method pushed to the stack
        [System.Runtime.InteropServices.DllImport("TracyProfiler.so")]
        [SuppressUnmanagedCodeSecurity]
        internal static extern void EndZone();
        
        [System.Runtime.InteropServices.DllImport("TracyProfiler.so")]
        internal static extern long RegisterSourceFunction([MarshalAs(UnmanagedType.LPStr)] string methodName, int strLen);

        [System.Runtime.InteropServices.DllImport("TracyProfiler.so")]
        internal static extern void Shutdown();
        
        [System.Runtime.InteropServices.DllImport("TracyProfiler.so")]
        internal static extern void Startup();
        
        [System.Runtime.InteropServices.DllImport("TracyProfiler.so")]
        internal static extern void ToggleActive();
        
        internal static Harmony harmony;
        internal UnityEngine.GameObject obj;
        
        private static MethodInfo BeginZoneInfo = AccessTools.Method(typeof(ModBase), nameof(BeginZone));
        private static MethodInfo EndZoneInfo = AccessTools.Method(typeof(ModBase), nameof(EndZone));
        private static MethodInfo CurrentThreadId = AccessTools.PropertyGetter(typeof(Environment), nameof(Environment.CurrentManagedThreadId));
        
        public ModBase(ModContentPack content) : base(content) {
            harmony = new Harmony("wiri.tracy_profiler_rimworld");
            
            obj = new UnityEngine.GameObject("TracyProfilerComponent");
            obj.AddComponent<TracyProfilerComponent>();
            UnityEngine.Object.DontDestroyOnLoad(obj);

            harmony.Patch(AccessTools.Method(typeof(Root), nameof(Root.Shutdown)),
                          prefix: new HarmonyMethod(typeof(ModBase), nameof(ShutdownPrefix)));
        }

        public static void ShutdownPrefix() {
            Shutdown();
        }

        internal static string GetSignature(MethodBase method, bool showParameters = true)
        {
            var firstParam = true;
            var sigBuilder = new StringBuilder(40);
        
            string mKey;
            if (method.ReflectedType != null) mKey = TypeName(method.ReflectedType, true) + ":" + method.Name;
            else mKey = TypeName(method.DeclaringType, true) + ":" + method.Name;
            sigBuilder.Append(mKey);
        
            // Add method generics
            if(method.IsGenericMethod)
            {
                sigBuilder.Append("<");
                foreach(var g in method.GetGenericArguments())
                {
                    if (firstParam) firstParam = false;
                    else sigBuilder.Append(", ");
        
                    sigBuilder.Append(TypeName(g));
                }
                sigBuilder.Append(">");
            }
        
        
            if (showParameters)
            {
                sigBuilder.Append("(");
        
                firstParam = true;
                foreach (var param in method.GetParameters())
                {
                    if (firstParam)
                    {
                        firstParam = false;
                        if (method.IsDefined(typeof(ExtensionAttribute), false))
                        {
                            sigBuilder.Append("this ");
                        }
                    }
                    else
                        sigBuilder.Append(", ");
        
                    if (param.ParameterType.IsByRef)
                        sigBuilder.Append("ref ");
                    else if (param.IsOut)
                        sigBuilder.Append("out ");
        
                    sigBuilder.Append(TypeName(param.ParameterType));
                }
                sigBuilder.Append(")");
            }
        
        
            return sigBuilder.ToString();
        }
        internal static bool IsGenericType(Type type)
        {
            return (type.GetGenericArguments()?.Any() ?? false) && type.FullName.Contains('`');
        }
        
        internal static string TypeName(Type type, bool fullName = false)
        {
            var nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType != null) return nullableType.Name + "?";
        
            var tName = fullName ? type.FullName : type.Name;
        
            if (IsGenericType(type))
            {
                var sb = new StringBuilder(tName.Substring(0, tName.IndexOf('`')));
                sb.Append('<');
                var first = true;
                foreach (var t in type.GenericTypeArguments)
                {
                    if (!first)
                        sb.Append(", ");
        
                    sb.Append(TypeName(t));
                    first = false;
                }
                sb.Append('>');
                return sb.ToString();
            }
            else
            {
                string ReplaceOccurence(string typeName, string to)
                {
                    return type.Name.Replace(typeName, to);
                }
        
                // This finds things like "String[]" as well as just String, which is why its not in the switch
                if (type.Name.Contains("String")) return ReplaceOccurence("String", "string");
                if (type.Name.Contains("Int32")) return ReplaceOccurence("Int32", "int");
                if (type.Name.Contains("Object")) return ReplaceOccurence("Object", "object");
                if (type.Name.Contains("Boolean"))  return ReplaceOccurence("Boolean", "bool");
                if (type.Name.Contains("Decimal")) return ReplaceOccurence("Decimal", "decimal");
        
                if (type.Name == "Void") return "void"; 
        
                return fullName ? type.FullName : type.Name;
            }
        }

        private static Dictionary<MethodBase, long> seenMeths = new Dictionary<MethodBase, long>();

        public static IEnumerable<CodeInstruction> Transpiler(MethodBase __originalMethod, IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            // return instructions;
            var methStr = GetSignature(__originalMethod, false);
            
            if (!seenMeths.TryGetValue(__originalMethod, out var methIdx)) {
                methIdx = RegisterSourceFunction(methStr, methStr.Length);
                seenMeths.Add(__originalMethod, methIdx);
            }
            
            
            var insts = instructions.ToList();
            var initialSkipLabel = generator.DefineLabel();
            
            // yield return new CodeInstruction(OpCodes.Call, CurrentThreadId);
            // yield return new CodeInstruction(OpCodes.Stloc, threadVal);
            //
            // yield return new CodeInstruction(OpCodes.Ldloc, threadVal);
            // yield return new CodeInstruction(OpCodes.Ldsfld, TargetThreadId);
            // yield return new CodeInstruction(OpCodes.Bne_Un, initialSkipLabel);
            
            yield return new CodeInstruction(OpCodes.Ldc_I8, methIdx);
            yield return new CodeInstruction(OpCodes.Call, BeginZoneInfo);
            
            insts[0].WithLabels(initialSkipLabel);
            
            foreach (var inst in insts) {
                
                if (inst.opcode == OpCodes.Ret) {
                    var label = generator.DefineLabel();
                    
                    // yield return new CodeInstruction(OpCodes.Ldloc, threadVal).MoveLabelsFrom(inst);
                    // yield return new CodeInstruction(OpCodes.Ldsfld, TargetThreadId);
                    // yield return new CodeInstruction(OpCodes.Bne_Un, label);
                    yield return new CodeInstruction(OpCodes.Call, EndZoneInfo).MoveLabelsFrom(inst);
                    
            
                    yield return inst.WithLabels(label);
                    
                    continue;
                }
            
                yield return inst;
            }
        }
    }

    public class TracyProfilerComponent : UnityEngine.MonoBehaviour
    {
        private bool state = false;
        private bool firstTime = true;

        static bool IsSimpleMethod(MethodBase method) {
            var insts = PatchProcessor.GetOriginalInstructions(method);
            return insts.Count < 15 && insts.All(a => (a.operand is MethodBase methodBase &&
                                                       (methodBase == method || IsSimpleMethod(methodBase)) ||
                                                       a.operand is not MethodBase));
        }
        
        void OnGUI() {
            var text = state ? "Disable" : "Enable";
    
            if(UnityEngine.GUI.Button(new UnityEngine.Rect(10, 10, text.GetWidthCached(), 50), text)) {
                if (firstTime) {
                    
                    var transpiler = new HarmonyMethod(typeof(ModBase), nameof(ModBase.Transpiler));
                    foreach(var method in typeof(Pawn).Assembly
                                                      .GetTypes()
                                                      .Where(t => !t.IsGenericType && !t.HasAttribute<CompilerGeneratedAttribute>())
                                                      .SelectMany(t => AccessTools.GetDeclaredMethods(t))
                                                      .Where(m => !(!m.HasMethodBody() || m.IsGenericMethod || m.ContainsGenericParameters || m.IsGenericMethodDefinition || m.IsSecurityCritical || m.IsAbstract))) {
                        // Harmony is bugged currently.
                        if (method.DeclaringType.IsValueType) continue;
                        if (IsSimpleMethod(method)) continue;

                        ModBase.harmony.Patch(method, transpiler: transpiler);
                    }
                    
                    firstTime = false;
                    ModBase.Startup();
                }
                
                ModBase.ToggleActive();
                state = !state;
            }
        }
    }
}