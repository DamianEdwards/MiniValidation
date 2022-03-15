namespace MiniValidation.UnitTests;

class TestType
{
    [Required]
    public string? RequiredName { get; set; } = "Default";

    [Range(10, 100)]
    public int TenOrMore { get; set; } = 10;

    [Required]
    public virtual TestChildType Child { get; set; } = new TestChildType();

    public virtual TestValidatableChildType ValidatableChild { get; set; } = new TestValidatableChildType();

    public virtual TestValidatableOnlyType ValidatableOnlyChild { get; set; } = new TestValidatableOnlyType();

    public virtual object? PocoChild { get; set; } = default;

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