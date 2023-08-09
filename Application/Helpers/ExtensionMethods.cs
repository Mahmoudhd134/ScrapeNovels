namespace Application.Helpers;

public static class ExtensionMethods
{
    public static string GetFormattedString(this IEnumerable<object> enumerable) =>
        "[" + string.Join(", ", enumerable) + "]";
}