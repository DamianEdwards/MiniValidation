# Minimal Validation
A minimal validation library built atop the existing features in .NET's `System.ComponentModel.DataAnnotations` namespace. Adds support for single-line validation calls and recursion with cycle detection.

Supports .NET Standard 2.0 compliant runtimes.

## Installation
Install the library from [NuGet](https://www.nuget.org/packages/MinimalValidation):
``` console
❯ dotnet add package MinimalValidation --prerelease
```

## Example usage

### Console app
```csharp
using System;
using System.ComponentModel.DataAnnotations;

var title = args.Length > 0 ? args[0] : "";
var widget = new Widget { Name = title };

if (!MinimalValidation.TryValidate(widget, out var errors))
{
    Console.WriteLine($"{nameof(Widget)} has errors!");
    foreach (var entry in errors)
    {
        Console.WriteLine($"  {entry.Key}:");
        foreach (var error in entry.Value)
        {
            Console.WriteLine($"  - {error}");
        }
    }
}
else
{
    Console.WriteLine($"{nameof(Widget)} '{widget}' is valid!");
}

class Widget
{
    [Required, MinLength(3)]
    public string Name { get; set; }

    public override string ToString() => Name;
}
```
``` console
❯ widget.exe
Widget has errors!
Name:
  - The Name field is required.

❯ widget.exe Ok
Widget has errors!
  Name:
  - The field Name must be a string or array type with a minimum length of '3'.

❯ widget.exe MinimalValidation
Widget 'MinimalValidation' is valid!
```

### Web app (.NET 6)
```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapGet("/", () => "Hello World");

app.MapGet("/widgets", () =>
    new[] {
        new Widget { Name = "Shinerizer" },
        new Widget { Name = "Sparklizer" }
    });

app.MapGet("/widgets/{name}", (string name) =>
    new Widget { Name = name });

app.MapPost("/widgets", (Widget widget) =>
    !MinimalValidation.TryValidate(widget, out var errors)
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
```