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
}