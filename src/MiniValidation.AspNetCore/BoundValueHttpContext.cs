using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace MiniValidationLib.AspNetCore;

internal class BoundValueHttpContext<TValue> : HttpContext
{
    private readonly HttpContext _context;

    public BoundValueHttpContext(HttpContext httpContext)
    {
        _context = httpContext;
    }

    public TValue? Value { get; set; }

    public override IFeatureCollection Features => _context.Features;
    public override HttpRequest Request => _context.Request;
    public override HttpResponse Response => _context.Response;
    public override ConnectionInfo Connection => _context.Connection;
    public override WebSocketManager WebSockets => _context.WebSockets;
    public override ClaimsPrincipal User { get => _context.User; set => _context.User = value; }
    public override IDictionary<object, object?> Items { get => _context.Items; set => _context.Items = value; }
    public override IServiceProvider RequestServices { get => _context.RequestServices; set => _context.RequestServices = value; }
    public override CancellationToken RequestAborted { get => _context.RequestAborted; set => _context.RequestAborted = value; }
    public override string TraceIdentifier { get => _context.TraceIdentifier; set => _context.TraceIdentifier = value; }
    public override ISession Session { get => _context.Session; set => _context.Session = value; }

    public override void Abort() => _context.Abort();
}
