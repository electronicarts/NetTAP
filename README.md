[![NuGet version](https://badge.fury.io/nu/NETTap.svg)](https://badge.fury.io/nu/NETTap)

# NetTAP

NetTAP is a library for parsing TAP in C#. It is not a harness in the sense that
it makes decisions about tests results and instead leaves that to the user.

It features both synchronous and asynchronous parsing and supports any type of
`Stream` (files, network, etc.).

## Using

To use the parser, create an instance of it and send an instance of a `Stream`
to the `Parse()` method.

```cs
var parser = new TAPParser();
var session = parser.Parse(theStream);
    
var tests = session.Tests.ToList();
```

The asynchronous counterpart would be

```cs
var parser = new TAPParser();
var parseTask = parser.ParseAsync(theStream);
    
// do stuff ...

var session = parseTask.Result;
var tests = session.Tests.ToList();
```

### Event-based Parsing
NETTap also supports emitting events as parsing progresses. To use this,
subscribe to the events you want on the parser instance.

```cs
var parser = new TAPParser();

parser.OnTestResult += line =>
{
    // do stuff with line ...
};

var parseTask = parser.ParseAsync(theStream);
    
// do stuff ...

var session = parseTask.Result;
var tests = session.Tests.ToList();
```

## Contributing

Before you can contribute, EA must have a Contributor License Agreement (CLA) 
on file that has been signed by each contributor.
You can sign here: https://goo.gl/KPylZ3
