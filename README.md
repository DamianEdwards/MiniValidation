# MiniValidation
A minimalistic validation library built atop the existing features in .NET's `System.ComponentModel.DataAnnotations` namespace. Adds support for single-line validation calls and recursion with cycle detection.

Supports .NET Standard 2.0 compliant runtimes.

## Installation
Install the library from [NuGet](https://www.nuget.org/packages/MiniValidation):
``` console
❯ dotnet add package MiniValidation --prerelease
```

### ASP.NET Core 6+ Projects
If installing into an ASP.NET Core 6+ project, consider using the [MinimalApis.Extensions]((https://www.nuget.org/packages/MinimalApis.Extensions)) package instead, which adds extensions specific to ASP.NET Core:
``` console
❯ dotnet add package MinimalApis.Extensions --prerelease
```

## Example usage

### Console app
```csharp
using System;
using System.ComponentModel.DataAnnotations;
using MiniValidation;

var title = args.Length > 0 ? args[0] : "";
var widget = new Widget { Name = title };

if (!MiniValidator.TryValidate(widget, out var errors))
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

class Widget : IValidatableObject
{
    [Required, MinLength(3)]
    public string Name { get; set; }

    public override string ToString() => Name;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(Name, "Widget", StringComparison.OrdinalIgnoreCase))
        {
            yield return new("Cannot name a widget 'Widget'.", new [] { nameof(Name) });
        }
    }
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

❯ widget.exe Widget
Widget has errors!
Name:
  - Cannot name a widget 'Widget'.

❯ widget.exe MiniValidation
Widget 'MiniValidation' is valid!
```

### Web app (.NET 6)
```csharp
using System.ComponentModel.DataAnnotations;
using MiniValidation;

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
    !MiniValidator.TryValidate(widget, out var errors)
        ? Results.ValidationProblem(errors)
        : Results.Created($"/widgets/{widget.Name}", widget));

app.Run();

class Widget : IValidatableObject
{
    [Required, MinLength(3)]
    public string? Name { get; set; }

    public override string? ToString() => Name;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(Name, "Widget", StringComparison.OrdinalIgnoreCase))
        {
            yield return new("Cannot name a widget 'Widget'.");
        }
    }
}
```