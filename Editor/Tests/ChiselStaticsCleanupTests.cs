#if UNITY_6000_7_OR_NEWER
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Unity.Scripting.LifecycleManagement;

namespace Chisel.Editors.Tests
{
    // Every Chisel type with static state says what entering play mode without a domain reload does with it: reset it
    // (AutoStaticsCleanup) or keep it (NoAutoStaticsCleanup)
    public sealed class ChiselStaticsCleanupTests
    {
        static IEnumerable<Assembly> ChiselAssemblies()
        {
            yield return typeof(Chisel.Core.BrushMesh).Assembly;
            yield return typeof(Chisel.Components.ChiselBrushComponent).Assembly;
            yield return typeof(ChiselEditableOutline).Assembly;
        }

        // a static that can be set: a field, or what an automatic property or an event keeps its value in
        static bool HasStaticState(Type type)
        {
            const BindingFlags kDeclaredStatics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var field in type.GetFields(kDeclaredStatics))
            {
                if (!field.IsLiteral && !field.IsInitOnly)
                    return true;
            }
            return false;
        }

        static bool SaysWhatHappens(Type type)
        {
            return type.IsDefined(typeof(AutoStaticsCleanupAttribute), false) ||
                   type.IsDefined(typeof(NoAutoStaticsCleanupAttribute), false);
        }

        [Test]
        public void EveryTypeWithStaticState_SaysWhetherItIsReset()
        {
            var missing = new List<string>();
            foreach (var assembly in ChiselAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    // made by the compiler or Burst, not written
                    if (type.IsDefined(typeof(CompilerGeneratedAttribute), false) || type.Name.Contains('<') || type.Name.Contains('$'))
                        continue;
                    if (HasStaticState(type) && !SaysWhatHappens(type))
                        missing.Add(type.FullName);
                }
            }
            Assert.That(missing, Is.Empty);
        }
    }
}
#endif
