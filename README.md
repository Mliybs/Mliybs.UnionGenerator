# Mliybs.UnionGenerator
An incremental generator that enhances default union in .NET

# Example

```C#
using Mliybs.UnionGenerator;

[Union]
public partial struct MyUnion
{
    [UnionTemplate]
    union _(int, double);
}
```

```C#
var myUnion = new MyUnion(10.2);

if (myUnion is double value) Console.WriteLine(value); // Output: 10.2
```

# Features

- Non-boxing bahavior
- IDE supported
- Generic supported

# Usage

Add package reference from nuget:

> dotnet add package Mliybs.UnionGenerator

Add `using Mliybs.UnionGenerator;` on the top;

Add `[Union]` on the custom union type;

Declare a union with `[UnionTemplate]` attribute as template in the custom union type;

Done.