using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using Verse;
using HarmonyLib;

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
        
        private Harmony harmony;
        private static int mainThreadId;
        
        public ModBase(ModContentPack content) : base(content) {
            Log.Message("Please");

            mainThreadId = System.Environment.CurrentManagedThreadId;
            
            Startup();
            ToggleActive();
            
            Log.Message("Hi");
            var str = "Modbase:Ctor";
            var zoneBegin = RegisterSourceFunction(str, str.Length);

            Log.Message("Registered");

            var sw = new Stopwatch();
            
            for (int i = 0; i < 100000000; i++) {
                BeginZone(zoneBegin);
                EndZone();
            }
            
            Log.Message("Ho");

            Shutdown();
        }


        // IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        //     
        // }
    }
}