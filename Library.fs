namespace FSharpEventAddons

open System

/// Represents a scheduled event with an action and priority
type internal PriorityEventItem =
    { EventAction: unit -> unit
      Priority: int }

/// Represents a delayed event with an action and delay time in seconds
type internal DelayedEventItem =
    { EventAction: unit -> unit
      DelaySec: float
      ScheduledTime: DateTime }

/// Represents a periodic event with an action, interval, and unique identifier
type internal PeriodicEventItem =
    { EventAction: unit -> unit
      IntervalSec: float
      LastExecutionTime: DateTime option
      Id: Guid }

/// Functional event scheduler that processes events based on priority.
/// NOTE: Uses mutable state internally for thread safety, but provides immutable API patterns.
type public PriorityEventScheduler() =
    let mutable state: PriorityEventItem list = []
    let lockObj: obj = obj ()

    /// Schedule an event with a given priority (higher numbers = higher priority).
    member _.Schedule(action: unit -> unit, priority: int option) : unit =
        let priority: int = priority |> Option.defaultValue 0

        lock lockObj (fun () ->
            state <-
                { EventAction = action
                  Priority = priority }
                :: state)

    /// Execute all pending events in priority order.
    member _.Execute() : unit =
        let sortedEvents: PriorityEventItem list =
            lock lockObj (fun () ->
                let result: PriorityEventItem list =
                    state |> List.sortByDescending (fun (evt: PriorityEventItem) -> evt.Priority)

                state <- []
                result)

        sortedEvents |> List.iter (fun (evt: PriorityEventItem) -> evt.EventAction())

    /// Get the current count of pending events.
    member _.EventCount: int = lock lockObj (fun () -> List.length state)

    /// Clear all pending events.
    member _.Clear() : unit = lock lockObj (fun () -> state <- [])

/// Functional delayed event scheduler that executes events after specified delays.
/// NOTE: Uses mutable state internally for thread safety, but provides functional API patterns.
type public DelayedEventScheduler() =
    let mutable state: DelayedEventItem list = []
    let lockObj: obj = obj ()

    /// Schedule an event to execute after a specified delay in seconds.
    member _.ScheduleDelayed(action: unit -> unit, delaySec: float) : unit =
        if delaySec <= 0.0 then
            invalidArg (nameof delaySec) "Delay seconds must be greater than 0"

        lock lockObj (fun () ->
            state <-
                { EventAction = action
                  DelaySec = delaySec
                  ScheduledTime = DateTime.Now.AddSeconds delaySec }
                :: state)

    /// Execute events whose delay has expired.
    member _.ExecuteExpired() : unit =
        let now: DateTime = DateTime.Now

        let expiredEvents: DelayedEventItem list =
            lock lockObj (fun () ->
                let (expired: DelayedEventItem list), (remaining: DelayedEventItem list) =
                    state
                    |> List.partition (fun (evt: DelayedEventItem) -> evt.ScheduledTime <= now)

                state <- remaining
                expired)

        expiredEvents |> List.iter (fun (evt: DelayedEventItem) -> evt.EventAction())

    /// Get the current count of delayed events.
    member _.EventCount: int = lock lockObj (fun () -> List.length state)

    /// Clear all delayed events.
    member _.Clear() : unit = lock lockObj (fun () -> state <- [])

    /// Get the time left until the next scheduled event (in seconds).
    member _.SecondsUntilNextEvent: float option =
        lock lockObj (fun () ->
            match state with
            | [] -> None
            | (events: DelayedEventItem list) ->
                let nextTime: DateTime =
                    events
                    |> List.map (fun (evt: DelayedEventItem) -> evt.ScheduledTime)
                    |> List.min

                let secUntilNext: float = (nextTime - DateTime.Now).TotalSeconds
                Some(max 0 secUntilNext))

/// Functional periodic event scheduler that executes events at regular intervals.
/// NOTE: Uses mutable state internally for thread safety, but provides functional API patterns.
type public PeriodicEventScheduler() =
    let mutable state: PeriodicEventItem list = []
    let lockObj: obj = obj ()

    /// Schedule an event to execute periodically with the specified interval in seconds.
    /// Returns a unique identifier that can be used to remove the event later.
    member _.SchedulePeriodic(action: unit -> unit, intervalSec: float) : Guid =
        if intervalSec <= 0.0 then
            invalidArg (nameof intervalSec) "Interval seconds must be greater than 0"

        let eventId: Guid = Guid.NewGuid()

        lock lockObj (fun () ->
            state <-
                { EventAction = action
                  IntervalSec = intervalSec
                  LastExecutionTime = None
                  Id = eventId }
                :: state)

        eventId

    /// Execute periodic events whose interval has elapsed.
    member _.ExecuteDue() : unit =
        let now: DateTime = DateTime.Now

        let dueEvents: PeriodicEventItem list =
            lock lockObj (fun () ->
                let (dueEvents: PeriodicEventItem list), (remainingEvents: PeriodicEventItem list) =
                    state
                    |> List.map (fun (evt: PeriodicEventItem) ->
                        match evt.LastExecutionTime with
                        | None ->
                            Some evt,
                            { evt with
                                LastExecutionTime = Some now }
                        | Some(lastTime: DateTime) ->
                            if (now - lastTime).TotalSeconds >= evt.IntervalSec then
                                Some evt,
                                { evt with
                                    LastExecutionTime = Some now }
                            else
                                None, evt)
                    |> List.fold
                        (fun
                            (dueEvents: PeriodicEventItem list, updated: PeriodicEventItem list)
                            (dueOpt: PeriodicEventItem option, newEvent: PeriodicEventItem) ->
                            match dueOpt with
                            | Some(due: PeriodicEventItem) -> due :: dueEvents, newEvent :: updated
                            | None -> dueEvents, newEvent :: updated)
                        ([], [])

                state <- remainingEvents
                dueEvents)

        dueEvents |> List.iter (fun (evt: PeriodicEventItem) -> evt.EventAction())

    /// Get the current count of periodic events.
    member _.EventCount: int = lock lockObj (fun () -> List.length state)

    /// Clear all periodic events.
    member _.Clear() : unit = lock lockObj (fun () -> state <- [])

    /// Remove a specific periodic event by its unique identifier.
    member _.RemovePeriodic(eventId: Guid) : unit =
        lock lockObj (fun () -> state <- state |> List.filter (fun (evt: PeriodicEventItem) -> evt.Id <> eventId))
