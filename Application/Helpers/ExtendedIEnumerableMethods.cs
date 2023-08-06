namespace Application.Helpers;

public static class ExtendedIEnumerableMethods
{
    public static string GetFormattedString<T>(this IEnumerable<T> enumerable)
    {
        return "[" + string.Join("", string.Join(", ", enumerable).SkipLast(2)) + "]";
    }
}