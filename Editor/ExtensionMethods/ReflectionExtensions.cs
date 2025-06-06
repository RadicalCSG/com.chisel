using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public class ReflectedInstanceProperty<T>
    {
        public ReflectedInstanceProperty(PropertyInfo property) { this.property = property; }
		readonly PropertyInfo property;

        public T GetValue(object instance)
        {
            if (property == null ||
                instance == null)
                return default;
            var result = property.GetValue(instance, null);
            if (result == null)
                return default;
            return (T)result;
        }
        public void SetValue(object instance, T value)
        { 
            if (property == null ||
                instance == null)
                return;
            property.SetValue(instance, value, null);            
        }
    }   

    public class ReflectedProperty<T>
    {
        public ReflectedProperty(PropertyInfo property) { this.instance = null; this.property = property; }
        public ReflectedProperty(object instance, PropertyInfo property) { this.instance = instance; this.property = property; }
        PropertyInfo property;
        object instance;

        public static implicit operator T(ReflectedProperty<T> instance) => instance.Value;

        public T Value
        {
            get
            {
                if (property == null)
                    return default;
                var result = property.GetValue(instance, null);
                if (result == null)
                    return default;
                return (T)result;
            }
            set
            {
                if (property == null)
                    return;
                property.SetValue(instance, value, null);
            }
        }
    }

    public class ReflectedField<T>
    {
        public ReflectedField(object instance, FieldInfo field) { this.instance = instance; this.fieldInfo = field; }
		readonly FieldInfo fieldInfo;
		readonly object instance;

        public static implicit operator T(ReflectedField<T> instance) => instance.Value;

        public T Value
        {
            get
            {
                if (fieldInfo == null)
                    return default;
                var result = fieldInfo.GetValue(instance);
                if (result == null)
                    return default;
                return (T)result;
            }
            set
            {
                if (fieldInfo == null)
                    return;
                fieldInfo.SetValue(instance, value);
            }
        }
    }

    public static class ReflectionExtensions
    {
        static Assembly[] s_Assemblies;
        static Type[] s_AllTypes = null;
        static Type[] s_AllNonAbstractTypes = null;
        static Dictionary<string, Type> s_TypeLookups;

        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            if (s_Assemblies != null &&
                s_TypeLookups != null &&
                s_AllNonAbstractTypes != null)
                return;

            s_Assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            {
                var typeList = new List<Type>();
                foreach (var assembly in s_Assemblies)
                    typeList.AddRange(assembly.GetTypes());
                s_AllTypes = typeList.ToArray();
            }

            {
                var nonAbstractTypesList = new List<Type>();
                foreach (var type in s_AllTypes)
                {
                    if (type.IsAbstract ||
                        !type.IsClass)
                        continue;
                    nonAbstractTypesList.Add(type);
                }
                s_AllNonAbstractTypes = nonAbstractTypesList.ToArray();
            }

            {
                s_TypeLookups = new Dictionary<string, Type>();
                foreach (var type in s_AllTypes)
                    s_TypeLookups[type.FullName] = type;
            }
        }

        public static bool HasBaseClass<T>(this Type self)
        {
            while (self != null && self != typeof(object))
            {
                if (self.BaseType == typeof(T))
                    return true;
                self = self.BaseType;
            }
            return false;
        }

        public static bool HasBaseClass(this Type self, Type baseClass)
        {
            while (self != null && self != typeof(object))
            {
                if (self.BaseType == baseClass)
                    return true;
                self = self.BaseType;
            }
            return false;
        }

        public static Type GetGenericBaseClass(this Type self, Type genericBaseClass)
        {
            while (self != null && self != typeof(object))
            {
                var foundBaseClass = self.IsGenericType ? self.GetGenericTypeDefinition() : self;
                if (genericBaseClass == foundBaseClass)
                    return self;
                self = self.BaseType;
            }
            return null;
        }

        public static Type GetGenericBaseInterface(this Type self, Type genericBaseClass)
        {
            var interfaces = self.GetInterfaces();
            if (interfaces == null ||
                interfaces.Length == 0)
                return null;
            for (int i = 0; i < interfaces.Length; i++)
            {
                var foundBaseClass = interfaces[i].IsGenericType ? interfaces[i].GetGenericTypeDefinition() : null;
                if (foundBaseClass == genericBaseClass)
                    return interfaces[i];
            }
            return null;
        }

        public static Type GetTypeByName(string fullName)
        {
            Initialize();
            if (s_TypeLookups == null)
            {
                Debug.LogError("Failed to initialize Reflection information");
                return null;
            }
            if (s_TypeLookups.TryGetValue(fullName, out Type type))
                return type;
			Debug.LogError($"Could not find type for {fullName}");
			return null;
        }

        public static Type[] AllTypes { get { return s_AllTypes; } }

        public static Type[] AllNonAbstractClasses { get { return s_AllNonAbstractTypes; } }

        #region Properties

        public static ReflectedProperty<T> GetStaticProperty<T>(this Type type, string propertyName)
        {
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }
            var property = type.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (property == null)
			{
				Debug.LogError($"Could not find matching property for {propertyName}");
                return null;
			}
            return new ReflectedProperty<T>(property);
        }

        public static ReflectedProperty<T> GetStaticProperty<T>(object instance, string propertyName)
        {
            if (instance == null)
            {
                Debug.LogError("instance == null");
                return null;
            }
            var type = instance.GetType();
            if (type == null)
			{
				Debug.LogError($"Could not find matching property for {propertyName}");
				return null;
            }
            return new ReflectedProperty<T>(type.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static));
        }


        public static ReflectedProperty<T> GetStaticProperty<T>(string fullTypeName, string propertyName) 
        {
            var type = ReflectionExtensions.GetTypeByName(fullTypeName);
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }
            var property = type.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (property == null)
			{
				Debug.LogError($"Could not find matching property for {propertyName}");
			}
            return new ReflectedProperty<T>(property);
        }

        public static ReflectedProperty<T> GetProperty<T>(this Type type, object instance, string propertyName)
        {
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }
            var property = type.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (property == null)
			{
				Debug.LogError($"Could not find matching property for {propertyName}");
			}
            return new ReflectedProperty<T>(instance, property);
        }

        public static ReflectedProperty<T> GetProperty<T>(object instance, string fullTypeName, string propertyName)
        {
            var type = ReflectionExtensions.GetTypeByName(fullTypeName);
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }
            var property = type.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (property == null)
			{
				Debug.LogError($"Could not find matching property for {propertyName}");
				return null;
			}
            return new ReflectedProperty<T>(instance, property);
        }

        public static ReflectedProperty<T> GetProperty<T>(object instance, string propertyName)
        {
            if (instance == null)
            {
                Debug.LogError("instance == null");
                return null;
            }
            var type = instance.GetType();
            if (type == null)
			{
				Debug.LogError($"Could not find matching property for {propertyName}");
				return null;
            }
            return new ReflectedProperty<T>(instance, type.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | System.Reflection.BindingFlags.Instance));
        }


        public static ReflectedInstanceProperty<T> GetProperty<T>(this Type type, string propertyName)
        {
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }

            var property = type.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (property == null)
			{
				Debug.LogError($"Could not find matching property for {propertyName}");
				return null;
			}
            return new ReflectedInstanceProperty<T>(property);
        }

        public static ReflectedInstanceProperty<T> GetProperty<T>(string fullTypeName, string propertyName)
        {
            var type = ReflectionExtensions.GetTypeByName(fullTypeName);
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }
            var property = type.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (property == null)
			{
				Debug.LogError($"Could not find matching property for {propertyName}");
                return null;
			}
            return new ReflectedInstanceProperty<T>(property);
        }
        #endregion

        #region Fields
        public static ReflectedField<T> GetField<T>(this Type type, object instance, string name)
        {
            if (type == null)
            { 
                Debug.LogError("type == null");
                return null;
            }
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
			{
				Debug.LogError($"Could not find matching field for {name}");
				return null;
            }
            return new ReflectedField<T>(instance, field);
        }

        public static ReflectedField<T> GetField<T>(object instance, string fullTypeName, string fieldName)
        {
            var type = ReflectionExtensions.GetTypeByName(fullTypeName);
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                Debug.LogError($"Could not find matching field for {fullTypeName} {fieldName}");
                return null;
            }
            return new ReflectedField<T>(instance, field);
        }

        public static ReflectedField<T> GetField<T>(object instance, string fieldName)
        {
            if (instance == null)
            {
                Debug.LogError("instance == null");
                return null;
            }
            var type = instance.GetType();
            if (type == null)
			{
				Debug.LogError($"Could not find matching field for {fieldName}");
				return null;
            }
            return new ReflectedField<T>(instance, type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public));
        }
        #endregion
        

        #region Fields
        public static ReflectedField<T> GetStaticField<T>(this Type type, string name)
        {
            if (type == null)
            { 
                Debug.LogError("type == null");
                return null;
            }
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (field == null)
			{
				Debug.LogError($"Could not find matching field for {name}");
				return null;
            }
            return new ReflectedField<T>(null, field);
        }

        public static ReflectedField<T> GetStaticField<T>(string fullTypeName, string fieldName)
        {
            var type = ReflectionExtensions.GetTypeByName(fullTypeName);
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                Debug.LogError($"Could not find matching field for {fullTypeName} {fieldName}");
                return null;
            }
            return new ReflectedField<T>(null, field);
        }
        #endregion


        public static MethodInfo GetStaticMethod(this Type type, string name)
        {
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }
            var field = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreReturn); 
			if (field == null)
			{
				Debug.LogError($"Could not find matching field for {name}");
				return null;
			}
            return field;
		}

        public static MethodInfo GetStaticMethod(this Type type, string name, int parameterCount)
        {
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }

            var flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreReturn;
            var allMethods = type.GetMethods(name, flags);
            if (allMethods == null)
			{
				Debug.LogError($"Could not find matching method for {name}");
				return null;
            }

            foreach(var method in allMethods)
            {
                if (method.GetParameters().Length == parameterCount)
                    return method;
            }

			Debug.LogError($"Could not find matching method for {name}");
			return null;
        }


        public static MethodInfo GetMethod(this Type type, string name)
        {
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }
            var method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public);
			if (method == null)
			{
				Debug.LogError($"Could not find matching method for {name}");
				return null;
			}
			return method;
		}

        public static MethodInfo GetStaticMethod(this Type type, string name, params Type[] parameterTypes)
        {
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }

            const BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Static | BindingFlags.IgnoreReturn;
            var result = type.GetMethod(name, bindingFlags, null, parameterTypes, null);
            if (result == null)
            {
                var foundMethods = type.GetMethods(name, bindingFlags);
                if (foundMethods.Length == 0)
				{
					Debug.LogError($"Could not find matching method for {name}");
					return null;
                }
                if (foundMethods.Length == 1)
                {/*
                    var foundParams = foundMethods[0].GetParameters();
                    for (int i = 0; i < foundParams.Length; i++)
                    {
                        Debug.Log($"{i}: {foundParams[i]}");
                    }
                    for (int i = 0; i < parameterTypes.Length; i++)
                    {
                        Debug.Log($"{i}: {parameterTypes[i]}");
                    }*/
                    return foundMethods[0];
				}
				Debug.LogError("foundMethods.Length > 1");
				return null;
            }
            return result;
        }

        public static MethodInfo GetMethod(this Type type, string name, params Type[] parameterTypes)
        {
            if (type == null)
			{
				Debug.LogError($"Could not find matching method for {name}");
				return null;
            }
            var method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public, null, parameterTypes, null);
            if (method == null)
			{
				Debug.LogError($"Could not find matching method for {name}");
			}
            return method;
        }

        public static MethodInfo[] GetMethods(this Type type, string name, BindingFlags bindingFlags)
        {
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }
            var methods = (from method in type.GetMethods(bindingFlags) where method.Name == name select method).ToArray();
			if (methods == null)
			{
				Debug.LogError($"Could not find matching methods for {name}");
			}
            return methods;
		}

        public static T CreateDelegate<T>(MethodInfo methodInfo) where T : Delegate
        {
            if (methodInfo == null)
            {
                Debug.LogError("methodInfo == null");
                return null;
            }
            var method = (T)Delegate.CreateDelegate(typeof(T), null, methodInfo, true);
			if (method == null)
			{
				Debug.LogError($"Could not create delegate for method {methodInfo.Name}");
			}
            return method;
		}

        public static T CreateDelegate<T>(this Type type, string methodName) where T : Delegate
        {
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }

            var parameterTypes = (from param in typeof(T).GetMethod("Invoke").GetParameters() select param.ParameterType).ToArray();
            var methodInfo = type.GetStaticMethod(methodName, parameterTypes);
            if (methodInfo == null)
            {
                Debug.LogError($"methodInfo == null (methodName: {methodName})");
                return null;
            }
            var method = (T)Delegate.CreateDelegate(typeof(T), null, methodInfo, true);
			if (method == null)
			{
				Debug.LogError($"Could not create delegate for method {methodInfo.Name}");
			}
			return method;
        }


        public static T CreateDelegate<T>(this Type type, string methodName, params Type[] parameterTypes) where T : Delegate
        {
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }
            var methodInfo = type.GetStaticMethod(methodName, parameterTypes);
            if (methodInfo == null)
            {
                Debug.LogError($"methodInfo == null (methodName: {methodName})");
                return null;
            }
            var method = (T)Delegate.CreateDelegate(typeof(T), null, methodInfo, true);
			if (method == null)
			{
				Debug.LogError($"Could not create delegate for method {methodInfo.Name}");
			}
			return method;
		}

        public static T CreateDelegate<T>(string fullTypeName, string methodName) where T : Delegate
        {
            var type = ReflectionExtensions.GetTypeByName(fullTypeName);
            if (type == null)
            {
                Debug.LogError($"type == null (fullTypeName: {fullTypeName})");
                return null;
            }
            var parameterTypes = (from param in typeof(T).GetMethod("Invoke").GetParameters() select param.ParameterType).ToArray();
            var methodInfo = type.GetStaticMethod(methodName, parameterTypes);
            if (methodInfo == null)
            {
                Debug.LogError($"methodInfo == null (methodName: {methodName})");
                return null;
            }
            try
            {
				var method = (T)Delegate.CreateDelegate(typeof(T), null, methodInfo, true);
				if (method == null)
				{
					Debug.LogError($"Could not create delegate for method {methodInfo.Name}");
				}
				return method;
			}
            catch (Exception ex)
            {
                Debug.LogError($"{methodName}'s signature might've been modified between Unity versions");
                throw ex;
            }
        }

        public static T CreateDelegate<T>(object instance, MethodInfo methodInfo) where T : Delegate
        {
            if (instance == null)
            {
                Debug.LogError("instance == null");
                return null;
            }
            if (methodInfo == null)
            {
                Debug.LogError("methodInfo == null");
                return null;
            }
			var method = (T)Delegate.CreateDelegate(typeof(T), instance, methodInfo, true);
			if (method == null)
			{
				Debug.LogError($"Could not create delegate for method {methodInfo.Name}");
			}
			return method;
		}

        public static T CreateDelegate<T>(object instance, string methodName) where T : Delegate
        {
            if (instance == null)
            {
                Debug.LogError("instance == null");
                return null;
            }
            var type = instance.GetType();
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }
            var parameterTypes = (from param in typeof(T).GetMethod("Invoke").GetParameters() select param.ParameterType).ToArray();
            var methodInfo = type.GetMethod(methodName, parameterTypes);
            if (methodInfo == null)
            {
                Debug.LogError($"methodInfo == null (methodName: {methodName})");
                return null;
            }
			var method = (T)Delegate.CreateDelegate(typeof(T), instance, methodInfo, true);
			if (method == null)
			{
				Debug.LogError($"Could not create delegate for method {methodInfo.Name}");
			}
			return method;
		}

        public static T CreateDelegate<T>(this Type type, object instance, string methodName) where T : Delegate
        {
            if (type == null)
            {
                Debug.LogError("type == null");
                return null;
            }
            if (instance == null)
            {
                Debug.LogError("instance == null");
                return null;
            }
            var parameterTypes = (from param in typeof(T).GetMethod("Invoke").GetParameters() select param.ParameterType).ToArray();
            var methodInfo = type.GetMethod(methodName, parameterTypes);
            if (methodInfo == null)
            {
                Debug.LogError($"methodInfo == null (methodName: {methodName})");
                return null;
            }
			var method = (T)Delegate.CreateDelegate(typeof(T), instance, methodInfo, true);
			if (method == null)
			{
				Debug.LogError($"Could not create delegate for method {methodInfo.Name}");
			}
			return method;
		}
    }
}
