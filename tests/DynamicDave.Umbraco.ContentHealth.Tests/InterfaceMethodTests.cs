using DynamicDave.Umbraco.ContentHealth.Status;

namespace DynamicDave.Umbraco.ContentHealth.Tests;

public class InterfaceMethodTests
{
    private interface IBase
    {
        bool M(Guid id);

        bool Two(Guid id, string culture);
    }

    // Mirrors Umbraco 18: the members live on the base interface, the derived one declares nothing.
    private interface IDerived : IBase;

    // Mirrors Umbraco 17: the members are declared directly on the interface.
    private interface IDirect
    {
        bool M(Guid id);
    }

    private sealed class Implementation(bool result) : IDerived, IDirect
    {
        public bool M(Guid id) => result && id != Guid.Empty;

        public bool Two(Guid id, string culture) => result && culture == "nl";
    }

    [Fact]
    public void Finds_method_declared_on_a_base_interface()
    {
        var bound = InterfaceMethod.Bind<Func<Guid, bool>>(new Implementation(true), typeof(IDerived), "M");

        Assert.True(bound(Guid.NewGuid()));
        Assert.False(bound(Guid.Empty));
    }

    [Fact]
    public void Finds_method_declared_directly_on_the_interface()
    {
        var bound = InterfaceMethod.Bind<Func<Guid, bool>>(new Implementation(true), typeof(IDirect), "M");

        Assert.True(bound(Guid.NewGuid()));
    }

    [Fact]
    public void Invokes_the_implementation_of_the_target()
    {
        var bound = InterfaceMethod.Bind<Func<Guid, bool>>(new Implementation(false), typeof(IDerived), "M");

        Assert.False(bound(Guid.NewGuid()));
    }

    [Fact]
    public void Matches_on_parameter_types()
    {
        var bound = InterfaceMethod.Bind<Func<Guid, string, bool>>(new Implementation(true), typeof(IDerived), "Two");

        Assert.True(bound(Guid.NewGuid(), "nl"));
        Assert.False(bound(Guid.NewGuid(), "en"));
    }

    [Fact]
    public void Throws_when_no_method_has_the_delegate_signature()
        => Assert.Throws<MissingMethodException>(() => InterfaceMethod.Bind<Func<Guid, string, bool>>(new Implementation(true), typeof(IDerived), "M"));

    [Fact]
    public void Throws_for_an_unknown_name()
        => Assert.Throws<MissingMethodException>(() => InterfaceMethod.Bind<Func<Guid, bool>>(new Implementation(true), typeof(IDerived), "Nope"));
}
