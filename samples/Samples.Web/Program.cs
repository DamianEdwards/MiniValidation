using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World");

app.MapGet("/widgets", () =>
    new[] {
        new Widget { Name = "Shinerizer" },
        new Widget { Name = "Sparklizer" }
    });

app.MapGet("/widgets/{name}", (string name) =>
    new Widget { Name = name });

app.MapPost("/widgets", (Widget widget) =>
    !MiniValidation.TryValidate(widget, out var errors)
        ? Results.BadRequest(errors)
        : Results.Created($"/widgets/{widget.Name}", widget));

app.MapPost("/widgets-validated", (Validated<Widget> input) =>
{
    var (widget, isValid, errors) = input;
    return !isValid
        ? Results.BadRequest(errors)
        : Results.Created($"/widgets/{widget.Name}", widget);
});

app.Run();

class Widget
{
    [Required, MinLength(3)]
    public string? Name { get; set; }

    public override string? ToString() => Name;
}
