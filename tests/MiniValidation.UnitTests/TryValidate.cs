using Microsoft.Extensions.DependencyInjection;

namespace MiniValidation.UnitTests;

public class TryValidate
{
#nullable disable
    [Fact]
    public void Throws_ANE_For_Null_Target()
    {
        TestType thingToValidate = null;

        Assert.Throws<ArgumentNullException>(() =>
            MiniValidator.TryValidate(thingToValidate, out var errors));
    }
#nullable enable

    [Fact]
    public void RequiredValidator_Invalid_When_Null()
    {
        var thingToValidate = new TestType { RequiredName = null };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Collection(errors, entry => Assert.Equal(nameof(TestType.RequiredName), entry.Key));
    }

    [Fact]
    public void RequiredValidator_Invalid_When_Empty()
    {
        var thingToValidate = new TestType { RequiredName = string.Empty };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Collection(errors, entry => Assert.Equal(nameof(TestType.RequiredName), entry.Key));
    }

    [Fact]
    public void RequiredValidator_Valid_When_NonEmpty_Value()
    {
        var thingToValidate = new TestType { RequiredName = "test" };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.True(result);
        Assert.Equal(0, errors.Count);
    }

    [Fact]
    public void NonRequiredValidator_Invalid_When_Invalid()
    {
        var thingToValidate = new TestType { TenOrMore = 5 };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Collection(errors, entry => Assert.Equal(nameof(TestType.TenOrMore), entry.Key));
    }

    [Fact]
    public void NonRequiredValidator_Valid_When_Valid()
    {
        var thingToValidate = new TestType { TenOrMore = 11 };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.True(result);
        Assert.Equal(0, errors.Count);
    }

#if NET6_0_OR_GREATER
    [Fact]
    public void NonRequiredValidator_Valid_When_Valid_On_Record()
    {
        var thingToValidate = new TestRecordType(TenOrMore: 11);

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.True(result);
        Assert.Equal(0, errors.Count);
    }

    [Fact]
    public void NonRequiredValidator_Invalid_When_Invalid_On_Record()
    {
        var thingToValidate = new TestRecordType(TenOrMore: 9);

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
    }
#endif

    [Fact]
    public void MultipleValidators_Valid_When_All_Valid()
    {
        var thingToValidate = new TestType { RequiredName = "test", TenOrMore = 11 };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.True(result);
        Assert.Equal(0, errors.Count);
    }

    [Fact]
    public void MultipleValidators_Invalid_When_First_Invalid()
    {
        var thingToValidate = new TestType { RequiredName = null, TenOrMore = 11 };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
    }

    [Fact]
    public void MultipleValidators_Invalid_When_One_Other_Than_First_Invalid()
    {
        var thingToValidate = new TestType { RequiredName = "test", TenOrMore = 5 };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
    }

