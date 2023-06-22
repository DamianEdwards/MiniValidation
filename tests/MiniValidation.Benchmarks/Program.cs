using System.ComponentModel.DataAnnotations;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using MiniValidation;

BenchmarkRunner.Run<Benchmarks>();

#pragma warning disable CA1050 // Declare types in namespaces
//[SimpleJob(RuntimeMoniker.Net472)]
//[SimpleJob(RuntimeMoniker.Net60)]
[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
[MemoryDiagnoser]
public class Benchmarks
{
    [GlobalSetup]
#pragma warning disable CA1822 // Mark members as static
    public void Initialize()
    {
        // Prime the internal type cache of MiniValidator
        var types = typeof(BenchmarkTypes).GetNestedTypes();
        foreach (var type in types)
        {
            var target = Activator.CreateInstance(type);
            MiniValidator.TryValidate(target, recurse: true, allowAsync: false, out var _);
        }
    }

    [Benchmark(Baseline = true)]
    public (bool, IDictionary<string, string[]>) NothingToValidate()
    {
        var target = new BenchmarkTypes.TodoWithNoValidation();
        var isValid = MiniValidator.TryValidate(target, out var errors);
        return (isValid, errors);
    }

    [Benchmark]
    public (bool, IDictionary<string, string[]>) SinglePropertyToValidate_NoRecursion_Valid()
    {
        var target = new BenchmarkTypes.Todo { Title = "This is the title" };
        var isValid = MiniValidator.TryValidate(target, recurse: false, allowAsync: false, out var errors);
        return (isValid, errors);
    }

    [Benchmark]
    public (bool, IDictionary<string, string[]>) SinglePropertyToValidate_NoRecursion_Invalid()
    {
        var target = new BenchmarkTypes.Todo { Title = "" };
        var isValid = MiniValidator.TryValidate(target, recurse: false, allowAsync: false, out var errors);
        return (isValid, errors);
    }

    [Benchmark]
    public (bool, IDictionary<string, string[]>) OneLevelHierarchy_Valid()
    {
        var target = new BenchmarkTypes.Todo { Title = "This is the title" };
        target.Tags.Add(new() { Name = "A tag" });
        var isValid = MiniValidator.TryValidate(target, out var errors);
        return (isValid, errors);
    }

    [Benchmark]
    public (bool, IDictionary<string, string[]>) OneLevelHierarchy_Invalid()
    {
        var target = new BenchmarkTypes.Todo { Title = "This is the title" };
        target.Tags.Add(new() { Name = "" });
        var isValid = MiniValidator.TryValidate(target, out var errors);
        return (isValid, errors);
    }
#pragma warning restore CA1822 // Mark members as static
}

public class BenchmarkTypes
{
    public class TodoWithNoValidation
    {
        public int Id { get; set; }

        public string Title { get; set; } = default!;

        public bool IsComplete { get; set; }
    }

    public class Todo
    {
        private List<Tag>? _tags;

        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = default!;

        public bool IsComplete { get; set; }

        public IList<Tag> Tags
        {
            get => _tags ??= new List<Tag>();
        }
    }

    public class Tag
    {
        [Required]
        public string Name { get; set; } = default!;
    }
}
#pragma warning restore CA1050 // Declare types in namespaces
