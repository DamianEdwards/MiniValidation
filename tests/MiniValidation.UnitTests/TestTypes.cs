using Microsoft.Extensions.DependencyInjection;

namespace MiniValidation.UnitTests;

class TestType
{
    [Required]
    public string? RequiredName { get; set; } = "Default";

    [Required, Display(Name = "Required name")]
    public string? RequiredNameWithDisplay { get; set; } = "Default";

    [Range(10, 100)]
    public int TenOrMore { get; set; } = 10;

    [Required]
    public virtual TestChildType Child { get; set; } = new TestChildType();

    public virtual TestValidatableChildType ValidatableChild { get; set; } = new TestValidatableChildType();

    public virtual TestValidatableOnlyType ValidatableOnlyChild { get; set; } = new TestValidatableOnlyType();

    public virtual object? PocoChild { get; set; } = default;

    public IAnInterface? InterfaceProperty { get; set; }

    [SkipRecursion]
    public TestChildType SkippedChild { get; set; } = new TestChildType();

    public IList<TestChildType> Children { get; } = new List<TestChildType>();
}

class TestValidatableType : TestType, IValidatableObject
{
    public int TwentyOrMore { get; set; } = 20;

    public override TestChildType Child { get; set; } = new TestChildTypeDerivative();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TwentyOrMore < 20)
        {
            yield return new ValidationResult($"The field {validationContext.DisplayName} must have a value greater than 20.", new[] { nameof(TwentyOrMore) });
        }
    }
}

class TestValidatableOnlyType : IValidatableObject
{
    public int TwentyOrMore { get; set; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TwentyOrMore < 20)
        {
            yield return new ValidationResult($"The field {validationContext.DisplayName} must have a value greater than 20.", new[] { nameof(TwentyOrMore) });
        }
    }
}

class TestClassLevelValidatableOnlyType : IValidatableObject
{
    public int TwentyOrMore { get; set; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TwentyOrMore < 20)
        {
            yield return new ValidationResult($"The field {validationContext.DisplayName} must have a value greater than 20.");
        }
    }
}

class TestClassLevelValidatableOnlyTypeWithServiceProvider : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (validationContext.GetService(typeof(TestService)) == null)
        {
            yield return new ValidationResult($"This validationContext did not support ServiceProvider.", new[] { nameof(IServiceProvider) });
        }
    }
}

class TestClassWithEnumerable<TEnumerable>
{
    public IEnumerable<TEnumerable>? Enumerable { get; set; }
}

class TestClassLevelAsyncValidatableOnlyType : IAsyncValidatableObject
{
    public int TwentyOrMore { get; set; } = 20;

    public async Task<IEnumerable<ValidationResult>> ValidateAsync(ValidationContext validationContext)
    {
        await Task.Yield();

        List<ValidationResult>? errors = null;

        if (TwentyOrMore < 20)
        {
            errors ??= new List<ValidationResult>();
            errors.Add(new ValidationResult($"The field {validationContext.DisplayName} must have a value greater than 20."));
        }

        return errors ?? Enumerable.Empty<ValidationResult>();
    }
}

class TestClassLevelAsyncValidatableOnlyTypeWithServiceProvider : IAsyncValidatableObject
{
    public async Task<IEnumerable<ValidationResult>> ValidateAsync(ValidationContext validationContext)
    {
        await Task.Yield();

        List<ValidationResult>? errors = null;

        if (validationContext.GetService(typeof(TestService)) == null)
        {
            errors ??= new List<ValidationResult>();
            errors.Add(new ValidationResult($"This validationContext did not support ServiceProvider.", new[] { nameof(IServiceProvider) }));
        }

        return errors ?? Enumerable.Empty<ValidationResult>();
    }
}

class TestService
{

}

class TestChildType
{
    [Required]
    public virtual string? RequiredCategory { get; set; } = "Default";

    [MinLength(5)]
    public string? MinLengthFive { get; set; } = "Default";

    public TestChildType? Child { get; set; }

    [SkipRecursion]
    public virtual TestChildType? SkippedChild { get; set; }

    internal static void AddDescendents(TestChildType target, int maxDepth, int currentDepth = 1)
    {
        if (currentDepth <= maxDepth)
        {
            target.Child = new();
            if (currentDepth < maxDepth)
            {
                AddDescendents(target.Child, maxDepth, currentDepth + 1);
            }
            else
            {
                target.Child.RequiredCategory = null;
            }
        }
    }
}

class TestValidatableChildType : TestChildType, IValidatableObject
{
    public int TwentyOrMore { get; set; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TwentyOrMore < 20)
        {
            yield return new ValidationResult($"The field {validationContext.DisplayName} must have a value greater than 20.", new[] { nameof(TwentyOrMore) });
        }
    }
}

class TestTypeWithAsyncChild
{
    public TestAsyncValidatableChildType? NeedsAsync { get; set; }
}

class TestAsyncValidatableChildType : TestChildType, IAsyncValidatableObject
{
    public int TwentyOrMore { get; set; } = 20;

    public async Task<IEnumerable<ValidationResult>> ValidateAsync(ValidationContext validationContext)
    {
        var taskToAwait = validationContext.GetService<Task>();
        if (taskToAwait is not null)
        {
            await taskToAwait;
        }

        List<ValidationResult>? result = null;
        if (TwentyOrMore < 20)
        {
            result ??= new();
            result.Add(new ($"The field {validationContext.DisplayName} must have a value greater than 20.", new[] { nameof(TwentyOrMore) }));
        }

        return result ?? Enumerable.Empty<ValidationResult>();
    }
}

class TestChildTypeDerivative : TestChildType
{
    public override string? RequiredCategory { get; set; } = "Derived Default";

    [MinLength(10)]
    public string? DerivedMinLengthTen { get; set; } = "1234567890";
}

class TestSkippedChildType
{
    [Required]
    [SkipRecursion]
    public TestChildType? RequiredSkippedChild { get; set; }
}

struct TestStruct
{
    public TestStruct()
    {

    }

    [Required]
    public string? RequiredName { get; set; } = "Default";

    [Range(10, 100)]
    public int TenOrMore { get; set; } = 10;
}

interface IAnInterface { }

#if NET6_0_OR_GREATER
abstract record BaseRecordType(string Type);

record TestRecordType([Required, Display(Name = "Required name")] string RequiredName = "Default", [Range(10, 100)] int TenOrMore = 10)
    : BaseRecordType(nameof(TestRecordType))
{
#pragma warning disable IDE0060 // Remove unused parameter
    public TestRecordType(string anotherParam, bool doTheThing) : this("Another name", 23)
#pragma warning restore IDE0060 // Remove unused parameter
    {
    }
};
#endif

class ClassWithUri
{
    [Required]
    public Uri? BaseAddress { get; set; }
}

class TestTypeForTypeDescriptor
{
    public string? PropertyToBeRequired { get; set; }

    [MaxLength(1)]
    public string? AnotherProperty { get; set; } = "Test";
}