namespace System.Threading.Tasks;

internal static class ValueTaskExtensions
{
#if NET6_0_OR_GREATER
    public static void Discard(this ValueTask valueTask)
    {
        if (!valueTask.IsCompleted)
        {
            _ = valueTask.AsTask();
        }
    }

    public static void Discard<T>(this ValueTask<T> valueTask)
    {
        if (!valueTask.IsCompleted)
        {
            _ = valueTask.AsTask();
        }
    }
#endif
}
