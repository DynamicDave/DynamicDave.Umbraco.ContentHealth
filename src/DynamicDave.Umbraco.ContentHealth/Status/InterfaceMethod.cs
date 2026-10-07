using System.Reflection;

namespace DynamicDave.Umbraco.ContentHealth.Status;

internal static class InterfaceMethod
{
    /// <summary>
    /// Binds a public interface method by name to a delegate. Members that moved to a base interface in a later
    /// Umbraco major break IL compiled against the earlier one (MissingMethodException); binding by name at
    /// runtime, searching the interface and its base interfaces, works on both.
    /// </summary>
    public static TDelegate Bind<TDelegate>(object target, Type interfaceType, string name)
        where TDelegate : Delegate
    {
        var invoke = typeof(TDelegate).GetMethod(nameof(Action.Invoke))!;
        var parameterTypes = invoke.GetParameters().Select(p => p.ParameterType).ToArray();

        var method = new[] { interfaceType }.Concat(interfaceType.GetInterfaces())
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .FirstOrDefault(candidate => candidate.Name == name
                && candidate.ReturnType == invoke.ReturnType
                && candidate.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameterTypes))
            ?? throw new MissingMethodException(interfaceType.FullName, name);

        return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), target, method);
    }
}
