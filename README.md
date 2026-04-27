# FSharpEventAddons

A functional programming-inspired event scheduling library for F# with priority, delayed, and periodic event schedulers. (current version 1.0.0; already stable)

If you're using F# event models for the first time, or want to refresh your knowledge, please see the [Beginner's Guide](#f-event-models-a-beginners-guide) section.

## Requirements
- [.NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
- F# projects or F# scripts

## Features

- **`PriorityEventScheduler`**: Execute events based on priority (higher number = higher priority)
- **`DelayedEventScheduler`**: Schedule events to execute after specified delays
- **`PeriodicEventScheduler`**: Execute events at regular intervals with unique identifier management
- **Thread-safe**: All operations are thread-safe using proper locking
- **Functional API design**: Provides functional programming patterns while using mutable state internally for performance
- **Honest documentation**: Clear about implementation trade-offs between functional purity and practical performance

## Installation

Install the package using NuGet:
```powershell
Install-Package FSharpEventAddons
```

Or via .NET CLI:
```bash
dotnet add package FSharpEventAddons
```

If you're using F# scripts, you can reference the library directly:
```fsharp
#r "nuget: FSharpEventAddons"
```

## Quick Start

Import this library to your F# project:
```fsharp
open FSharpEventAddons
```

### Priority-based Event Scheduling
```fsharp
let scheduler = PriorityEventScheduler()
scheduler.Schedule((fun () -> printfn "High priority event"), Some 10)
scheduler.Schedule((fun () -> printfn "Low priority event"), Some 1)
scheduler.Schedule((fun () -> printfn "Default priority event"), None)
printfn "Priority scheduler event count: %d" (scheduler.EventCount) // 3 events
scheduler.Execute() // Executes in priority order
```

### Delayed Event Scheduling
```fsharp
let scheduler = DelayedEventScheduler()
scheduler.ScheduleDelayed((fun () -> printfn "Delayed event"), 1.0) // 1 second delay
// Call ExecuteExpired() periodically to check for expired events
```

### Periodic Event Scheduling
```fsharp
let scheduler = PeriodicEventScheduler()
let eventId = scheduler.SchedulePeriodic((fun () -> printfn "Periodic event"), 500) // Every 500ms
// Call ExecuteDue() periodically to execute due events
```

## API Reference

### `PriorityEventScheduler`

- `Schedule(action: unit -> unit, priority: int) : unit` - Schedule an event with priority
- `Execute() : unit` - Execute all pending events in priority order
- `EventCount: int` - Get current count of pending events
- `Clear() : unit` - Clear all pending events

### `DelayedEventScheduler`

- `ScheduleDelayed(action: unit -> unit, delaySec: float) : unit` - Schedule event with delay in seconds (must be greater than 0)
- `ExecuteExpired() : unit` - Execute events whose delay has expired
- `EventCount: int` - Get current count of delayed events
- `SecondsUntilNextEvent: float option` - Time until next scheduled event in seconds
- `Clear() : unit` - Clear all delayed events

### `PeriodicEventScheduler`

- `SchedulePeriodic(action: unit -> unit, intervalSec: float) : Guid` - Schedule periodic event with seconds interval (must be greater than 0), and returns a unique identifier for the event
- `ExecuteDue() : unit` - Execute due events whose interval has elapsed
- `EventCount: int` - Get current count of periodic events
- `RemovePeriodic(eventId: Guid) : unit` - Remove specific periodic event
- `Clear() : unit` - Clear all periodic events

## F# Event Models: A Beginner's Guide

### Understanding F# Event Handling

F# provides several built-in event handling mechanisms, but they differ from traditional object-oriented event systems:

#### 1. F# Event Type
```fsharp
let event = Event<int>()
let observable = event.Publish

observable.Add(fun x -> printfn "Event received: %d" x)
event.Trigger(42)
```

#### 2. Observable Pattern
```fsharp
let numbers = [1..10]
numbers 
|> List.toSeq 
|> Observable.ofSeq
|> Observable.filter (fun x -> x % 2 = 0)
|> Observable.add (printfn "Even number: %d")
```

#### 3. MailboxProcessor (Actor Model)
```fsharp
type Message = 
    | Schedule of (unit -> unit) * int
    | Execute

let scheduler = MailboxProcessor.Start(fun inbox ->
    let rec loop events = async {
        let! msg = inbox.Receive()
        match msg with
        | Schedule(action, priority) -> 
            return! loop ((action, priority) :: events)
        | Execute ->
            events 
            |> List.sortBy snd 
            |> List.iter (fst >> Async.RunSynchronously)
            return! loop []
    }
    loop [])
```

### Why Use FSharpEventAddons?

While F# has built-in event handling, FSharpEventAddons provides:

1. **Priority-based execution** - Built-in priority queues
2. **Time-based scheduling** - Precise delayed and periodic execution
3. **Functional composition** - Designed with functional programming principles
4. **Thread safety** - Safe for concurrent usage
5. **Simple API** - Easy to understand and use

### Comparison with Other Approaches

| Feature | F# Events | MailboxProcessor | FSharpEventAddons |
|---------|-----------|------------------|-------------------|
| Priority scheduling | ❌ | ⚠️ (Manual) | ✅ |
| Delayed execution | ❌ | ⚠️ (Complex) | ✅ |
| Periodic execution | ❌ | ⚠️ (Complex) | ✅ |
| Thread safety | ✅ | ✅ | ✅ |
| Functional design | ✅ | ✅ | ✅ |

## Advanced Usage

### Combining Schedulers

```fsharp
let priorityScheduler = PriorityEventScheduler()
let delayedScheduler = DelayedEventScheduler()

// Schedule a delayed event that adds a priority event
delayedScheduler.ScheduleDelayed(
    (fun () -> 
        priorityScheduler.Schedule(
            (fun () -> printfn "Delayed priority event"), 
            Some 1)), 
    2.0)
```

### Error Handling

```fsharp
let safeScheduler = PriorityEventScheduler()

safeScheduler.Schedule(
    (fun () -> 
        try
            // Your event logic here
            printfn "Event executed successfully"
        with
        | ex -> printfn "Event failed: %s" ex.Message),
    None)
```

### Implementation Trade-offs

**Honest Assessment of Functional Purity vs. Practical Performance**

This library makes a deliberate trade-off: it provides a functional API while using mutable state internally for performance reasons. Here's why:

#### Why Mutable State?
- **Performance**: Pure functional implementations with immutable state would require copying the entire event list on every operation
- **Thread Safety**: The mutable state is properly locked, ensuring thread safety without sacrificing performance
- **Practicality**: For event scheduling systems that handle many events, immutable state would be prohibitively expensive

#### Functional Patterns Used
Despite the mutable implementation, the library embraces functional programming principles:
- **Immutable Data Structures**: Event items are F# records (immutable by default)
- **Pure Functions**: Operations like sorting and filtering use pure functional patterns
- **Functional Composition**: API designed for functional composition and pipelining

### When to Use This Library vs. Pure Functional Alternatives

**Use FSharpEventAddons when:**
- You need high-performance event scheduling
- You're dealing with many events (100+)
- Thread safety is a requirement
- You want a simple, practical API

**Consider pure functional alternatives when:**
- You're dealing with very few events (< 10)
- Functional purity is more important than performance
- You're building a learning/educational project

### Performance Considerations

- **PriorityEventScheduler**: O(n log n) for execution due to sorting
- **DelayedEventScheduler**: O(n) for execution, efficient for small numbers of events
- **PeriodicEventScheduler**: O(n) for execution, efficient interval checking

For high-performance scenarios, consider batching events or using specialized data structures.

## Building from Source

```bash
git clone https://github.com/Pac-Dessert1436/FSharpEventAddons.git
cd FSharpEventAddons
dotnet build
dotnet test  # If tests are added later
```

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.

## License

This project is licensed under the BSD 3-Clause License. See the [LICENSE](LICENSE) file for details.

## Version History

- **1.0.0**: Stable release with priority, delayed, and periodic event schedulers