using Microsoft.Extensions.DependencyInjection;

namespace MiniValidation.UnitTests;

public class ExternalValidators
{
    [Fact]
    public void TryValidate_Uses_External_Sync_Validator()
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IValidate<ExternalWidget>, ExternalWidgetNameValidator>()
            .BuildServiceProvider();
        var target = new ExternalWidget { Name = "no" };

        var isValid = MiniValidator.TryValidate(target, serviceProvider, out var errors);

        Assert.False(isValid);
        var entry = Assert.Single(errors);
        Assert.Equal(nameof(ExternalWidget.Name), entry.Key);
    }

    [Fact]
    public async Task TryValidateAsync_Uses_External_Async_Validator()
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IAsyncValidate<ExternalWidget>, ExternalWidgetAsyncNameValidator>()
            .BuildServiceProvider();
        var target = new ExternalWidget { Name = "no" };

        var (isValid, errors) = await MiniValidator.TryValidateAsync(target, serviceProvider);

        Assert.False(isValid);
        var entry = Assert.Single(errors);
        Assert.Equal(nameof(ExternalWidget.Name), entry.Key);
    }

    [Fact]
    public void TryValidate_Aggregates_Multiple_External_Validators()
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IValidate<ExternalWidget>, ExternalWidgetNameValidator>()
            .AddSingleton<IValidate<ExternalWidget>, ExternalWidgetCategoryValidator>()
            .BuildServiceProvider();
        var target = new ExternalWidget { Name = "no", Category = "no" };

        var isValid = MiniValidator.TryValidate(target, serviceProvider, out var errors);

        Assert.False(isValid);
        Assert.Equal(2, errors.Count);
        Assert.Contains(nameof(ExternalWidget.Name), errors.Keys);
        Assert.Contains(nameof(ExternalWidget.Category), errors.Keys);
    }

    [Fact]
    public void TryValidate_Resolves_Single_External_Validator_From_ServiceProvider()
    {
        var validator = new ExternalWidgetNameValidator();
        var serviceProvider = new SingleServiceProvider(typeof(IValidate<ExternalWidget>), validator);
        var target = new ExternalWidget { Name = "no" };

        var isValid = MiniValidator.TryValidate(target, serviceProvider, out var errors);

        Assert.False(isValid);
        Assert.Single(errors);
        Assert.Equal(nameof(ExternalWidget.Name), errors.Keys.First());
    }

    [Fact]
    public void TryValidate_Uses_External_Validator_For_Nested_Object()
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IValidate<SealedExternalChild>, SealedExternalChildValidator>()
            .BuildServiceProvider();
        var target = new ExternalContainer
        {
            Child = new SealedExternalChild { Code = "no" }
        };

        var isValid = MiniValidator.TryValidate(target, serviceProvider, out var errors);

        Assert.False(isValid);
        var entry = Assert.Single(errors);
        Assert.Equal($"{nameof(ExternalContainer.Child)}.{nameof(SealedExternalChild.Code)}", entry.Key);
    }

    [Fact]
    public void TryValidate_Uses_External_Validator_For_Enumerable_Element()
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IValidate<SealedExternalChild>, SealedExternalChildValidator>()
            .BuildServiceProvider();
        var target = new ExternalCollectionContainer
        {
            Children = new List<SealedExternalChild>
            {
                new() { Code = "no" }
            }
        };

        var isValid = MiniValidator.TryValidate(target, serviceProvider, out var errors);

        Assert.False(isValid);
        var entry = Assert.Single(errors);
        Assert.Equal($"{nameof(ExternalCollectionContainer.Children)}.[0].{nameof(SealedExternalChild.Code)}", entry.Key);
    }

    [Fact]
    public void TryValidate_Throws_When_Async_External_Validator_Is_Registered()
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IAsyncValidate<ExternalWidget>, ExternalWidgetAsyncNameValidator>()
            .BuildServiceProvider();
        var target = new ExternalWidget { Name = "no" };

        Assert.Throws<InvalidOperationException>(() => MiniValidator.TryValidate(target, serviceProvider, out _));
    }

    [Fact]
    public void TryValidate_Does_Not_Cache_Transient_External_Validator_Instances()
    {
        TransientExternalValidator.Reset();
        var serviceProvider = new ServiceCollection()
            .AddTransient<IValidate<TransientExternalTarget>, TransientExternalValidator>()
            .BuildServiceProvider();
        var target = new TransientExternalTarget();

        MiniValidator.TryValidate(target, serviceProvider, out var firstErrors);
        MiniValidator.TryValidate(target, serviceProvider, out var secondErrors);

        Assert.Equal("Validator 1", Assert.Single(firstErrors[""]));
        Assert.Equal("Validator 2", Assert.Single(secondErrors[""]));
    }

    [Fact]
    public void TryValidate_Uses_Correct_Interface_For_Multi_Target_Validator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<MultiTargetExternalValidator>();
        services.AddSingleton<IValidate<FirstExternalTarget>>(sp => sp.GetRequiredService<MultiTargetExternalValidator>());
        services.AddSingleton<IValidate<SecondExternalTarget>>(sp => sp.GetRequiredService<MultiTargetExternalValidator>());
        var serviceProvider = services.BuildServiceProvider();

        MiniValidator.TryValidate(new FirstExternalTarget(), serviceProvider, out var firstErrors);
        MiniValidator.TryValidate(new SecondExternalTarget(), serviceProvider, out var secondErrors);

        Assert.Equal(nameof(FirstExternalTarget.FirstName), firstErrors.Keys.Single());
        Assert.Equal(nameof(SecondExternalTarget.SecondName), secondErrors.Keys.Single());
    }

    [Fact]
    public void TryValidate_Uses_Validator_That_Also_Implements_NonGeneric_Interface()
    {
        var services = new ServiceCollection();
        services.AddSingleton<MarkedExternalValidator>();
        services.AddSingleton<INonGenericValidatorMarker>(sp => sp.GetRequiredService<MarkedExternalValidator>());
        services.AddSingleton<IValidate<MarkedExternalTarget>>(sp => sp.GetRequiredService<MarkedExternalValidator>());
        var serviceProvider = services.BuildServiceProvider();

        var isValid = MiniValidator.TryValidate(new MarkedExternalTarget(), serviceProvider, out var errors);

        Assert.False(isValid);
        Assert.Equal(nameof(MarkedExternalTarget.Value), errors.Keys.Single());
    }

    [Fact]
    public void RequiresValidation_With_ServiceProvider_Accounts_For_External_Validators()
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IValidate<ExternalOnlyTarget>, ExternalOnlyValidator>()
            .BuildServiceProvider();

        Assert.False(MiniValidator.RequiresValidation(typeof(ExternalOnlyTarget), recurse: false));
        Assert.True(MiniValidator.RequiresValidation(typeof(ExternalOnlyTarget), serviceProvider, recurse: false));
    }

    private sealed class SingleServiceProvider : IServiceProvider
    {
        private readonly Type _serviceType;
        private readonly object _service;

        public SingleServiceProvider(Type serviceType, object service)
        {
            _serviceType = serviceType;
            _service = service;
        }

        public object? GetService(Type serviceType)
        {
            return serviceType == _serviceType ? _service : null;
        }
    }

    private sealed class ExternalWidget
    {
        public string? Name { get; set; }

        public string? Category { get; set; }
    }

    private sealed class ExternalWidgetNameValidator : IValidate<ExternalWidget>
    {
        public IEnumerable<ValidationResult> Validate(ExternalWidget target, ValidationContext validationContext)
        {
            if (target.Name is null || target.Name.Length < 3)
            {
                yield return new ValidationResult("Name is too short.", new[] { nameof(ExternalWidget.Name) });
            }
        }
    }

    private sealed class ExternalWidgetCategoryValidator : IValidate<ExternalWidget>
    {
        public IEnumerable<ValidationResult> Validate(ExternalWidget target, ValidationContext validationContext)
        {
            if (target.Category is null || target.Category.Length < 3)
            {
                yield return new ValidationResult("Category is too short.", new[] { nameof(ExternalWidget.Category) });
            }
        }
    }

    private sealed class ExternalWidgetAsyncNameValidator : IAsyncValidate<ExternalWidget>
    {
        public async Task<IEnumerable<ValidationResult>> ValidateAsync(ExternalWidget target, ValidationContext validationContext)
        {
            await Task.Yield();

            return target.Name is null || target.Name.Length < 3
                ? new[] { new ValidationResult("Name is too short.", new[] { nameof(ExternalWidget.Name) }) }
                : Enumerable.Empty<ValidationResult>();
        }
    }

    private sealed class ExternalContainer
    {
        public SealedExternalChild? Child { get; set; }
    }

    private sealed class ExternalCollectionContainer
    {
        public IEnumerable<SealedExternalChild>? Children { get; set; }
    }

    private sealed class SealedExternalChild
    {
        public string? Code { get; set; }
    }

    private sealed class SealedExternalChildValidator : IValidate<SealedExternalChild>
    {
        public IEnumerable<ValidationResult> Validate(SealedExternalChild target, ValidationContext validationContext)
        {
            if (target.Code is null || target.Code.Length < 3)
            {
                yield return new ValidationResult("Code is too short.", new[] { nameof(SealedExternalChild.Code) });
            }
        }
    }

    private sealed class TransientExternalTarget
    {
    }

    private sealed class TransientExternalValidator : IValidate<TransientExternalTarget>
    {
        private static int s_instances;
        private readonly int _instance = System.Threading.Interlocked.Increment(ref s_instances);

        public static void Reset()
        {
            s_instances = 0;
        }

        public IEnumerable<ValidationResult> Validate(TransientExternalTarget target, ValidationContext validationContext)
        {
            yield return new ValidationResult($"Validator {_instance}");
        }
    }

    private sealed class FirstExternalTarget
    {
        public string? FirstName { get; set; }
    }

    private sealed class SecondExternalTarget
    {
        public string? SecondName { get; set; }
    }

    private sealed class MultiTargetExternalValidator : IValidate<FirstExternalTarget>, IValidate<SecondExternalTarget>
    {
        IEnumerable<ValidationResult> IValidate<FirstExternalTarget>.Validate(FirstExternalTarget target, ValidationContext validationContext)
        {
            yield return new ValidationResult("First target is invalid.", new[] { nameof(FirstExternalTarget.FirstName) });
        }

        IEnumerable<ValidationResult> IValidate<SecondExternalTarget>.Validate(SecondExternalTarget target, ValidationContext validationContext)
        {
            yield return new ValidationResult("Second target is invalid.", new[] { nameof(SecondExternalTarget.SecondName) });
        }
    }

    private interface INonGenericValidatorMarker
    {
    }

    private sealed class MarkedExternalTarget
    {
        public string? Value { get; set; }
    }

    private sealed class MarkedExternalValidator : INonGenericValidatorMarker, IValidate<MarkedExternalTarget>
    {
        public IEnumerable<ValidationResult> Validate(MarkedExternalTarget target, ValidationContext validationContext)
        {
            yield return new ValidationResult("Value is invalid.", new[] { nameof(MarkedExternalTarget.Value) });
        }
    }

    private sealed class ExternalOnlyTarget
    {
    }

    private sealed class ExternalOnlyValidator : IValidate<ExternalOnlyTarget>
    {
        public IEnumerable<ValidationResult> Validate(ExternalOnlyTarget target, ValidationContext validationContext)
        {
            yield return new ValidationResult("External only target is invalid.");
        }
    }
}
