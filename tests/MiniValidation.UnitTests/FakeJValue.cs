namespace Newtonsoft.Json.Linq;

public sealed class FakeJValue
{
    public object First => throw new InvalidOperationException("Cannot access child value on Newtonsoft.Json.Linq.JValue.");
}
