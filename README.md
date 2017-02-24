[![NuGet version](https://badge.fury.io/nu/NETTap.svg)](https://badge.fury.io/nu/NETTap)

# NetTAP

NetTAP is a library for parsing the [**T**est **A**nything **P**rotocol (TAP)](https://testanything.org/) in C#. It is not a harness in the sense that it does make decisions about tests results instead of leaving that to the user.

It features both synchronous and asynchronous parsing and supports any type of
`Stream` (files, network, etc.).

## The Protocol
TAP is a fairly simple line based protocol that can additionally be augmented with [YAML](http://www.yaml.org/), as of TAP 13, for additional structured test information.

```txt
TAP version 13
1..8
#
# Create a new Board and Tile, then place
# the Tile onto the board.
#
ok 1 - The object is a Board
ok 2 - Board size is zero
ok 3 - The object is a Tile
ok 4 - Get possible places to put the Tile
ok 5 - Placing the tile produces no error
ok 6 - Board size is 1
not ok 7 - Error descrtion goes here
ok 8 - Something succeeded!
```

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