    [Fact]
    public void MultipleValidators_Invalid_When_All_Invalid()
    {
        var thingToValidate = new TestType { RequiredName = null, TenOrMore = 5 };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Validator_DisplayAttribute_Name_Used_In_Error_Message()
    {
        var thingToValidate = new TestType { RequiredNameWithDisplay = null };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Collection(errors,
            entry => Assert.Collection(entry.Value,
                error => Assert.Contains("Required name", error)
            )
        );
    }

#if NET6_0_OR_GREATER
    [Fact]
    public void Validator_DisplayAttribute_Name_Used_In_Error_Message_For_Record()
    {
        var thingToValidate = new TestRecordType("");

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Collection(errors,
            entry => Assert.Collection(entry.Value,
                error => Assert.Contains("Required name", error)
            )
        );
    }
#endif

    [Fact]
    public void List_Invalid_When_Entry_Invalid()
    {
        var collectionToValidate = new List<TestType> { new TestType { RequiredName = null } };

        var result = MiniValidator.TryValidate(collectionToValidate, out var errors);

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
#if NET6_0_OR_GREATER
        new object[] { new DateOnly(2021, 01, 01) },
        new object[] { new TimeOnly(0, 0) },
#endif
        new object[] { StringComparison.OrdinalIgnoreCase },
        new object?[] { new int?(1) },
    };

    [Theory]
    [MemberData(nameof(PrimitiveValues))]
    public void Valid_When_Target_Is_Not_Complex(object thingToValidate)
    {
        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.True(result);
        Assert.Equal(0, errors.Count);
    }

    [Fact]
    public void Struct_Valid_When_Valid()
    {
        var thingToValidate = new TestStruct();

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.True(result);
        Assert.Equal(0, errors.Count);
    }

    [Fact]
    public void Struct_Invalid_When_Invalid()
    {
        var thingToValidate = new TestStruct { RequiredName = null };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
    }

    [Fact]
    public void Invalid_When_ValidatableObject_Validate_Is_Invalid()
    {
        var thingToValidate = new TestValidatableType
        {
            TwentyOrMore = 12
        };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
        Assert.Equal(nameof(TestValidatableType.TwentyOrMore), errors.Keys.First());
    }

    [Fact]
    public void Invalid_When_ValidatableObject_Has_Invalid_Attributes()
    {
        var thingToValidate = new TestValidatableType
        {
            TenOrMore = 9
        };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
        Assert.Equal(nameof(TestValidatableType.TenOrMore), errors.Keys.First());
    }

    [Fact]
    public void ValidatableObject_Is_Not_Validated_When_Has_Invalid_Attributes()
    {
        var thingToValidate = new TestValidatableType
        {
            TenOrMore = 9,
            TwentyOrMore = 12
        };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
        Assert.Equal(nameof(TestValidatableType.TenOrMore), errors.Keys.First());
    }

    [Fact]
    public void Invalid_When_ValidatableObject_With_Class_Level_Only_Is_Invalid()
    {
        var thingToValidate = new TestClassLevelValidatableOnlyType
        {
            TwentyOrMore = 12
        };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
        Assert.Equal("", errors.Keys.First());
    }

    [Fact]
    public async Task Invalid_When_AsyncValidatableObject_With_Class_Level_Only_Is_Invalid()
    {
        var thingToValidate = new TestClassLevelAsyncValidatableOnlyType
        {
            TwentyOrMore = 12
        };

        var (isValid, errors) = await MiniValidator.TryValidateAsync(thingToValidate, true);

        Assert.False(isValid);
        Assert.Equal(1, errors.Count);
        Assert.Equal("", errors.Keys.First());
    }

    [Fact]
    public void Throws_ArgumentException_When_TryValidate_Called_On_Target_Requiring_Async()
    {
        var thingToValidate = new TestClassLevelAsyncValidatableOnlyType
        {
            TwentyOrMore = 12
        };

        Assert.Throws<ArgumentException>("target", () =>
        {
            var isValid = MiniValidator.TryValidate(thingToValidate, out var errors);
        });
    }

    [Fact]
    public void Valid_When_Target_Has_Required_Uri_Property()
    {
        var thingToValidate = new ClassWithUri { BaseAddress = new("https://example.com") };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.True(result);
        Assert.Equal(0, errors.Count);
    }

    [Fact]
    public void Valid_When_Target_Has_Required_Uri_Property_With_UriKind_Relative_Value()
    {
        var thingToValidate = new ClassWithUri { BaseAddress = new("/a/relative/path", UriKind.Relative) };

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.True(result);
        Assert.Equal(0, errors.Count);
    }

    [Fact]
    public void Invalid_When_Target_Has_Required_Uri_Property_With_Null_Value()
    {
        var thingToValidate = new ClassWithUri();

        var result = MiniValidator.TryValidate(thingToValidate, out var errors);

        Assert.False(result);
        Assert.Equal(1, errors.Count);
        Assert.Equal(nameof(ClassWithUri.BaseAddress), errors.Keys.First());
    }

    [Fact]
    public void TryValidate_With_ServiceProvider()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TestService>();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var thingToValidate = new TestClassLevelValidatableOnlyTypeWithServiceProvider();

        var result = MiniValidator.TryValidate(thingToValidate, serviceProvider, out var errors);
        Assert.True(result);

        errors.Clear();
        result = MiniValidator.TryValidate(thingToValidate, out errors);
        Assert.False(result);
        Assert.Equal(1, errors.Count);
        Assert.Equal(nameof(IServiceProvider), errors.Keys.First());
    }

    [Fact]
    public async Task TryValidateAsync_With_ServiceProvider()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<TestService>();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var thingToValidate = new TestClassLevelValidatableOnlyTypeWithServiceProvider();

        var (isValid, errors) = await MiniValidator.TryValidateAsync(thingToValidate, serviceProvider);

        Assert.True(isValid);
        Assert.Equal(0, errors.Count);

        errors.Clear();
        (isValid, errors) = await MiniValidator.TryValidateAsync(thingToValidate);
        Assert.False(isValid);
        Assert.Equal(1, errors.Count);
        Assert.Equal(nameof(IServiceProvider), errors.Keys.First());
    }

    [Fact]
    public async Task TryValidateAsync_With_Attribute_Attached_Via_TypeDescriptor()
    {
        var thingToValidate = new TestTypeForTypeDescriptor();

        typeof(TestTypeForTypeDescriptor).AttachAttribute(
            nameof(TestTypeForTypeDescriptor.PropertyToBeRequired), 
            _ => new RequiredAttribute());

        var (isValid, errors) = await MiniValidator.TryValidateAsync(thingToValidate);

        Assert.False(isValid);
        Assert.Equal(2, errors.Count);

        Assert.Single(errors["PropertyToBeRequired"]);
        Assert.Single(errors["AnotherProperty"]);
    }
}
