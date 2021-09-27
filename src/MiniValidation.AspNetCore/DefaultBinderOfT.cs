using Microsoft.AspNetCore.Http;

namespace MiniValidationLib.AspNetCore;

internal static class DefaultBinder<TValue>
{
    private static readonly RequestDelegate _defaultRequestDelegate = RequestDelegateFactory.Create(DefaultValueDelegate).RequestDelegate;

    public static async Task<TValue?> GetValueAsync(HttpContext httpContext)
    {
        var boundValueHttpContext = new BoundValueHttpContext<TValue>(httpContext);

        await _defaultRequestDelegate(boundValueHttpContext);

        return boundValueHttpContext.Value;
    }

    private static IResult DefaultValueDelegate(TValue value, HttpContext httpContext)
    {
        if (httpContext is BoundValueHttpContext<TValue> boundValueHttpContext)
        {
            boundValueHttpContext.Value = value;
        }
        
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
