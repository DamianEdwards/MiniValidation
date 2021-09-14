namespace MinimalValidationUnitTests;

public class Recursion
{
    [Fact]
    public void Does_Not_Recurse_When_Top_Level_Is_Invalid()
    {
        var thingToValidate = new TestType { RequiredName = null, Child = new TestChildType { RequiredCategory = null, MinLengthFive = "123" } };

        var result = MinimalValidation.TryValidate(thingToValidate, recurse: true, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
        Assert.Collection(errors, entry => Assert.Equal($"{nameof(TestType.RequiredName)}", entry.Key));
    }

    [Fact]
    public void Invalid_When_Child_Invalid_And_Recurse_True()
    {
        var thingToValidate = new TestType { Child = new TestChildType { RequiredCategory = null, MinLengthFive = "123" } };

        var result = MinimalValidation.TryValidate(thingToValidate, recurse: true, out var errors);

        Assert.False(result);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Invalid_When_Child_Invalid_And_Recurse_Default()
    {
        var thingToValidate = new TestType { Child = new TestChildType { RequiredCategory = null } };

        var result = MinimalValidation.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
    }

    [Fact]
    public void Valid_When_Child_Invalid_And_Recurse_False()
    {
        var thingToValidate = new TestType { Child = new TestChildType { RequiredCategory = null, MinLengthFive = "123" } };

        var result = MinimalValidation.TryValidate(thingToValidate, recurse: false, out var errors);

        Assert.True(result);
        Assert.Equal(0, errors.Count);
    }

    [Fact]
    public void Invalid_When_Enumerable_Item_Invalid_When_Recurse_Default()
    {
        var thingToValidate = new List<TestType> { new TestType { Child = new TestChildType { RequiredCategory = null, MinLengthFive = "123" } } };

        var result = MinimalValidation.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Invalid_When_Enumerable_Item_Invalid_When_Recurse_True()
    {
        var thingToValidate = new List<TestType> { new TestType { Child = new TestChildType { RequiredCategory = null, MinLengthFive = "123" } } };

        var result = MinimalValidation.TryValidate(thingToValidate, recurse: true, out var errors);

        Assert.False(result);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Valid_When_Enumerable_Item_Invalid_When_Recurse_False()
    {
        var thingToValidate = new List<TestType> { new TestType { Child = new TestChildType { RequiredCategory = null, MinLengthFive = "123" } } };
        
        var result = MinimalValidation.TryValidate(thingToValidate, recurse: false, out _);

        Assert.True(result);
    }

    [Fact]
    public void Valid_When_Enumerable_Item_Valid_When_Recurse_True()
    {
        var thingToValidate = new List<TestType> { new TestType() };

        var result = MinimalValidation.TryValidate(thingToValidate, recurse: true, out _);

        Assert.True(result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(11)]
    public void Error_Message_Keys_For_Descendants_Are_Formatted_Correctly(int maxDepth)
    {
        var thingToValidate = new TestType { Child = new() };

        TestChildType.AddDescendents(thingToValidate.Child, maxDepth);

        var result = MinimalValidation.TryValidate(thingToValidate, recurse: true, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);

        var messagePrefix = string.Concat(Enumerable.Repeat($"{nameof(TestType.Child)}.", maxDepth + 1));
        Assert.Collection(errors,
            entry => Assert.Equal($"{messagePrefix}{nameof(TestChildType.RequiredCategory)}", entry.Key));
    }

    [Fact]
    public void Error_Message_Keys_For_Root_Enumerable_Are_Formatted_Correctly()
    {
        var thingToValidate = new List<TestType>
        {
            new TestType() ,
            new TestType { RequiredName = null, TenOrMore = 5 },
        };

        var result = MinimalValidation.TryValidate(thingToValidate, recurse: true, out var errors);

        Assert.False(result);
        Assert.Equal(2, errors.Count);
        Assert.Collection(errors,
            entry => Assert.Equal($"[1].{nameof(TestType.RequiredName)}", entry.Key),
            entry => Assert.Equal($"[1].{nameof(TestType.TenOrMore)}", entry.Key));
    }

    [Fact]
    public void Error_Message_Keys_For_Descendant_Enumerable_Are_Formatted_Correctly()
    {
        var thingToValidate = new TestType();
        thingToValidate.Children.Add(new() { });
        thingToValidate.Children.Add(new() { RequiredCategory = null });

        var result = MinimalValidation.TryValidate(thingToValidate, recurse: true, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
        Assert.Collection(errors,
            entry => Assert.Equal($"{nameof(TestType.Children)}[1].{nameof(TestChildType.RequiredCategory)}", entry.Key));
    }

    [Fact]
    public void First_Error_In_Root_Enumerable_Returns_Immediately()
    {
        var thingToValidate = new List<TestType>
        {
            new TestType { RequiredName = null },
            new TestType { RequiredName = null },
        };

        var result = MinimalValidation.TryValidate(thingToValidate, recurse: true, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
        Assert.Collection(errors,
            entry => Assert.Equal($"[0].{nameof(TestType.RequiredName)}", entry.Key));
    }

    [Fact]
    public void First_Error_In_Descendant_Enumerable_Returns_Immediately()
    {
        var thingToValidate = new TestType();
        thingToValidate.Children.Add(new() { MinLengthFive = "123" });
        thingToValidate.Children.Add(new() { RequiredCategory = null });

        var result = MinimalValidation.TryValidate(thingToValidate, recurse: true, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
        Assert.Collection(errors,
            entry => Assert.Equal($"{nameof(TestType.Children)}[0].{nameof(TestChildType.MinLengthFive)}", entry.Key));
    }

    [Fact]
    public void All_Errors_From_Invalid_Item_In_Descendant_Enumerable_Reported()
    {
        var thingToValidate = new TestType();
        thingToValidate.Children.Add(new());
        thingToValidate.Children.Add(new() { RequiredCategory = null, MinLengthFive = "123" });

        var result = MinimalValidation.TryValidate(thingToValidate, recurse: true, out var errors);

        Assert.False(result);
        Assert.Equal(2, errors.Count);
        Assert.Collection(errors,
            entry => Assert.Equal($"{nameof(TestType.Children)}[1].{nameof(TestChildType.RequiredCategory)}", entry.Key),
            entry => Assert.Equal($"{nameof(TestType.Children)}[1].{nameof(TestChildType.MinLengthFive)}", entry.Key));
    }
}