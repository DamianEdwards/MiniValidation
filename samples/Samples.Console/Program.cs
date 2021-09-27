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

class Widget
{
    [Required, MinLength(3)]
    public string Name { get; set; }

    public override string ToString() => Name;
}