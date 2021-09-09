namespace MinimalValidationUnitTests;

class TestType
{
    [Required]
    public string? RequiredName { get; set; } = "Default";

    [Range(10, 100)]
    public int TenOrMore { get; set; } = 10;

    [Required]
    public TestChildType Child { get; set; } = new TestChildType();

    public IList<TestChildType> Children { get; } = new List<TestChildType>();
}

class TestChildType
{
    [Required]
    public string? RequiredCategory { get; set; } = "Default";

    [MinLength(5)]
    public string? MinLengthFive { get; set; } = "Default";

    public TestChildType? Child { get; set; }

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

struct TestStruct
{
    [Required]
    public string? RequiredName { get; set; } = "Default";

    [Range(10, 100)]
    public int TenOrMore { get; set; } = 10;
}