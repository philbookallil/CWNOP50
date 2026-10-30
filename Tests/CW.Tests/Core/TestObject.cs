using System.Reflection;
using System.Runtime.CompilerServices;

namespace CW.Tests.Core;

/// <summary>
/// A service, factory or controller built without its constructor, with only the fields a test needs
/// </summary>
internal static class TestObject
{
    /// <summary>
    /// An instance whose fields are all unset
    /// </summary>
    public static T Create<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    /// <summary>
    /// Sets a field declared on the object's type or a base type, the most derived first
    /// </summary>
    /// <param name="target">The object</param>
    /// <param name="name">Field name</param>
    /// <param name="value">Value</param>
    /// <param name="optional">True to ignore a field the type does not have</param>
    public static void Set(object target, string name, object value, bool optional = false)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field == null)
                continue;

            field.SetValue(target, value);
            return;
        }

        if (!optional)
            throw new MissingFieldException(target.GetType().Name, name);
    }

    /// <summary>
    /// Calls a protected or private instance method that returns a task
    /// </summary>
    public static async Task<TResult> InvokeAsync<TResult>(object target, string method, params object?[] arguments)
    {
        var info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;

        return await (Task<TResult>)info.Invoke(target, arguments)!;
    }
}
