using System.ComponentModel;

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        public bool ReturnValue { get; }
        public NotNullWhenAttribute(bool returnValue)
        {
            ReturnValue = returnValue;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, AllowMultiple = true, Inherited = false)]
    internal sealed class NotNullIfNotNullAttribute : Attribute
    {
        public string ParameterName { get; }
        public NotNullIfNotNullAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }
    }
}

namespace System
{
    internal static class CompatHelpers
    {
        public static string[] Split(this string value, char key)
        {
            return value.Split(new[] { key });
        }

        public static string[] Split(this string value, char key, int count)
        {
            return value.Split(new[] { key }, count);
        }

        public static bool EndsWith(this string value, char key)
        {
            if (value.Length == 0)
                return false;

            char last = value[value.Length - 1];

            if (last == key)
                return true;
            else
                return false;
        }

        public static bool StartsWith(this string value, char key)
        {
            if (value.Length == 0)
                return false;

            char last = value[0];

            if (last == key)
                return true;
            else
                return false;
        }
    }
}
#endif

