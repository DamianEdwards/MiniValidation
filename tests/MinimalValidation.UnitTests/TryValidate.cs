namespace MinimalValidationUnitTests;

public class TryValidate
{
#nullable disable
    [Fact]
    public void Throws_ANE_For_Null_Target()
    {
        TestType thingToValidate = null;

        Assert.Throws<ArgumentNullException>(() =>
            MinimalValidation.TryValidate(thingToValidate, out var errors));
    }
#nullable enable

    [Fact]
    public void RequiredValidator_Invalid_When_Null()
    {
        var thingToValidate = new TestType { RequiredName = null };

        var result = MinimalValidation.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Collection(errors, entry => Assert.Equal(nameof(TestType.RequiredName), entry.Key));
    }

    [Fact]
    public void RequiredValidator_Invalid_When_Empty()
    {
        var thingToValidate = new TestType { RequiredName = string.Empty };

        var result = MinimalValidation.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Collection(errors, entry => Assert.Equal(nameof(TestType.RequiredName), entry.Key));
    }

    [Fact]
    public void RequiredValidator_Valid_When_NonEmpty_Value()
    {
        var thingToValidate = new TestType { RequiredName = "test" };

        var result = MinimalValidation.TryValidate(thingToValidate, out var errors);

        Assert.True(result);
        Assert.Equal(0, errors.Count);
    }

    [Fact]
    public void NonRequiredValidator_Invalid_When_Invalid()
    {
        var thingToValidate = new TestType { TenOrMore = 5 };

        var result = MinimalValidation.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Collection(errors, entry => Assert.Equal(nameof(TestType.TenOrMore), entry.Key));
    }

    [Fact]
    public void NonRequiredValidator_Valid_When_Valid()
    {
        var thingToValidate = new TestType { TenOrMore = 11 };

        var result = MinimalValidation.TryValidate(thingToValidate, out var errors);

        Assert.True(result);
        Assert.Equal(0, errors.Count);
    }

    [Fact]
    public void MultipleValidators_Valid_When_All_Valid()
    {
        var thingToValidate = new TestType { RequiredName = "test", TenOrMore = 11 };

        var result = MinimalValidation.TryValidate(thingToValidate, out var errors);

        Assert.True(result);
        Assert.Equal(0, errors.Count);
    }

    [Fact]
    public void MultipleValidators_Invalid_When_First_Invalid()
    {
        var thingToValidate = new TestType { RequiredName = null, TenOrMore = 11 };

        var result = MinimalValidation.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
    }

    [Fact]
    public void MultipleValidators_Invalid_When_One_Other_Than_First_Invalid()
    {
        var thingToValidate = new TestType { RequiredName = "test", TenOrMore = 5 };

        var result = MinimalValidation.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
    }

    [Fact]
    public void MultipleValidators_Invalid_When_All_Invalid()
    {
        var thingToValidate = new TestType { RequiredName = null, TenOrMore = 5 };

        var result = MinimalValidation.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void List_Invalid_When_Entry_Invalid()
    {
        var collectionToValidate = new List<TestType> { new TestType { RequiredName = null } };

        var result = MinimalValidation.TryValidate(collectionToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
    }

    public static IEnumerable<object?[]> PrimitiveValues
        => new object?[][] {
        new object[] { "A string" },
        new object[] { 'c' },
        new object[] { 100 },
        new object[] { 100.2 },
        new object[] { 100.2m },
        new object[] { (long)100 },
        new object[] { true },
        new object[] { new DateTime(2021, 01, 01) },
        new object[] { new DateTimeOffset(2021, 01, 01, 0, 0, 0, TimeSpan.FromHours(1)) },
        new object[] { StringComparison.OrdinalIgnoreCase },
        new object?[] { new int?(1) },
    };

    [Theory]
    [MemberData(nameof(PrimitiveValues))]
    public void Valid_When_Target_Is_Not_Complex(object thingToValidate)
    {
        var result = MinimalValidation.TryValidate(thingToValidate, out var errors);

        Assert.True(result);
        Assert.Equal(0, errors.Count);
    }

    [Fact]
    public void Struct_Valid_When_Valid()
    {
        var thingToValidate = new TestStruct();

        var result = MinimalValidation.TryValidate(thingToValidate, out var errors);

        Assert.True(result);
        Assert.Equal(0, errors.Count);
    }

    [Fact]
    public void Struct_Invalid_When_Invalid()
    {
        var thingToValidate = new TestStruct { RequiredName = null };

        var result = MinimalValidation.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
    }
}