using Microsoft.AspNetCore.Http;

namespace MiniValidationLib.AspNetCore;

internal static class DefaultBinder<TValue>
{
    private static object _itemsKey = new();
    private static readonly RequestDelegate _defaultRequestDelegate = RequestDelegateFactory.Create(DefaultValueDelegate).RequestDelegate;

    public static async Task<TValue?> GetValueAsync(HttpContext httpContext)
    {
        await _defaultRequestDelegate(httpContext);

        return (TValue?)httpContext.Items[_itemsKey];
    }

    private static IResult DefaultValueDelegate(TValue value, HttpContext httpContext)
    {
        httpContext.Items[_itemsKey] = value;
        
        return FakeResult.Instance;
    }

    class FakeResult : IResult
    {
        public static FakeResult Instance { get; } = new FakeResult();

        public Task ExecuteAsync(HttpContext httpContext)
        {
            return Task.CompletedTask;
        }
    }
}
