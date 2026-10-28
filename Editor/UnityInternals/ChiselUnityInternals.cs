using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    // Unity members Chisel uses that have no public API. Each feature that needs some looks them up once through these;
    // one Unity no longer has logs a warning naming it and the feature, which then turns itself off.
    static class ChiselUnityInternals
    {
        const BindingFlags kAll = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        static readonly List<string> s_Missing = new();

        /// <summary>What could not be found so far, as "member (feature)"</summary>
        public static IReadOnlyList<string> Missing => s_Missing;

        public static Type Type(string fullName, string feature)
        {
            var type = typeof(Editor).Assembly.GetType(fullName) ?? typeof(UnityEngine.Object).Assembly.GetType(fullName);
            if (type == null)
                Report(fullName, feature);
            return type;
        }

        public static FieldInfo Field(Type type, string name, string feature)
        {
            var field = type?.GetField(name, kAll);
            if (field == null)
                Report(type, name, feature);
            return field;
        }

        public static PropertyInfo Property(Type type, string name, string feature)
        {
            var property = type?.GetProperty(name, kAll);
            if (property == null)
                Report(type, name, feature);
            return property;
        }

        public static MethodInfo Method(Type type, string name, string feature, params Type[] parameters)
        {
            var method = parameters.Contains(null) ? null : type?.GetMethod(name, kAll, null, parameters, null);
            if (method == null)
                Report(type, name, feature);
            return method;
        }

        // A static method as a delegate of its own signature
        public static T Delegate<T>(Type type, string name, string feature) where T : Delegate
        {
            var parameters = typeof(T).GetMethod("Invoke").GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            var method = Method(type, name, feature, parameters);
            return method == null ? null : (T)method.CreateDelegate(typeof(T));
        }

        public static object EnumValue(Type enumType, string name, string feature)
        {
            if (enumType != null && Enum.IsDefined(enumType, name))
                return Enum.Parse(enumType, name);
            Report(enumType, name, feature);
            return null;
        }

        // A member of a type that is missing itself was reported with the type
        static void Report(Type type, string member, string feature)
        {
            if (type != null)
                Report(type.FullName + "." + member, feature);
        }

        static void Report(string member, string feature)
        {
            var entry = $"{member} ({feature})";
            if (s_Missing.Contains(entry))
                return;
            s_Missing.Add(entry);
            Debug.LogWarning($"Chisel: this version of Unity has no {member}, so {feature} is turned off");
        }
    }
}
