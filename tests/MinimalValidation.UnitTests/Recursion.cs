using System.ComponentModel.DataAnnotations;
using Xunit;

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
    public void Error_Message_Keys_For_Descendants_Are_Formatted_Correctly()
    {
        var thingToValidate = new TestType { Child = new TestChildType { RequiredCategory = null } };

        var result = MinimalValidation.TryValidate(thingToValidate, recurse: true, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
        Assert.Collection(errors, entry => Assert.Equal($"{nameof(TestType.Child)}.{nameof(TestChildType.RequiredCategory)}", entry.Key));
    }

    [Fact]
    public void Error_Message_Keys_For_Descendant_Collections_Are_Formatted_Correctly()
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
    public void First_Error_In_Descendant_Collection_Returns_Immediately()
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
    public void All_Errors_From_Invalid_Item_In_Descendant_Collection_Reported()
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