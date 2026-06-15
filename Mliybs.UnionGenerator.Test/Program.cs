using Mliybs.UnionGenerator;

var @int = new IntOrDouble(10);

var @double = new IntOrDouble(3.14);

if (@int is int value1) Console.WriteLine($"This is int {value1}");

if (@double is double value2) Console.WriteLine($"This is double {value2}");



var result = Optional<string>.Of("string");

var none = Optional<string>.Empty;

if (result is string value3) Console.WriteLine($"This is string {value3}");

if (none is None) Console.WriteLine("There is no value");



[Union]
public partial struct IntOrDouble
{
    [UnionTemplate]
    union _(int, double);
}

public record None
{
    public static readonly None Empty = new();
}

[Union]
public partial class Optional<T>
{
    [UnionTemplate]
    union _(T, None);

    public static Optional<T> Of(T value) => new(value);
    public static Optional<T> OfNullable(T value) => value is null ? new(None.Empty) : new(value);
    public static Optional<T> Empty => new(None.Empty);
}