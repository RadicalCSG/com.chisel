using System;

namespace Chisel.Editors
{
    public static class ReflectionExtensions
    {
        // The generic base class a type derives from that is made from genericBaseClass, or null
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
    }
}