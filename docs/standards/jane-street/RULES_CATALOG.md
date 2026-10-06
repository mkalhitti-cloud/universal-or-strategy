# Jane Street Rules Catalog

**Version**: 1.0  
**Last Updated**: 2026-06-03  
**Status**: Active Standard  
**Compliance**: V12 DNA Mandatory

---

## Overview

This catalog contains all DO/DON'T rules extracted from the 10 Jane Street standards documents. Rules are categorized by topic, assigned severity levels (P0/P1/P2), and include automation patterns for the rule checker.

**Rule Format**:
- **Rule ID**: Unique identifier (JS-XXX)
- **Category**: Type Safety, Performance, Concurrency, Testing, etc.
- **Severity**: P0 (CRITICAL - Blocking), P1 (HIGH - Warning), P2 (MEDIUM - Info)
- **Source**: Originating standards document
- **Pattern**: Regex or AST pattern for automation
- **DO/DON'T**: Code examples
- **Fix Suggestion**: Actionable remediation

---

## Category: Type Safety (20 rules)

### Rule JS-001: Use Result<T,E> Instead of Exceptions

**Category**: Type Safety  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 1

**Description**: Never throw exceptions in hot paths. Use Result<T,E> for explicit error handling.

**Pattern** (for automation):
```regex
throw\s+new\s+\w+Exception\((?!.*\/\/\s*Result<T,E>)
```

**DO**:
```csharp
public Result<OrderId, OrderError> SubmitOrder(Order order)
{
    return ValidateOrder(order)
        .Bind(validated => SendToExchange(validated));
}
```

**DON'T**:
```csharp
public OrderId SubmitOrder(Order order)
{
    if (!IsValid(order))
        throw new InvalidOrderException(); // ÔØî Exception in hot path
    return SendToExchange(order);
}
```

**Fix Suggestion**: Replace exception throwing with `Result<T,E>.Err(error)` and update return type.

**Related Rules**: JS-002 (Option<T>), JS-015 (Validated Types)

---

### Rule JS-002: Use Option<T> Instead of Null

**Category**: Type Safety  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 4

**Description**: Never return null for missing values. Use Option<T> or nullable reference types.

**Pattern** (for automation):
```regex
return\s+null\s*;(?!\s*//\s*Option<T>)
```

**DO**:
```csharp
public Option<User> FindUser(int id) => 
    _users.TryGetValue(id, out var user) 
        ? Option<User>.Some(user) 
        : Option<User>.None();
```

**DON'T**:
```csharp
public User FindUser(int id) => 
    _users.ContainsKey(id) ? _users[id] : null; // ÔØî Implicit null
```

**Fix Suggestion**: Change return type to `Option<T>` and use `Some`/`None` constructors.

**Related Rules**: JS-001 (Result<T,E>), JS-015 (Validated Types)

---

### Rule JS-003: Use Sealed Record Hierarchies for Sum Types

**Category**: Type Safety  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 2

**Description**: Model discriminated unions with sealed record hierarchies, not enums + separate data classes.

**Pattern** (for automation):
```regex
public\s+enum\s+\w+State.*\n.*public\s+class\s+\w+Data
```

**DO**:
```csharp
public abstract record OrderState
{
    private OrderState() { }
    public sealed record Pending(int OrderId) : OrderState;
    public sealed record Filled(int OrderId, double Price) : OrderState;
}
```

**DON'T**:
```csharp
public enum OrderState { Pending, Filled }
public class OrderData { public int OrderId; public double? Price; } // ÔØî Separate data
```

**Fix Suggestion**: Convert enum to sealed record hierarchy with data embedded in each variant.

**Related Rules**: JS-004 (Exhaustive Matching), JS-010 (Private Constructors)

---

### Rule JS-004: Use Switch Expressions for Exhaustive Matching

**Category**: Type Safety  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_TYPE_SAFETY.md, Pattern 3

**Description**: Use switch expressions with sealed hierarchies to ensure exhaustive matching.

**Pattern** (for automation):
```regex
switch\s*\(.*\)\s*\{.*default\s*:.*\}
```

**DO**:
```csharp
public string Describe(OrderState state) =>
    state switch
    {
        OrderState.Pending p => $"Pending {p.OrderId}",
        OrderState.Filled f => $"Filled at {f.Price}",
        _ => throw new InvalidOperationException("Unreachable")
    };
```

**DON'T**:
```csharp
public string Describe(OrderState state)
{
    switch (state)
    {
        case OrderState.Pending: return "Pending";
        default: return "Unknown"; // ÔØî Hides missing cases
    }
}
```

**Fix Suggestion**: Use switch expression and remove default case to expose missing patterns.

**Related Rules**: JS-003 (Sealed Hierarchies), JS-005 (Enable Nullable)

---

### Rule JS-005: Enable Nullable Reference Types

**Category**: Type Safety  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_TYPE_SAFETY.md, Pattern 4

**Description**: Enable nullable reference types to catch null reference errors at compile time.

**Pattern** (for automation):
```regex
^(?!.*#nullable\s+enable)
```

**DO**:
```csharp
#nullable enable

public string GetSymbol(Option<Order> orderOpt) =>
    orderOpt.Match(
        onSome: order => order.Symbol.Value,
        onNone: () => "UNKNOWN");
```

**DON'T**:
```csharp
// ÔØî No #nullable enable
public string GetSymbol(Order order) => order?.Symbol ?? "UNKNOWN";
```

**Fix Suggestion**: Add `#nullable enable` at top of file and fix all warnings.

**Related Rules**: JS-002 (Option<T>), JS-004 (Exhaustive Matching)

---

### Rule JS-006: Use Phantom Types for Units

**Category**: Type Safety  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_TYPE_SAFETY.md, Pattern 1

**Description**: Use phantom types to prevent mixing incompatible units (currency, distance, time).

**Pattern** (for automation):
```regex
public\s+\w+\s+\w+(USD|EUR|GBP)\s*\+\s*\w+(USD|EUR|GBP)(?!\1)
```

**DO**:
```csharp
public readonly struct Price<TCurrency> where TCurrency : struct, ICurrency
{
    public static Price<TCurrency> operator +(Price<TCurrency> a, Price<TCurrency> b) => ...
}
```

**DON'T**:
```csharp
public double UsdPrice + double EurPrice; // ÔØî Can mix currencies
```

**Fix Suggestion**: Wrap primitives in generic structs with currency marker types.

**Related Rules**: JS-007 (Newtype Pattern), JS-010 (Private Constructors)

---

### Rule JS-007: Use Newtype Pattern for Semantic Types

**Category**: Type Safety  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_TYPE_SAFETY.md, Pattern 2

**Description**: Wrap primitives in readonly structs to prevent mixing semantically different values.

**Pattern** (for automation):
```regex
public\s+\w+\s+\w+\(int\s+orderId,\s+int\s+userId\)
```

**DO**:
```csharp
public readonly struct OrderId { private readonly int _value; }
public readonly struct UserId { private readonly int _value; }
public void Process(OrderId orderId, UserId userId) { }
```

**DON'T**:
```csharp
public void Process(int orderId, int userId) { } // ÔØî Can swap arguments
```

**Fix Suggestion**: Create readonly struct wrappers with private constructors and smart constructors.

**Related Rules**: JS-006 (Phantom Types), JS-010 (Private Constructors)

---

### Rule JS-008: Use Readonly Structs for Immutable Data

**Category**: Type Safety  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 3

**Description**: Use readonly structs for small immutable data (<16 bytes).

**Pattern** (for automation):
```regex
public\s+struct\s+\w+(?!\s+:\s+readonly)
```

**DO**:
```csharp
public readonly struct MarketData
{
    public readonly double Bid { get; init; }
    public readonly double Ask { get; init; }
}
```

**DON'T**:
```csharp
public struct MarketData
{
    public double Bid { get; set; } // ÔØî Mutable
}
```

**Fix Suggestion**: Add `readonly` modifier to struct and make all fields `readonly` or `init`-only.

**Related Rules**: JS-009 (Immutable Collections), JS-011 (With Expressions)

---

### Rule JS-009: Use ImmutableDictionary for Persistent Collections

**Category**: Type Safety  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 3

**Description**: Use ImmutableDictionary instead of Dictionary for persistent collections.

**Pattern** (for automation):
```regex
Dictionary<\w+,\s*\w+>\s+\w+\s*=\s*new\(\)
```

**DO**:
```csharp
public ImmutableDictionary<string, MarketData> UpdateSymbols(
    ImmutableDictionary<string, MarketData> dataMap,
    IEnumerable<(string, double)> updates) => ...
```

**DON'T**:
```csharp
public Dictionary<string, MarketData> dataMap = new(); // ÔØî Mutable
```

**Fix Suggestion**: Replace `Dictionary<K,V>` with `ImmutableDictionary<K,V>`.

**Related Rules**: JS-008 (Readonly Structs), JS-011 (With Expressions)

---

### Rule JS-010: Use Private Constructors for Smart Constructors

**Category**: Type Safety  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 6

**Description**: Use private constructors + factory methods to enforce invariants.

**Pattern** (for automation):
```regex
public\s+\w+\(.*\)\s*\{(?!.*private\s+\w+\()
```

**DO**:
```csharp
public sealed class Order
{
    private Order(...) { }
    public static Result<Order, string> CreateLimitOrder(...) { }
}
```

**DON'T**:
```csharp
public class Order
{
    public Order(double price) { } // ÔØî Public constructor, no validation
}
```

**Fix Suggestion**: Make constructor private, add static factory method with validation.

**Related Rules**: JS-001 (Result<T,E>), JS-015 (Validated Types)

---

### Rule JS-011: Use With Expressions for Functional Updates

**Category**: Type Safety  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 3

**Description**: Use `with` expressions for functional updates of immutable data.

**Pattern** (for automation):
```regex
\w+\.\w+\s*=\s*\w+;(?!.*with)
```

**DO**:
```csharp
public MarketData WithBid(double newBid) =>
    this with { Bid = newBid, LastUpdate = DateTimeOffset.UtcNow };
```

**DON'T**:
```csharp
public void UpdateBid(double newBid)
{
    this.Bid = newBid; // ÔØî Mutation
}
```

**Fix Suggestion**: Replace mutation with `with` expression returning new instance.

**Related Rules**: JS-008 (Readonly Structs), JS-009 (Immutable Collections)

---

### Rule JS-012: Use Bind for Monadic Composition

**Category**: Type Safety  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 5

**Description**: Use Bind operator to chain Result<T,E> and Option<T> operations.

**Pattern** (for automation):
```regex
if\s*\(.*\.IsOk\).*if\s*\(.*\.IsOk\)
```

**DO**:
```csharp
public Result<OrderId, OrderError> ProcessOrder(Order order) =>
    ValidateOrder(order)
        .Bind(EnrichWithMarketData)
        .Bind(SubmitToExchange);
```

**DON'T**:
```csharp
var result1 = ValidateOrder(order);
if (!result1.IsOk) return result1.Error;
var result2 = EnrichWithMarketData(result1.Value);
if (!result2.IsOk) return result2.Error; // ÔØî Nested if/else
```

**Fix Suggestion**: Replace nested if/else with `.Bind()` chain.

**Related Rules**: JS-001 (Result<T,E>), JS-002 (Option<T>)

---

### Rule JS-013: Use Extension Methods for Pipeline Operators

**Category**: Type Safety  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 5

**Description**: Use extension methods to create pipeline-style APIs.

**Pattern** (for automation):
```regex
public\s+static\s+\w+\s+Pipe<.*>\(this\s+\w+\s+value
```

**DO**:
```csharp
public static U Pipe<T, U>(this T value, Func<T, U> f) => f(value);

var result = order
    .Pipe(ValidateOrder)
    .Bind(EnrichWithMarketData);
```

**DON'T**:
```csharp
var validated = ValidateOrder(order);
var enriched = EnrichWithMarketData(validated); // ÔØî Imperative style
```

**Fix Suggestion**: Create extension methods for common operations.

**Related Rules**: JS-012 (Bind), JS-014 (LINQ Query Syntax)

---

### Rule JS-014: Use LINQ Query Syntax for Complex Pipelines

**Category**: Type Safety  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 5

**Description**: Use LINQ query syntax for complex monadic pipelines.

**Pattern** (for automation):
```regex
\.Bind\(.*\.Bind\(.*\.Bind\(
```

**DO**:
```csharp
public Result<OrderId, OrderError> ProcessOrder(Order order) =>
    from validated in ValidateOrder(order)
    from enriched in EnrichWithMarketData(validated)
    from orderId in SubmitToExchange(enriched)
    select orderId;
```

**DON'T**:
```csharp
ValidateOrder(order)
    .Bind(v => EnrichWithMarketData(v)
        .Bind(e => SubmitToExchange(e))); // ÔØî Deeply nested
```

**Fix Suggestion**: Convert nested Bind to LINQ query syntax.

**Related Rules**: JS-012 (Bind), JS-013 (Pipeline Operators)

---

### Rule JS-015: Parse at Boundaries, Use Validated Types Internally

**Category**: Type Safety  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_TYPE_SAFETY.md, Pattern 5

**Description**: Parse and validate at system boundaries, use validated types internally.

**Pattern** (for automation):
```regex
public\s+\w+\s+\w+\(string\s+email\)(?!.*Email\.Parse)
```

**DO**:
```csharp
public readonly struct Email
{
    private Email(string value) { }
    public static Result<Email, string> Parse(string value) { }
}

public void SendEmail(Email email, string body) { } // No validation needed
```

**DON'T**:
```csharp
public void SendEmail(string email, string body)
{
    if (!email.Contains('@')) throw new Exception(); // ÔØî Repeated validation
}
```

**Fix Suggestion**: Create validated type with Parse method, use internally.

**Related Rules**: JS-010 (Private Constructors), JS-001 (Result<T,E>)

---

### Rule JS-016: Use Type-Level State Machines

**Category**: Type Safety  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_TYPE_SAFETY.md, Pattern 6

**Description**: Use phantom types to encode state machine transitions at compile time.

**Pattern** (for automation):
```regex
public\s+enum\s+\w+State.*\n.*public\s+void\s+\w+\(.*State\s+state\)
```

**DO**:
```csharp
public sealed class Connection<TState> where TState : struct, IConnectionState
{
    public async ValueTask<Connection<Authenticated>> AuthenticateAsync(
        this Connection<Connected> conn) { }
}
```

**DON'T**:
```csharp
public enum ConnectionState { Disconnected, Connected, Authenticated }
public void SendMessage(ConnectionState state) { } // ÔØî Runtime check
```

**Fix Suggestion**: Use phantom types to make invalid state transitions unrepresentable.

**Related Rules**: JS-006 (Phantom Types), JS-003 (Sealed Hierarchies)

---

### Rule JS-017: Use Generic Constraints for Dependent Types

**Category**: Type Safety  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_TYPE_SAFETY.md, Pattern 7

**Description**: Use generic constraints to enforce type relationships at compile time.

**Pattern** (for automation):
```regex
public\s+interface\s+IExpr\s*\{(?!.*<T>)
```

**DO**:
```csharp
public interface IExpr<T> { }
public sealed record AddExpr(IExpr<int> Left, IExpr<int> Right) : IExpr<int>;
```

**DON'T**:
```csharp
public interface IExpr { }
public class AddExpr { public IExpr Left; public IExpr Right; } // ÔØî No type safety
```

**Fix Suggestion**: Add generic type parameter to interface and constrain implementations.

**Related Rules**: JS-006 (Phantom Types), JS-016 (Type-Level FSM)

---

### Rule JS-018: Implement IEquatable<T> for Value Types

**Category**: Type Safety  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_TYPE_SAFETY.md, Pattern 2

**Description**: Implement IEquatable<T> for readonly structs to avoid boxing.

**Pattern** (for automation):
```regex
public\s+readonly\s+struct\s+\w+(?!.*:\s*IEquatable)
```

**DO**:
```csharp
public readonly struct OrderId : IEquatable<OrderId>
{
    public bool Equals(OrderId other) => _value == other._value;
    public override int GetHashCode() => _value.GetHashCode();
}
```

**DON'T**:
```csharp
public readonly struct OrderId
{
    // ÔØî No IEquatable, causes boxing
}
```

**Fix Suggestion**: Implement IEquatable<T>, override Equals/GetHashCode, add == and != operators.

**Related Rules**: JS-007 (Newtype Pattern), JS-008 (Readonly Structs)

---

### Rule JS-019: Override ToString for Debugging

**Category**: Type Safety  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_TYPE_SAFETY.md, Pattern 2

**Description**: Override ToString for custom types to aid debugging.

**Pattern** (for automation):
```regex
public\s+readonly\s+struct\s+\w+(?!.*override\s+string\s+ToString)
```

**DO**:
```csharp
public readonly struct OrderId
{
    public override string ToString() => $"OrderId({_value})";
}
```

**DON'T**:
```csharp
public readonly struct OrderId
{
    // ÔØî No ToString, prints type name only
}
```

**Fix Suggestion**: Add `override string ToString()` with meaningful representation.

**Related Rules**: JS-007 (Newtype Pattern), JS-018 (IEquatable)

---

### Rule JS-020: Use Records for Data Transfer Objects

**Category**: Type Safety  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 2

**Description**: Use records for DTOs to get value semantics and immutability.

**Pattern** (for automation):
```regex
public\s+class\s+\w+DTO\s*\{(?!.*record)
```

**DO**:
```csharp
public record OrderDTO(int OrderId, double Price, int Quantity);
```

**DON'T**:
```csharp
public class OrderDTO
{
    public int OrderId { get; set; } // ÔØî Mutable class
}
```

**Fix Suggestion**: Convert class to record with positional parameters.

**Related Rules**: JS-008 (Readonly Structs), JS-011 (With Expressions)

---

## Category: Concurrency (15 rules)

### Rule JS-021: No Lock() Usage

**Category**: Concurrency  
**Severity**: P0 (CRITICAL)  
**Source**: AGENTS.md, Section 2

**Description**: Lock usage is strictly banned. Use Actor/FSM pattern or atomic primitives.

**Pattern** (for automation):
```regex
lock\s*\(
```

**DO**:
```csharp
private readonly Channel<Message> _channel = Channel.CreateUnbounded<Message>();

public async ValueTask EnqueueAsync(Message msg) =>
    await _channel.Writer.WriteAsync(msg);
```

**DON'T**:
```csharp
private readonly object _lock = new();

public void UpdateState()
{
    lock (_lock) { } // ÔØî BANNED
}
```

**Fix Suggestion**: Replace lock with Channel-based Actor pattern or Interlocked operations.

**Related Rules**: JS-022 (Actor Pattern), JS-023 (Atomic Primitives)

---

### Rule JS-022: Use Actor Pattern for Stateful Concurrency

**Category**: Concurrency  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_ASYNC_PATTERNS.md, Pattern 2

**Description**: Use Channel-based Actor pattern for stateful message processing.

**Pattern** (for automation):
```regex
private\s+\w+\s+_state;.*lock\s*\(
```

**DO**:
```csharp
public sealed class OrderActor
{
    private readonly Channel<Message> _mailbox = Channel.CreateUnbounded<Message>();
    
    public async ValueTask EnqueueAsync(Message msg) =>
        await _mailbox.Writer.WriteAsync(msg);
        
    private async Task ProcessMessagesAsync()
    {
        await foreach (var msg in _mailbox.Reader.ReadAllAsync())
        {
            _state = HandleMessage(_state, msg);
        }
    }
}
```

**DON'T**:
```csharp
private OrderState _state;
private readonly object _lock = new();

public void UpdateState(Message msg)
{
    lock (_lock) { _state = HandleMessage(_state, msg); } // ÔØî Lock
}
```

**Fix Suggestion**: Convert to Actor pattern with Channel-based mailbox.

**Related Rules**: JS-021 (No Lock), JS-024 (Structured Concurrency)

---

### Rule JS-023: Use Atomic Primitives for Simple State

**Category**: Concurrency  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_PERFORMANCE_PATTERNS.md, Pattern 5

**Description**: Use Interlocked operations for simple atomic state updates.

**Pattern** (for automation):
```regex
lock\s*\(\w+\)\s*\{\s*\w+\s*[+\-*/]=
```

**DO**:
```csharp
private long _counter;

public void Increment() =>
    Interlocked.Increment(ref _counter);
```

**DON'T**:
```csharp
private long _counter;
private readonly object _lock = new();

public void Increment()
{
    lock (_lock) { _counter++; } // ÔØî Lock for simple increment
}
```

**Fix Suggestion**: Replace lock with Interlocked.Increment/Add/CompareExchange.

**Related Rules**: JS-021 (No Lock), JS-025 (Lock-Free Data Structures)

---

### Rule JS-024: Use Structured Concurrency

**Category**: Concurrency  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_ASYNC_PATTERNS.md, Pattern 3

**Description**: Use Task.WhenAll for scoped parallelism, ensure all tasks complete.

**Pattern** (for automation):
```regex
Task\.Run\(.*\);(?!.*await)
```

**DO**:
```csharp
public async Task ProcessOrdersAsync(Order[] orders)
{
    var tasks = orders.Select(ProcessOrderAsync).ToArray();
    await Task.WhenAll(tasks); // Structured: all tasks complete
}
```

**DON'T**:
```csharp
public void ProcessOrders(Order[] orders)
{
    foreach (var order in orders)
        Task.Run(() => ProcessOrder(order)); // ÔØî Fire-and-forget
}
```

**Fix Suggestion**: Use Task.WhenAll and await all tasks before returning.

**Related Rules**: JS-026 (Backpressure), JS-027 (Timeout)

---

### Rule JS-025: Use Lock-Free Data Structures

**Category**: Concurrency  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_PERFORMANCE_PATTERNS.md, Pattern 5

**Description**: Use ConcurrentQueue/ConcurrentDictionary instead of lock-protected collections.

**Pattern** (for automation):
```regex
lock\s*\(\w+\)\s*\{\s*\w+\.Add\(
```

**DO**:
```csharp
private readonly ConcurrentQueue<Order> _orders = new();

public void AddOrder(Order order) =>
    _orders.Enqueue(order);
```

**DON'T**:
```csharp
private readonly List<Order> _orders = new();
private readonly object _lock = new();

public void AddOrder(Order order)
{
    lock (_lock) { _orders.Add(order); } // ÔØî Lock-protected collection
}
```

**Fix Suggestion**: Replace lock-protected collection with concurrent collection.

**Related Rules**: JS-021 (No Lock), JS-023 (Atomic Primitives)

---

### Rule JS-026: Implement Backpressure

**Category**: Concurrency  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_ASYNC_PATTERNS.md, Pattern 4

**Description**: Use bounded channels to implement backpressure and prevent memory exhaustion.

**Pattern** (for automation):
```regex
Channel\.CreateUnbounded<\w+>\(\)
```

**DO**:
```csharp
private readonly Channel<Message> _channel = 
    Channel.CreateBounded<Message>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.Wait
    });
```

**DON'T**:
```csharp
private readonly Channel<Message> _channel = 
    Channel.CreateUnbounded<Message>(); // ÔØî No backpressure
```

**Fix Suggestion**: Use CreateBounded with appropriate capacity and FullMode.

**Related Rules**: JS-022 (Actor Pattern), JS-024 (Structured Concurrency)

---

### Rule JS-027: Use Timeout for All Async Operations

**Category**: Concurrency  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_ASYNC_PATTERNS.md, Pattern 6

**Description**: Always use CancellationToken with timeout for async operations.

**Pattern** (for automation):
```regex
async\s+\w+\s+\w+Async\([^)]*\)(?!.*CancellationToken)
```

**DO**:
```csharp
public async ValueTask<Result<Order, string>> FetchOrderAsync(
    int orderId,
    CancellationToken ct = default)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(TimeSpan.FromSeconds(5));
    return await _client.GetOrderAsync(orderId, cts.Token);
}
```

**DON'T**:
```csharp
public async Task<Order> FetchOrderAsync(int orderId)
{
    return await _client.GetOrderAsync(orderId); // ÔØî No timeout
}
```

**Fix Suggestion**: Add CancellationToken parameter and use CancelAfter.

**Related Rules**: JS-028 (Retry with Backoff), JS-024 (Structured Concurrency)

---

### Rule JS-028: Implement Retry with Exponential Backoff

**Category**: Concurrency  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_ASYNC_PATTERNS.md, Pattern 6

**Description**: Use exponential backoff for retry logic to avoid thundering herd.

**Pattern** (for automation):
```regex
for\s*\(.*retries.*\)\s*\{.*await.*\}(?!.*Task\.Delay)
```

**DO**:
```csharp
public async ValueTask<Result<T, string>> RetryAsync<T>(
    Func<CancellationToken, ValueTask<Result<T, string>>> operation,
    int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        var result = await operation(ct);
        if (result.IsOk) return result;
        
        var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, i));
        await Task.Delay(delay, ct);
    }
}
```

**DON'T**:
```csharp
for (int i = 0; i < 3; i++)
{
    var result = await operation();
    if (result.IsOk) return result;
    // ÔØî No delay, thundering herd
}
```

**Fix Suggestion**: Add exponential backoff delay between retries.

**Related Rules**: JS-027 (Timeout), JS-029 (Circuit Breaker)

---

### Rule JS-029: Use Circuit Breaker for External Services

**Category**: Concurrency  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_ASYNC_PATTERNS.md, Pattern 6

**Description**: Implement circuit breaker pattern for external service calls.

**Pattern** (for automation):
```regex
await\s+_httpClient\.GetAsync\((?!.*CircuitBreaker)
```

**DO**:
```csharp
private readonly CircuitBreaker _breaker = new(
    failureThreshold: 5,
    timeout: TimeSpan.FromSeconds(30));

public async ValueTask<Result<T, string>> CallServiceAsync<T>()
{
    if (_breaker.IsOpen)
        return Result<T, string>.Err("Circuit breaker open");
        
    try
    {
        var result = await _httpClient.GetAsync(url);
        _breaker.RecordSuccess();
        return result;
    }
    catch
    {
        _breaker.RecordFailure();
        throw;
    }
}
```

**DON'T**:
```csharp
public async Task<T> CallServiceAsync<T>()
{
    return await _httpClient.GetAsync(url); // ÔØî No circuit breaker
}
```

**Fix Suggestion**: Wrap external calls in circuit breaker pattern.

**Related Rules**: JS-027 (Timeout), JS-028 (Retry)

---

### Rule JS-030: Use AsyncLocal for Context Propagation

**Category**: Concurrency  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_ASYNC_PATTERNS.md, Pattern 7

**Description**: Use AsyncLocal<T> for async context propagation, not ThreadLocal<T>.

**Pattern** (for automation):
```regex
private\s+static\s+readonly\s+ThreadLocal<\w+>
```

**DO**:
```csharp
private static readonly AsyncLocal<RequestContext> _context = new();

public static RequestContext Current
{
    get => _context.Value;
    set => _context.Value = value;
}
```

**DON'T**:
```csharp
private static readonly ThreadLocal<RequestContext> _context = new(); // ÔØî ThreadLocal
```

**Fix Suggestion**: Replace ThreadLocal<T> with AsyncLocal<T>.

**Related Rules**: JS-024 (Structured Concurrency), JS-031 (ConfigureAwait)

---

### Rule JS-031: Use ConfigureAwait(false) in Libraries

**Category**: Concurrency  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_ASYNC_PATTERNS.md, Pattern 1

**Description**: Use ConfigureAwait(false) in library code to avoid capturing SynchronizationContext.

**Pattern** (for automation):
```regex
await\s+\w+\.(?!.*ConfigureAwait)
```

**DO**:
```csharp
public async ValueTask<Result<T, string>> FetchAsync<T>()
{
    var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
    return await ProcessResponseAsync(response).ConfigureAwait(false);
}
```

**DON'T**:
```csharp
public async Task<T> FetchAsync<T>()
{
    var response = await _httpClient.GetAsync(url); // ÔØî Captures context
}
```

**Fix Suggestion**: Add `.ConfigureAwait(false)` to all awaits in library code.

**Related Rules**: JS-027 (Timeout), JS-030 (AsyncLocal)

---

### Rule JS-032: Use ValueTask for Hot Paths

**Category**: Concurrency  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_ASYNC_PATTERNS.md, Pattern 1

**Description**: Use ValueTask<T> instead of Task<T> in hot paths to avoid allocation.

**Pattern** (for automation):
```regex
public\s+async\s+Task<\w+>\s+\w+Async\((?!.*ValueTask)
```

**DO**:
```csharp
public async ValueTask<Result<Order, string>> GetOrderAsync(int id)
{
    if (_cache.TryGetValue(id, out var order))
        return Result<Order, string>.Ok(order); // No allocation
        
    return await FetchFromDatabaseAsync(id);
}
```

**DON'T**:
```csharp
public async Task<Order> GetOrderAsync(int id)
{
    if (_cache.TryGetValue(id, out var order))
        return order; // ÔØî Allocates Task
}
```

**Fix Suggestion**: Change return type from Task<T> to ValueTask<T>.

**Related Rules**: JS-033 (Zero-Allocation), JS-034 (Span<T>)

---

### Rule JS-033: Avoid Async Void

**Category**: Concurrency  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_ASYNC_PATTERNS.md, Pattern 1

**Description**: Never use async void except for event handlers.

**Pattern** (for automation):
```regex
async\s+void\s+\w+\((?!.*EventHandler)
```

**DO**:
```csharp
public async ValueTask ProcessOrderAsync(Order order)
{
    await _channel.Writer.WriteAsync(order);
}
```

**DON'T**:
```csharp
public async void ProcessOrder(Order order) // ÔØî async void
{
    await _channel.Writer.WriteAsync(order);
}
```

**Fix Suggestion**: Change return type to ValueTask or Task.

**Related Rules**: JS-032 (ValueTask), JS-024 (Structured Concurrency)

---

### Rule JS-034: Use Async Sequences for Lazy Streams

**Category**: Concurrency  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_ASYNC_PATTERNS.md, Pattern 5

**Description**: Use IAsyncEnumerable<T> for lazy async streams.

**Pattern** (for automation):
```regex
public\s+async\s+Task<List<\w+>>\s+\w+Async\(
```

**DO**:
```csharp
public async IAsyncEnumerable<Order> StreamOrdersAsync(
    [EnumeratorCancellation] CancellationToken ct = default)
{
    await foreach (var order in _channel.Reader.ReadAllAsync(ct))
    {
        yield return order;
    }
}
```

**DON'T**:
```csharp
public async Task<List<Order>> GetAllOrdersAsync()
{
    var orders = new List<Order>();
    await foreach (var order in _channel.Reader.ReadAllAsync())
        orders.Add(order); // ÔØî Loads all into memory
    return orders;
}
```

**Fix Suggestion**: Change return type to IAsyncEnumerable<T> and use yield return.

**Related Rules**: JS-026 (Backpressure), JS-032 (ValueTask)

---

### Rule JS-035: Use SemaphoreSlim for Async Coordination

**Category**: Concurrency  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_ASYNC_PATTERNS.md, Pattern 7

**Description**: Use SemaphoreSlim (not Semaphore) for async coordination.

**Pattern** (for automation):
```regex
private\s+readonly\s+Semaphore\s+\w+
```

**DO**:
```csharp
private readonly SemaphoreSlim _semaphore = new(1, 1);

public async ValueTask<T> ExecuteAsync<T>(Func<ValueTask<T>> operation)
{
    await _semaphore.WaitAsync();
    try
    {
        return await operation();
    }
    finally
    {
        _semaphore.Release();
    }
}
```

**DON'T**:
```csharp
private readonly Semaphore _semaphore = new(1, 1); // ÔØî Blocks thread

public T Execute<T>(Func<T> operation)
{
    _semaphore.WaitOne();
    try { return operation(); }
    finally { _semaphore.Release(); }
}
```

**Fix Suggestion**: Replace Semaphore with SemaphoreSlim and use WaitAsync.

**Related Rules**: JS-021 (No Lock), JS-024 (Structured Concurrency)

---

## Category: Performance (15 rules)

### Rule JS-036: Use Span<T> for Zero-Allocation

**Category**: Performance  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 7

**Description**: Use Span<T> for stack-allocated buffers in hot paths.

**Pattern** (for automation):
```regex
byte\[\]\s+buffer\s*=\s*new\s+byte\[
```

**DO**:
```csharp
public void ProcessMessages(ReadOnlySpan<Message> messages)
{
    Span<byte> buffer = stackalloc byte[4096];
    foreach (ref readonly Message msg in messages)
    {
        SerializeToSpan(msg, buffer);
    }
}
```

**DON'T**:
```csharp
public void ProcessMessages(Message[] messages)
{
    byte[] buffer = new byte[4096]; // ÔØî Heap allocation
}
```

**Fix Suggestion**: Replace byte[] with Span<byte> and use stackalloc.

**Related Rules**: JS-037 (ArrayPool), JS-038 (Ref Readonly)

---

### Rule JS-037: Use ArrayPool for Reusable Buffers

**Category**: Performance  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 7

**Description**: Use ArrayPool<T> for reusable buffers to avoid allocation.

**Pattern** (for automation):
```regex
new\s+byte\[\d+\](?!.*ArrayPool)
```

**DO**:
```csharp
private readonly ArrayPool<byte> _pool = ArrayPool<byte>.Shared;

public void ProcessMessage(Message msg)
{
    byte[] buffer = _pool.Rent(4096);
    try
    {
        SerializeToBuffer(msg, buffer);
    }
    finally
    {
        _pool.Return(buffer);
    }
}
```

**DON'T**:
```csharp
public void ProcessMessage(Message msg)
{
    byte[] buffer = new byte[4096]; // ÔØî Allocates every call
}
```

**Fix Suggestion**: Use ArrayPool.Rent/Return instead of new[].

**Related Rules**: JS-036 (Span<T>), JS-039 (Memory<T>)

---

### Rule JS-038: Use Ref Readonly for Zero-Copy Iteration

**Category**: Performance  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 7

**Description**: Use `ref readonly` for zero-copy iteration over structs.

**Pattern** (for automation):
```regex
foreach\s*\(\s*var\s+\w+\s+in\s+\w+\)(?!.*ref\s+readonly)
```

**DO**:
```csharp
public void ProcessOrders(ReadOnlySpan<Order> orders)
{
    foreach (ref readonly Order order in orders)
    {
        ProcessOrder(order); // No copy
    }
}
```

**DON'T**:
```csharp
public void ProcessOrders(Order[] orders)
{
    foreach (var order in orders) // ÔØî Copies struct
    {
        ProcessOrder(order);
    }
}
```

**Fix Suggestion**: Use `ref readonly` in foreach for struct iteration.

**Related Rules**: JS-036 (Span<T>), JS-040 (Readonly Struct)

---

### Rule JS-039: Use Memory<T> for Async Buffer Operations

**Category**: Performance  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_PERFORMANCE_PATTERNS.md, Pattern 1

**Description**: Use Memory<T> instead of Span<T> for async operations.

**Pattern** (for automation):
```regex
async\s+\w+\s+\w+Async\(.*Span<\w+>
```

**DO**:
```csharp
public async ValueTask WriteAsync(Memory<byte> buffer, CancellationToken ct)
{
    await _stream.WriteAsync(buffer, ct);
}
```

**DON'T**:
```csharp
public async Task WriteAsync(Span<byte> buffer) // ÔØî Span in async
{
    await _stream.WriteAsync(buffer.ToArray()); // Allocates
}
```

**Fix Suggestion**: Replace Span<T> with Memory<T> in async methods.

**Related Rules**: JS-036 (Span<T>), JS-032 (ValueTask)

---

### Rule JS-040: Use Readonly Struct for Small Value Types

**Category**: Performance  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_PERFORMANCE_PATTERNS.md, Pattern 1

**Description**: Use readonly struct for small value types (<16 bytes) to avoid defensive copies.

**Pattern** (for automation):
```regex
public\s+struct\s+\w+(?!.*readonly).*\{.*\}
```

**DO**:
```csharp
public readonly struct Price
{
    private readonly double _value;
    public Price(double value) => _value = value;
}
```

**DON'T**:
```csharp
public struct Price // ÔØî Not readonly, defensive copies
{
    public double Value { get; set; }
}
```

**Fix Suggestion**: Add `readonly` modifier to struct and make all fields readonly.

**Related Rules**: JS-008 (Readonly Structs), JS-038 (Ref Readonly)

---

### Rule JS-041: Use StructLayout for Cache-Friendly Data

**Category**: Performance  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_PERFORMANCE_PATTERNS.md, Pattern 2

**Description**: Use StructLayout(LayoutKind.Sequential) for cache-friendly data structures.

**Pattern** (for automation):
```regex
public\s+struct\s+\w+(?!.*\[StructLayout)
```

**DO**:
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly struct MarketData
{
    public readonly long Timestamp;
    public readonly double Bid;
    public readonly double Ask;
}
```

**DON'T**:
```csharp
public struct MarketData // ÔØî Auto layout, cache misses
{
    public double Bid;
    public long Timestamp;
    public double Ask;
}
```

**Fix Suggestion**: Add [StructLayout(LayoutKind.Sequential)] and order fields by access pattern.


### Rule JS-042: Use MemoryMarshal for Low-Level Access

**Category**: Performance  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_PERFORMANCE_PATTERNS.md, Pattern 2

**Description**: Use MemoryMarshal.Cast for zero-copy reinterpretation of buffers.

**Pattern** (for automation):
```regex
BitConverter\.GetBytes\(|BitConverter\.ToInt
```

**DO**:
```csharp
public static long ReadTimestamp(ReadOnlySpan<byte> buf) =>
    MemoryMarshal.Read<long>(buf.Slice(0, 8));
```

**DON'T**:
```csharp
public static long ReadTimestamp(byte[] buf) =>
    BitConverter.ToInt64(buf, 0); // ÔØî Copies bytes
```

**Fix Suggestion**: Use MemoryMarshal.Read/Write or MemoryMarshal.Cast for zero-copy reinterpretation.

**Related Rules**: JS-036 (Span<T>), JS-041 (StructLayout)

---

### Rule JS-043: Profile Before Optimizing

**Category**: Performance  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_PERFORMANCE_PATTERNS.md, Pattern 3

**Description**: Always measure with BenchmarkDotNet before applying micro-optimizations.

**Pattern** (for automation):
```regex
// TODO.*optimize|// FIXME.*slow|// PERF.*consider
```

**DO**:
```csharp
[MemoryDiagnoser]
public class OrderBenchmarks
{
    [Benchmark] public void ProcessOrder() { ... }
}
```

**DON'T**:
```csharp
// Optimize this later -- looks slow
public void ProcessOrder() { ... }
```

**Fix Suggestion**: Write a benchmark before optimizing. Optimize only what measurements confirm.

**Related Rules**: JS-044 (Avoid Premature Optimization), JS-057 (BenchmarkDotNet)

---

### Rule JS-044: Avoid Premature Optimization

**Category**: Performance  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_PERFORMANCE_PATTERNS.md, Pattern 3

**Description**: Write correct code first, optimize only proven bottlenecks.

**Pattern** (for automation):
```regex
unsafe\s+\{|fixed\s+\((?!.*\/\/\s*measured)
```

**DO**:
```csharp
public void ProcessOrder(Order order)
{
    // Correct, readable. Profiler confirms this is not on hot path.
    var enriched = Enrich(order);
    Submit(enriched);
}
```

**DON'T**:
```csharp
unsafe public void ProcessOrder(Order* order) // ÔØî Premature unsafe
{
    // No profiling data
}
```

**Fix Suggestion**: Add profiling data comment before any unsafe block.

**Related Rules**: JS-043 (Profile First), JS-045 (Inline Hot Paths)

---

### Rule JS-045: Use AggressiveInlining for Hot Paths

**Category**: Performance  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_PERFORMANCE_PATTERNS.md, Pattern 4

**Description**: Use [MethodImpl(MethodImplOptions.AggressiveInlining)] on hot path leaf methods.

**Pattern** (for automation):
```regex
public\s+\w+\s+\w+\(\)\s*=>\s*\w+;(?!.*AggressiveInlining)
```

**DO**:
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static double MidPrice(double bid, double ask) =>
    (bid + ask) * 0.5;
```

**DON'T**:
```csharp
public static double MidPrice(double bid, double ask) =>
    (bid + ask) * 0.5; // ÔØî JIT may not inline in tight loops
```

**Fix Suggestion**: Add [MethodImpl(AggressiveInlining)] to short hot-path methods.

**Related Rules**: JS-043 (Profile First), JS-046 (NoAlloc on Hot Path)

---

### Rule JS-046: Zero Allocation on Hot Path

**Category**: Performance  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_PERFORMANCE_PATTERNS.md, Pattern 4

**Description**: Hot path methods must allocate zero heap objects per invocation.

**Pattern** (for automation):
```regex
new\s+\w+\(|new\s+\w+\[\d
```

**DO**:
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public void OnTick(double bid, double ask)
{
    Span<byte> buf = stackalloc byte[16]; // Stack only
    Serialize(bid, ask, buf);
    _pipe.Write(buf);
}
```

**DON'T**:
```csharp
public void OnTick(double bid, double ask)
{
    var msg = new TickMessage(bid, ask); // ÔØî Heap allocation on every tick
    _queue.Enqueue(msg);
}
```

**Fix Suggestion**: Use stackalloc or pooled structs. Replace class with struct on hot path.

**Related Rules**: JS-036 (Span<T>), JS-037 (ArrayPool), JS-045 (Inlining)

---

### Rule JS-047: Use SIMD for Batch Computations

**Category**: Performance  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_PERFORMANCE_PATTERNS.md, Pattern 5

**Description**: Use System.Numerics.Vector for SIMD-accelerated batch computations.

**Pattern** (for automation):
```regex
for\s*\(\s*int\s+i\s*=\s*0;\s*i\s*<\s*\w+\.Length;\s*i\+\+\)\s*\{.*\*=?\s*\w+
```

**DO**:
```csharp
public static void ScalePrices(Span<double> prices, double factor)
{
    var vFactor = new Vector<double>(factor);
    int i = 0;
    for (; i + Vector<double>.Count <= prices.Length; i += Vector<double>.Count)
    {
        var v = new Vector<double>(prices.Slice(i, Vector<double>.Count));
        (v * vFactor).CopyTo(prices.Slice(i));
    }
    for (; i < prices.Length; i++) prices[i] *= factor;
}
```

**DON'T**:
```csharp
for (int i = 0; i < prices.Length; i++)
    prices[i] *= factor; // ÔØî Scalar loop, no SIMD
```

**Fix Suggestion**: Use Vector<T> for data-parallel loops on hot paths.

**Related Rules**: JS-045 (Inlining), JS-046 (Zero Allocation)

---

### Rule JS-048: Avoid Virtual Dispatch on Hot Path

**Category**: Performance  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_PERFORMANCE_PATTERNS.md, Pattern 4

**Description**: Avoid virtual method calls and interface dispatch on the hot path.

**Pattern** (for automation):
```regex
private\s+readonly\s+I\w+\s+\w+;.*\w+\.\w+\(
```

**DO**:
```csharp
// Use sealed class or concrete type in hot path
private readonly ConcreteOrderHandler _handler;

public void OnOrder(Order order) => _handler.Handle(order);
```

**DON'T**:
```csharp
private readonly IOrderHandler _handler; // ÔØî Interface, virtual dispatch

public void OnOrder(Order order) => _handler.Handle(order); // Indirect call
```

**Fix Suggestion**: Replace interface with sealed/concrete type in hot paths, or use delegates.

**Related Rules**: JS-045 (Inlining), JS-049 (Devirtualization)

---

### Rule JS-049: Use Generic Specialization for Devirtualization

**Category**: Performance  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_PERFORMANCE_PATTERNS.md, Pattern 4

**Description**: Use generic type parameters with interface constraints for JIT devirtualization.

**Pattern** (for automation):
```regex
where\s+T\s*:\s*I\w+(?!.*struct)
```

**DO**:
```csharp
public static void Process<THandler>(THandler handler, Order order)
    where THandler : struct, IOrderHandler
{
    handler.Handle(order); // JIT can devirtualize struct interface call
}
```

**DON'T**:
```csharp
public static void Process(IOrderHandler handler, Order order)
{
    handler.Handle(order); // ÔØî Virtual dispatch every call
}
```

**Fix Suggestion**: Add `struct` constraint to generic type for JIT devirtualization.

**Related Rules**: JS-048 (Avoid Virtual), JS-045 (Inlining)

---

### Rule JS-050: Optimize String Operations

**Category**: Performance  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_PERFORMANCE_PATTERNS.md, Pattern 6

**Description**: Avoid string concatenation in loops. Use StringBuilder or string interpolation.

**Pattern** (for automation):
```regex
\+\s*=\s*\"\w+\"|\"\w+\"\s*\+\s*\w+\s*\+\s*\"\w+\"
```

**DO**:
```csharp
public string BuildOrderSummary(IEnumerable<Order> orders)
{
    var sb = new StringBuilder();
    foreach (var o in orders)
        sb.Append(o.Symbol).Append(':').Append(o.Quantity).AppendLine();
    return sb.ToString();
}
```

**DON'T**:
```csharp
var result = "";
foreach (var o in orders)
    result += o.Symbol + ":" + o.Quantity + "\n"; // ÔØî O(n^2) allocations
```

**Fix Suggestion**: Use StringBuilder for loops. Use `$"..."` for single expressions.

**Related Rules**: JS-046 (Zero Allocation), JS-043 (Profile First)

---

## Category: Testing (15 rules)

### Rule JS-051: Use xUnit for All Tests

**Category**: Testing  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_TESTING.md, Pattern 1

**Description**: All tests must use xUnit. NUnit and MSTest are banned.

**Pattern** (for automation):
```regex
\[TestFixture\]|\[TestMethod\]|using\s+NUnit|using\s+Microsoft\.VisualStudio\.TestTools
```

**DO**:
```csharp
using Xunit;

public class OrderTests
{
    [Fact]
    public void SubmitOrder_ValidOrder_ReturnsOrderId() { }
}
```

**DON'T**:
```csharp
using NUnit.Framework; // ÔØî NUnit banned
using Microsoft.VisualStudio.TestTools.UnitTesting; // ÔØî MSTest banned
```

**Fix Suggestion**: Remove NUnit/MSTest. Replace [Test]/[TestMethod] with [Fact]/[Theory].

**Related Rules**: JS-052 (Fact vs Theory), JS-053 (No Skip Without Reason)

---

### Rule JS-052: Use [Fact] for Unit Tests, [Theory] for Parameterized

**Category**: Testing  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_TESTING.md, Pattern 1

**Description**: Use [Fact] for single assertions, [Theory] with [InlineData] for parameterized tests.

**Pattern** (for automation):
```regex
\[Test\]\s*\[TestCase\]|\[Test\]\s+public
```

**DO**:
```csharp
[Theory]
[InlineData("MES", 4, 8)]
[InlineData("MGC", 10, 20)]
public void GetQuickTicks_ReturnsExpected(string name, int t1, int t2) { }
```

**DON'T**:
```csharp
[Test]
[TestCase("MES", 4, 8)] // ÔØî NUnit style
public void GetQuickTicks_ReturnsExpected(string name, int t1, int t2) { }
```

**Fix Suggestion**: Replace [Test]/[TestCase] with [Fact]/[Theory]+[InlineData].

**Related Rules**: JS-051 (xUnit), JS-054 (Arrange-Act-Assert)

---

### Rule JS-053: No Skip Without Documented Reason

**Category**: Testing  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_TESTING.md, Pattern 2

**Description**: Never use [Fact(Skip=...)] without a documented, time-bounded reason.

**Pattern** (for automation):
```regex
\[Fact\(Skip\s*=\s*""[^"]*(TODO|todo|fixme|later)""\)
```

**DO**:
```csharp
[Fact(Skip = "NT8-runtime: requires NinjaTrader host. Covered by SIM gate B190-T1.")]
public void Panel_ConstructWithInstrument_SetsQuickT1() { }
```

**DON'T**:
```csharp
[Fact(Skip = "TODO: fix later")] // ÔØî No reason, no owner
public void Panel_ConstructWithInstrument_SetsQuickT1() { }
```

**Fix Suggestion**: Add DW card reference or specific Skip reason with owner.

**Related Rules**: JS-052 (Fact vs Theory), JS-055 (Test Naming)

---

### Rule JS-054: Follow Arrange-Act-Assert Pattern

**Category**: Testing  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_TESTING.md, Pattern 3

**Description**: Structure all tests using Arrange-Act-Assert (AAA) sections.

**Pattern** (for automation):
```regex
\[Fact\].*public\s+void\s+\w+\(\).*\{(?!.*\/\/\s*Arrange)
```

**DO**:
```csharp
[Fact]
public void SubmitOrder_ValidInput_ReturnsOk()
{
    // Arrange
    var order = new Order("MES", 1);

    // Act
    var result = _engine.SubmitOrder(order);

    // Assert
    Assert.True(result.IsOk);
}
```

**DON'T**:
```csharp
[Fact]
public void Test1()
{
    Assert.True(_engine.SubmitOrder(new Order("MES", 1)).IsOk); // ÔØî No structure
}
```

**Fix Suggestion**: Add // Arrange, // Act, // Assert comments to all test methods.

**Related Rules**: JS-055 (Test Naming), JS-056 (Single Assertion)

---

### Rule JS-055: Use Descriptive Test Names

**Category**: Testing  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_TESTING.md, Pattern 3

**Description**: Name tests as MethodUnderTest_Scenario_ExpectedResult.

**Pattern** (for automation):
```regex
public\s+void\s+Test\d+\(\)|public\s+void\s+\w+Test\(\)
```

**DO**:
```csharp
[Fact]
public void GetQuickTicks_MES_Returns4And8() { }

[Fact]
public void SubmitOrder_NullOrder_ReturnsFalse() { }
```

**DON'T**:
```csharp
[Fact]
public void Test1() { } // ÔØî No description

[Fact]
public void GetQuickTicksTest() { } // ÔØî No scenario or expected
```

**Fix Suggestion**: Rename to MethodName_Scenario_Expected format.

**Related Rules**: JS-054 (AAA), JS-051 (xUnit)

---

### Rule JS-056: One Assert per Test

**Category**: Testing  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_TESTING.md, Pattern 3

**Description**: Each test should assert one logical thing. Multiple asserts allowed only for compound values.

**Pattern** (for automation):
```regex
Assert\.\w+\(.*\);.*\n.*Assert\.\w+\(.*\);.*\n.*Assert\.\w+\(
```

**DO**:
```csharp
[Fact]
public void GetQuickTicks_MES_Returns4And8()
{
    var (t1, t2) = InstrumentDefaults.GetQuickTicks("MES");
    Assert.Equal(4, t1);
    Assert.Equal(8, t2); // Compound value, two asserts is acceptable
}
```

**DON'T**:
```csharp
[Fact]
public void ManyThings()
{
    Assert.Equal(4, GetT1("MES"));
    Assert.Equal(10, GetT1("MGC")); // ÔØî Two scenarios in one test
    Assert.False(_panel.IsEnabled);  // ÔØî Unrelated assertion
}
```

**Fix Suggestion**: Split multi-scenario tests into one [Fact] per scenario.

**Related Rules**: JS-055 (Test Naming), JS-054 (AAA)

---

### Rule JS-057: Use BenchmarkDotNet for Performance Tests

**Category**: Testing  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_TESTING.md, Pattern 4

**Description**: Use BenchmarkDotNet for all performance regression tests.

**Pattern** (for automation):
```regex
Stopwatch\.\w+|Environment\.TickCount(?!.*BenchmarkDotNet)
```

**DO**:
```csharp
[MemoryDiagnoser]
public class TickBenchmarks
{
    [Benchmark]
    public void ProcessOneTick() => _engine.OnTick(1.0, 1.001);
}
```

**DON'T**:
```csharp
var sw = Stopwatch.StartNew();
for (int i = 0; i < 1000000; i++) _engine.OnTick(1.0, 1.001);
sw.Stop();
Console.WriteLine(sw.Elapsed); // ÔØî Manual timing, not reproducible
```

**Fix Suggestion**: Use BenchmarkDotNet [Benchmark] attribute and run with BenchmarkRunner.Run<T>.

**Related Rules**: JS-043 (Profile First), JS-058 (Property-Based Testing)

---

### Rule JS-058: Use Property-Based Testing for Invariants

**Category**: Testing  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_TESTING.md, Pattern 5

**Description**: Use FsCheck or similar for invariant testing across random inputs.

**Pattern** (for automation):
```regex
\[Theory\]\s*\[InlineData\([^)]+\)\]\s*\[InlineData\([^)]+\)\]\s*\[InlineData\([^)]+\)\]\s*\[InlineData\([^)]+\)\]
```

**DO**:
```csharp
[Property]
public bool RoundTrip_Serialize_Deserialize_IsIdentity(Order order)
{
    var bytes = Serialize(order);
    var restored = Deserialize(bytes);
    return restored == order;
}
```

**DON'T**:
```csharp
[InlineData(1)] [InlineData(2)] [InlineData(3)] ... // ÔØî Manually listed cases
```

**Fix Suggestion**: Use FsCheck [Property] attribute for exhaustive invariant testing.

**Related Rules**: JS-057 (BenchmarkDotNet), JS-059 (Seeded Random)

---

### Rule JS-059: Use Seeded Random for Deterministic Tests

**Category**: Testing  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_TESTING.md, Pattern 5

**Description**: Always use seeded Random in tests for reproducible failures.

**Pattern** (for automation):
```regex
new\s+Random\(\)(?!\s*\/\/\s*seeded)
```

**DO**:
```csharp
[Fact]
public void MatchEngine_RandomOrders_NoCross()
{
    var rng = new Random(42); // Seeded -- reproducible
    var orders = Enumerable.Range(0, 100).Select(_ => RandomOrder(rng)).ToList();
    Assert.True(_engine.Process(orders).All(r => r.IsOk));
}
```

**DON'T**:
```csharp
var rng = new Random(); // ÔØî Unseeded, non-reproducible
```

**Fix Suggestion**: Replace `new Random()` with `new Random(fixedSeed)`.

**Related Rules**: JS-058 (Property-Based), JS-060 (Test Isolation)

---

### Rule JS-060: Ensure Test Isolation

**Category**: Testing  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_TESTING.md, Pattern 6

**Description**: Tests must not share mutable state. Each test starts from a known clean state.

**Pattern** (for automation):
```regex
private\s+static\s+\w+\s+_\w+;.*\[Fact\]
```

**DO**:
```csharp
public class OrderTests
{
    private readonly OrderEngine _engine; // Per-test instance

    public OrderTests() => _engine = new OrderEngine(); // Fresh per test

    [Fact] public void Test1() { }
    [Fact] public void Test2() { }
}
```

**DON'T**:
```csharp
private static OrderEngine _engine = new(); // ÔØî Shared static mutable state

[Fact] public void Test1() { _engine.Submit(...); }
[Fact] public void Test2() { _engine.Submit(...); } // State leaks from Test1
```

**Fix Suggestion**: Move shared state to constructor. Use per-test instances.

**Related Rules**: JS-059 (Seeded Random), JS-061 (No External Dependencies)

---

### Rule JS-061: No External Dependencies in Unit Tests

**Category**: Testing  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_TESTING.md, Pattern 7

**Description**: Unit tests must not call the filesystem, network, database, or NT8 host.

**Pattern** (for automation):
```regex
File\.Read|HttpClient\.Get|SqlConnection\.Open(?!.*\/\/\s*integration)
```

**DO**:
```csharp
[Fact]
public void GetQuickTicks_MES_Returns4And8()
{
    // Pure BCL -- no file, network, NT8
    var (t1, t2) = InstrumentDefaults.GetQuickTicks("MES");
    Assert.Equal(4, t1);
}
```

**DON'T**:
```csharp
[Fact]
public void GetQuickTicks_ReadFromDisk() // ÔØî Unit test reads file
{
    var data = File.ReadAllText("config.json");
    ...
}
```

**Fix Suggestion**: Inject dependencies as interfaces. Use in-memory fakes in unit tests.

**Related Rules**: JS-060 (Test Isolation), JS-062 (Fake over Mock)

---

### Rule JS-062: Use Fakes over Mocks

**Category**: Testing  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_TESTING.md, Pattern 8

**Description**: Prefer hand-rolled fakes over mock frameworks (Moq, NSubstitute).

**Pattern** (for automation):
```regex
\.Setup\(|\.Returns\(|\.Verify\(|Mock<\w+>
```

**DO**:
```csharp
private class FakeOrderGateway : IOrderGateway
{
    public List<Order> Submitted { get; } = new();
    public Result<OrderId, string> Submit(Order o)
    {
        Submitted.Add(o);
        return Result<OrderId, string>.Ok(new OrderId(1));
    }
}
```

**DON'T**:
```csharp
var mock = new Mock<IOrderGateway>(); // ÔØî Mock framework
mock.Setup(g => g.Submit(It.IsAny<Order>())).Returns(Result.Ok(1));
```

**Fix Suggestion**: Replace mock with a simple fake implementing the interface.

**Related Rules**: JS-061 (No External Dependencies), JS-060 (Test Isolation)

---

### Rule JS-063: Test Public API Only

**Category**: Testing  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_TESTING.md, Pattern 9

**Description**: Test through public/internal API. Avoid testing private methods directly via reflection.

**Pattern** (for automation):
```regex
\.GetMethod\(.*BindingFlags\.NonPublic.*\)\.Invoke\(
```

**DO**:
```csharp
[Fact]
public void Process_ValidInput_ProducesCorrectOutput()
{
    var result = _engine.Process(input); // Public entry point
    Assert.Equal(expected, result);
}
```

**DON'T**:
```csharp
var method = typeof(Engine).GetMethod("_internalHelper",
    BindingFlags.NonPublic | BindingFlags.Instance);
method.Invoke(_engine, new object[] { input }); // ÔØî Tests implementation detail
```

**Fix Suggestion**: Test private logic through the public method that uses it.

**Related Rules**: JS-060 (Test Isolation), JS-055 (Test Naming)

---

### Rule JS-064: Use InternalsVisibleTo for Internal APIs

**Category**: Testing  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_TESTING.md, Pattern 9

**Description**: Use [InternalsVisibleTo] to expose internal types to test assembly.

**Pattern** (for automation):
```regex
internal\s+class\s+\w+(?!.*InternalsVisibleTo)
```

**DO**:
```csharp
// In AssemblyInfo.cs or csproj:
[assembly: InternalsVisibleTo("PropTraderTools.Tests")]

// In source:
internal static (int t1, int t2) GetQuickTicks(string name) { ... }
```

**DON'T**:
```csharp
public static (int t1, int t2) GetQuickTicks(string name) { ... }
// ÔØî Made public only to enable testing
```

**Fix Suggestion**: Keep internal, use [InternalsVisibleTo] in assembly attributes.

**Related Rules**: JS-063 (Test Public API), JS-060 (Test Isolation)

---

### Rule JS-065: No Console.WriteLine in Tests

**Category**: Testing  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_TESTING.md, Pattern 10

**Description**: Never use Console.WriteLine in test methods. Use xUnit ITestOutputHelper.

**Pattern** (for automation):
```regex
Console\.WriteLine\((?!.*\/\/\s*ok)
```

**DO**:
```csharp
public class OrderTests
{
    private readonly ITestOutputHelper _out;
    public OrderTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void Test() { _out.WriteLine("debug info"); }
}
```

**DON'T**:
```csharp
[Fact]
public void Test()
{
    Console.WriteLine("debug info"); // ÔØî Not captured by xUnit runner
}
```

**Fix Suggestion**: Inject ITestOutputHelper via constructor and use _out.WriteLine.

**Related Rules**: JS-051 (xUnit), JS-054 (AAA)

---

## Category: Code Review (15 rules)

### Rule JS-066: CYC <= 8 Ceiling

**Category**: Code Review  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_CODE_REVIEW.md, Pattern 1

**Description**: Every method must have cyclomatic complexity <= 8 (Jane Street strict standard). Extract helpers when CYC would exceed 8.

**Pattern** (for automation):
```regex
if\s*\(.*\)\s*\{.*if\s*\(.*\)\s*\{.*if\s*\(.*\)\s*\{.*if\s*\(.*\)\s*\{.*if\s*\(.*\)\s*\{.*if\s*\(.*\)\s*\{.*if\s*\(
```

**DO**:
```csharp
// CYC=3 guard chain -- each branch extracted to helpers
public void CopyOrders(Account leader, IReadOnlyList<Account> followers)
{
    if (!_isCopyEnabled) return;
    if (leader == null) return;
    foreach (var follower in followers)
        CopySingle(leader, follower);
}
```

**DON'T**:
```csharp
public void CopyOrders(...)
{
    if (enabled) { if (leader != null) { if (followers != null) { foreach (...) { if (...) { if (...) { if (...) {
    // CYC=8+ ÔØÑÓÒÜ BANNED
    } } } } } } }
}
```

**Fix Suggestion**: Extract branches into helper methods. Each method CYC <= 8.

**Related Rules**: JS-067 (Method Length), JS-068 (Nesting Depth)

---

### Rule JS-067: Method Length <= 40 Lines

**Category**: Code Review  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CODE_REVIEW.md, Pattern 2

**Description**: Methods should not exceed 40 lines. Longer methods hide complexity.

**Pattern** (for automation):
```regex
public\s+\w+\s+\w+\(.*\)\s*\{(.|\n){1600,}\}
```

**DO**:
```csharp
public void ProcessOrder(Order order)
{
    var enriched = Enrich(order);      // Delegates to helper
    var validated = Validate(enriched); // Delegates to helper
    Submit(validated);                  // Delegates to helper
}
```

**DON'T**:
```csharp
public void ProcessOrder(Order order)
{
    // 80 lines of inline logic... ÔØî Too long
}
```

**Fix Suggestion**: Extract logical sections into private helper methods.

**Related Rules**: JS-066 (CYC <= 8), JS-068 (Nesting Depth)

---

### Rule JS-068: Nesting Depth <= 3

**Category**: Code Review  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CODE_REVIEW.md, Pattern 3

**Description**: Never nest blocks more than 3 levels deep. Use early returns to flatten.

**Pattern** (for automation):
```regex
\{[^\{\}]*\{[^\{\}]*\{[^\{\}]*\{[^\{\}]*\{
```

**DO**:
```csharp
public void Process(Order order)
{
    if (order == null) return;       // Guard
    if (!IsValid(order)) return;     // Guard
    Submit(order);                   // Flat
}
```

**DON'T**:
```csharp
if (order != null)
    if (IsValid(order))
        if (HasCapacity())
            if (IsMarketOpen())      // ÔØî 4 levels
                Submit(order);
```

**Fix Suggestion**: Invert conditions to use early returns. Flatten nesting.

**Related Rules**: JS-066 (CYC <= 8), JS-067 (Method Length)

---

### Rule JS-069: Use Guard Clauses

**Category**: Code Review  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CODE_REVIEW.md, Pattern 4

**Description**: Use guard clauses to return early on invalid conditions, not nested if-else.

**Pattern** (for automation):
```regex
if\s*\(\w+\s*!=\s*null\)\s*\{.*\n.*\}.*\n.*else\s*\{
```

**DO**:
```csharp
public void HandleOrder(Order order)
{
    if (order == null) return;
    if (!_isCopyEnabled) return;
    DoWork(order);
}
```

**DON'T**:
```csharp
public void HandleOrder(Order order)
{
    if (order != null)
    {
        if (_isCopyEnabled)
        {
            DoWork(order); // ÔØî Nested instead of guarded
        }
    }
}
```

**Fix Suggestion**: Invert condition, add early return, remove else branch.

**Related Rules**: JS-068 (Nesting Depth), JS-066 (CYC)

---

### Rule JS-070: No Magic Numbers

**Category**: Code Review  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CODE_REVIEW.md, Pattern 5

**Description**: Replace magic numbers with named constants.

**Pattern** (for automation):
```regex
(?<![_A-Za-z])[0-9]{4,}(?![_A-Za-z])
```

**DO**:
```csharp
private const int MaxRetries = 3;
private const double TickSizeMES = 0.25;

return Math.Round(price / TickSizeMES) * TickSizeMES;
```

**DON'T**:
```csharp
return Math.Round(price / 0.25) * 0.25; // ÔØî Magic number, no context
```

**Fix Suggestion**: Extract to named constant with descriptive name.

**Related Rules**: JS-066 (CYC), JS-071 (Self-Documenting Code)

---

### Rule JS-071: Write Self-Documenting Code

**Category**: Code Review  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_CODE_REVIEW.md, Pattern 6

**Description**: Code should read as prose. Comments explain WHY, not WHAT.

**Pattern** (for automation):
```regex
\/\/\s*(Get|Set|Return|Loop|Iterate|Check|Create)\s+\w
```

**DO**:
```csharp
// B190-T1: MGC tick size is 0.1pt; 10 ticks = 1pt target, 20 ticks = 2pt stop.
if (masterName.StartsWith("MGC")) return (10, 20);
```

**DON'T**:
```csharp
// Check if MGC and return values
if (masterName.StartsWith("MGC")) return (10, 20); // ÔØî Comment repeats code
```

**Fix Suggestion**: Remove comments that describe WHAT (obvious from code). Keep WHY comments.

**Related Rules**: JS-070 (No Magic Numbers), JS-072 (XML Docs)

---

### Rule JS-072: XML Documentation for Public APIs

**Category**: Code Review  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CODE_REVIEW.md, Pattern 6

**Description**: All public/internal methods must have XML documentation.

**Pattern** (for automation):
```regex
public\s+\w+\s+\w+\((?!.*\/\/\/)
```

**DO**:
```csharp
/// <summary>Returns quick-tick defaults for the given instrument master name.</summary>
/// <param name="masterName">Instrument master name (e.g. "MES", "MGC").</param>
/// <returns>(t1, t2) tick values for Quick and Quick2t buttons.</returns>
public static (int t1, int t2) GetQuickTicks(string masterName) { ... }
```

**DON'T**:
```csharp
public static (int t1, int t2) GetQuickTicks(string masterName) { ... }
// ÔØî No XML doc
```

**Fix Suggestion**: Add /// <summary>, <param>, <returns> docs to all public/internal members.

**Related Rules**: JS-071 (Self-Documenting), JS-055 (Test Naming)

---

### Rule JS-073: Diff Size < 10k Characters

**Category**: Code Review  
**Severity**: P1 (HIGH)  
**Source**: AGENTS.md, Section 5

**Description**: Pull request diffs must target less than 10,000 characters of source code changes.

**Pattern** (for automation):
```
git diff --stat HEAD | tail -1 | grep -E "[0-9]{4,} deletions"
```

**DO**:
Split large epics into smaller focused PRs. Each PR changes one logical unit.

**DON'T**:
Submit a single PR with 5,000 lines of changes across 20 files.

**Fix Suggestion**: Split the PR. Each PR should address one ticket or one logical concern.

**Related Rules**: JS-074 (Whitespace), JS-075 (Single Responsibility)

---

### Rule JS-074: No Whitespace-Only Mutations

**Category**: Code Review  
**Severity**: P0 (CRITICAL)  
**Source**: AGENTS.md, Section 5

**Description**: Never commit whitespace, indentation, or line-ending changes alongside logic changes.

**Pattern** (for automation):
```regex
^\+\s+$|^\-\s+$
```

**DO**:
Touch only lines with actual logic changes. Leave surrounding indentation unchanged.

**DON'T**:
Re-indent entire files or change CRLF to LF as a side effect of a feature commit.

**Fix Suggestion**: Use `git diff -w` to see changes without whitespace. Revert whitespace-only lines.

**Related Rules**: JS-073 (Diff Size), JS-076 (ASCII Only Commits)

---

### Rule JS-075: Single Responsibility per Method

**Category**: Code Review  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CODE_REVIEW.md, Pattern 7

**Description**: Each method does exactly one thing. Side effects are documented.

**Pattern** (for automation):
```regex
public\s+\w+\s+\w+\(.*\)\s*\{.*\n.*\n.*\n.*\n.*\n.*\n.*\n.*\n.*\n.*\n.*\n.*\n.*\n.*\n.*\n.*\n.*\n.*\n.*\n.*\n.*\n
```

**DO**:
```csharp
public void BuildUI() { /* builds UI only */ }
public void SetInstrument(Instrument i) { /* sets state only */ }
```

**DON'T**:
```csharp
public void BuildAndInitialize() // ÔØî Two responsibilities
{
    BuildUI();
    SetInstrument(_defaultInstrument);
    StartMonitoring(); // Third responsibility
}
```

**Fix Suggestion**: Split multi-responsibility methods. Name each after its single action.

**Related Rules**: JS-067 (Method Length), JS-066 (CYC)

---

### Rule JS-076: ASCII-Only in Commit Messages

**Category**: Code Review  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CODE_REVIEW.md, Pattern 8

**Description**: Commit messages must use ASCII characters only. No emoji, no Unicode arrows.

**Pattern** (for automation):
```regex
[^\x00-\x7F]
```

**DO**:
```
feat(B190-T1): update MGC tick defaults (2,4) -> (10,20)
fix(B190-T3): set _beBuffer default to 0 for exact BE
```

**DON'T**:
```
feat: update MGC defaults -> now 10/20 ticks
```

**Fix Suggestion**: Replace all non-ASCII characters in commit message with ASCII equivalents.

**Related Rules**: JS-074 (No Whitespace), JS-073 (Diff Size)

---

### Rule JS-077: No Dead Code in Commits

**Category**: Code Review  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CODE_REVIEW.md, Pattern 9

**Description**: Never commit commented-out code or unused variables.

**Pattern** (for automation):
```regex
\/\/\s*(private|public|internal|protected)\s+\w+|#\s*if\s+false
```

**DO**:
Delete unused code. If it may return, track it in a DW card.

**DON'T**:
```csharp
// private int _oldValue = 5; // ÔØî Commented-out code
// public void OldMethod() { }
```

**Fix Suggestion**: Delete commented code. Open a DW card if you might need it later.

**Related Rules**: JS-073 (Diff Size), JS-078 (Remove TODOs)

---

### Rule JS-078: No TODOs Without DW Card

**Category**: Code Review  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CODE_REVIEW.md, Pattern 9

**Description**: Every TODO in code must reference a DW card ID.

**Pattern** (for automation):
```regex
\/\/\s*TODO(?!.*DW-)
```

**DO**:
```csharp
// TODO(DW-B183-03): IsLaneEEligible CYC=8 frozen -- address in next Lane E sprint.
```

**DON'T**:
```csharp
// TODO: fix this later
// TODO: optimize
```

**Fix Suggestion**: Create a DW card, reference its ID in the TODO comment.

**Related Rules**: JS-077 (No Dead Code), JS-071 (Self-Documenting)

---

### Rule JS-079: Verify Before Merge

**Category**: Code Review  
**Severity**: P0 (CRITICAL)  
**Source**: AGENTS.md, Section 2

**Description**: All 7 scans must be zero before issuing VERIFY_PASS and merging.

**Pattern** (for automation):
```
All 7 SCAN- checks must return 0 hits or N/A before BUILD_PASS.
```

**DO**:
Run all scans sequentially, report each result. Only merge after all are zero.

**DON'T**:
Issue BUILD_PASS with any scan skipped or showing hits.

**Fix Suggestion**: Fix all scan findings. Re-run scans. Report each result.

**Related Rules**: JS-080 (ptt-sync), JS-066 (CYC)

---

### Rule JS-080: Run ptt-sync After Every .cs Edit

**Category**: Code Review  
**Severity**: P0 (CRITICAL)  
**Source**: AGENTS.md, Section 2

**Description**: Run `powershell -File .\scripts\ptt-sync.ps1` after every .cs file change. Expected: 19 linked, 0 errors.

**Pattern** (for automation):
```
ptt-sync.ps1 exit code != 0 or link count != 19 = VERIFY_FAIL
```

**DO**:
```powershell
powershell -File .\scripts\ptt-sync.ps1
# Expected: 19 linked, 0 errors
```

**DON'T**:
Skip ptt-sync after a .cs edit. Missing hard links break NinjaTrader loading.

**Fix Suggestion**: Always run ptt-sync as the last step of SCAN-07. Confirm 19 linked.

**Related Rules**: JS-079 (Verify Before Merge), JS-073 (Diff Size)

---

## Category: Serialization (15 rules)

### Rule JS-081: ASCII-Only String Literals

**Category**: Serialization  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_SERIALIZATION.md, Pattern 1

**Description**: No Unicode, emoji, or non-ASCII characters in C# string literals. Use ASCII escape sequences if non-ASCII is needed.

**Pattern** (for automation):
```regex
"[^"\x00-\x7F]+"
```

**DO**:
```csharp
// ASCII-only: instrument names, labels, button content
private const string LabelQuick = "Quick";
private const string LabelMgc   = "MGC";
var content = "^"; // ASCII 0x5E, not Unicode up-arrow
```

**DON'T**:
```csharp
var arrow = "↑"; // ÔØî Unicode U+2191 -- BANNED in string literals
var label = "…";  // ÔØî Unicode ellipsis
```

**Fix Suggestion**: Replace Unicode characters with ASCII equivalents or \uXXXX escapes in comments only.

**Related Rules**: JS-082 (No Non-ASCII Identifiers), JS-074 (No Whitespace Mutations)

---

### Rule JS-082: No Non-ASCII Identifiers

**Category**: Serialization  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_SERIALIZATION.md, Pattern 1

**Description**: All identifiers (variables, methods, types) must be ASCII-only.

**Pattern** (for automation):
```regex
[^\x00-\x7F]\w+\s*[=({]
```

**DO**:
```csharp
private double _midPrice;
public void UpdatePrice(double bid, double ask) { }
```

**DON'T**:
```csharp
private double _mittelprice; // OK -- ASCII
private double _preço;       // ÔØî Non-ASCII identifier
```

**Fix Suggestion**: Rename to ASCII equivalent. Use transliteration.

**Related Rules**: JS-081 (ASCII Strings), JS-076 (ASCII Commits)

---

### Rule JS-083: Version All Serialized Messages

**Category**: Serialization  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_SERIALIZATION.md, Pattern 2

**Description**: All serialized message types must include a version field.

**Pattern** (for automation):
```regex
\[MessagePackObject\].*\n.*public\s+\w+\s+\w+(?!.*Version)
```

**DO**:
```csharp
[MessagePackObject]
public sealed record OrderMessage
{
    [Key(0)] public int Version { get; init; } = 1;
    [Key(1)] public int OrderId { get; init; }
}
```

**DON'T**:
```csharp
[MessagePackObject]
public sealed record OrderMessage
{
    [Key(0)] public int OrderId { get; init; } // ÔØî No version field
}
```

**Fix Suggestion**: Add `int Version` as first field in every serialized message.

**Related Rules**: JS-084 (Backward Compat), JS-085 (Checksum)

---

### Rule JS-084: Backward Compatibility for Serialized Types

**Category**: Serialization  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_SERIALIZATION.md, Pattern 3

**Description**: Never remove or reorder fields in serialized types. Add new fields at the end only.

**Pattern** (for automation):
```regex
\[Key\(0\)\].*\n.*\[Key\(1\)\].*delete\s+\[Key
```

**DO**:
```csharp
[MessagePackObject]
public sealed record OrderMessage
{
    [Key(0)] public int Version { get; init; }
    [Key(1)] public int OrderId { get; init; }
    [Key(2)] public string? NewField { get; init; } // Added at end
}
```

**DON'T**:
```csharp
// Removed Key(1) -- ÔØî Breaks deserialization of old messages
[Key(0)] public int Version { get; init; }
[Key(1)] public string Symbol { get; init; } // Was Key(2)
```

**Fix Suggestion**: Only add new [Key(N)] fields at the end. Never reorder or delete.

**Related Rules**: JS-083 (Version Field), JS-085 (Checksum)

---

### Rule JS-085: Add Checksum to Critical Messages

**Category**: Serialization  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_SERIALIZATION.md, Pattern 4

**Description**: Add checksum/CRC to messages where corruption would cause financial loss.

**Pattern** (for automation):
```regex
\[MessagePackObject\].*Order|Trade|Position(?!.*Checksum|.*Crc|.*Hash)
```

**DO**:
```csharp
[MessagePackObject]
public sealed record TradeMessage
{
    [Key(0)] public int Version { get; init; }
    [Key(1)] public double Price { get; init; }
    [Key(2)] public int Quantity { get; init; }
    [Key(3)] public uint Crc32 { get; init; }
}
```

**DON'T**:
```csharp
public sealed record TradeMessage
{
    public double Price { get; init; }
    public int Quantity { get; init; }
    // ÔØî No checksum -- silent corruption undetectable
}
```

**Fix Suggestion**: Add CRC32 or xxHash field. Validate on deserialization.

**Related Rules**: JS-083 (Version), JS-086 (Zero-Copy Deserialization)

---

### Rule JS-086: Use Zero-Copy Deserialization

**Category**: Serialization  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_SERIALIZATION.md, Pattern 5

**Description**: Use MessagePack or similar zero-copy serializer. Never use JSON in hot paths.

**Pattern** (for automation):
```regex
JsonSerializer\.Serialize|JsonConvert\.SerializeObject(?!.*\/\/\s*ok)
```

**DO**:
```csharp
public ReadOnlyMemory<byte> Serialize(OrderMessage msg) =>
    MessagePackSerializer.Serialize(msg, _options);
```

**DON'T**:
```csharp
public string Serialize(OrderMessage msg) =>
    JsonSerializer.Serialize(msg); // ÔØî JSON in hot path, allocates string
```

**Fix Suggestion**: Replace JSON with MessagePack. Use Serialize to ReadOnlyMemory<byte>.

**Related Rules**: JS-085 (Checksum), JS-046 (Zero Allocation)

---

### Rule JS-087: Use Source-Generated Serializers

**Category**: Serialization  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_SERIALIZATION.md, Pattern 6

**Description**: Use source-generated serializers (System.Text.Json source gen or MessagePack codegen) to avoid reflection overhead.

**Pattern** (for automation):
```regex
JsonSerializer\.Serialize<\w+>\((?!.*JsonSerializerContext)
```

**DO**:
```csharp
[JsonSerializable(typeof(OrderMessage))]
public partial class AppJsonContext : JsonSerializerContext { }

// Usage:
JsonSerializer.Serialize(msg, AppJsonContext.Default.OrderMessage);
```

**DON'T**:
```csharp
JsonSerializer.Serialize(msg); // ÔØî Reflection-based, not AOT-safe
```

**Fix Suggestion**: Add [JsonSerializable] source generation context.

**Related Rules**: JS-086 (Zero-Copy), JS-046 (Zero Allocation)

---

### Rule JS-088: Validate on Deserialization

**Category**: Serialization  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_SERIALIZATION.md, Pattern 7

**Description**: Always validate deserialized messages before use. Never trust external data.

**Pattern** (for automation):
```regex
MessagePackSerializer\.Deserialize.*\n(?!.*if\s*\(\w+\.Version|.*\.Validate\()
```

**DO**:
```csharp
public Result<OrderMessage, string> Deserialize(ReadOnlyMemory<byte> data)
{
    var msg = MessagePackSerializer.Deserialize<OrderMessage>(data);
    if (msg.Version != CurrentVersion)
        return Result<OrderMessage, string>.Err($"Version mismatch: {msg.Version}");
    return Result<OrderMessage, string>.Ok(msg);
}
```

**DON'T**:
```csharp
var msg = MessagePackSerializer.Deserialize<OrderMessage>(data); // ÔØî No validation
ProcessOrder(msg.OrderId);
```

**Fix Suggestion**: Add version check and field validation after deserialization.

**Related Rules**: JS-085 (Checksum), JS-083 (Version Field)

---

### Rule JS-089: Use ReadOnlySpan for Parsing

**Category**: Serialization  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_SERIALIZATION.md, Pattern 8

**Description**: Parse wire formats using ReadOnlySpan<byte>, not string or byte[].

**Pattern** (for automation):
```regex
Encoding\.UTF8\.GetString|new\s+string\(.*byte\[\]
```

**DO**:
```csharp
public static bool TryParseSymbol(ReadOnlySpan<byte> data, out SymbolId id)
{
    // Parse without allocating string
    id = new SymbolId(data[0] | (data[1] << 8));
    return true;
}
```

**DON'T**:
```csharp
public static SymbolId ParseSymbol(byte[] data)
{
    var str = Encoding.UTF8.GetString(data); // ÔØî Allocates string
    return SymbolId.Parse(str);
}
```

**Fix Suggestion**: Parse directly from ReadOnlySpan<byte> without string intermediary.

**Related Rules**: JS-036 (Span<T>), JS-086 (Zero-Copy)

---

### Rule JS-090: Endianness-Explicit Wire Format

**Category**: Serialization  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_SERIALIZATION.md, Pattern 9

**Description**: Always specify byte order for wire protocol fields. Use BinaryPrimitives.

**Pattern** (for automation):
```regex
BitConverter\.(ToInt32|ToInt64|ToDouble)(?!.*BigEndian|.*LittleEndian)
```

**DO**:
```csharp
public static long ReadInt64BE(ReadOnlySpan<byte> buf) =>
    BinaryPrimitives.ReadInt64BigEndian(buf);
```

**DON'T**:
```csharp
var value = BitConverter.ToInt64(buf, 0); // ÔØî Platform-dependent endianness
```

**Fix Suggestion**: Replace BitConverter with BinaryPrimitives.ReadXxxBigEndian/LittleEndian.

**Related Rules**: JS-089 (ReadOnlySpan Parsing), JS-042 (MemoryMarshal)

---

### Rule JS-091: Use Discriminated Union for Message Types

**Category**: Serialization  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_SERIALIZATION.md, Pattern 10

**Description**: Use sealed record hierarchy (not enum + switch) for message type discrimination.

**Pattern** (for automation):
```regex
public\s+enum\s+MessageType.*\n.*switch\s*\(\w+\.Type\)
```

**DO**:
```csharp
[Union(0, typeof(OrderMessage))]
[Union(1, typeof(CancelMessage))]
[MessagePackObject]
public abstract record GatewayMessage
{
    public abstract int Version { get; }
}
```

**DON'T**:
```csharp
public enum MessageType { Order, Cancel }
public class Message { public MessageType Type; public object? Payload; } // ÔØî Unsafe union
```

**Fix Suggestion**: Use [Union] attribute with sealed record hierarchy.

**Related Rules**: JS-003 (Sealed Hierarchies), JS-083 (Version Field)

---

### Rule JS-092: Use String Interning for High-Frequency Keys

**Category**: Serialization  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_SERIALIZATION.md, Pattern 11

**Description**: Use string.Intern or symbol tables for frequently repeated string keys.

**Pattern** (for automation):
```regex
ConcurrentDictionary<string,\s*\w+>.*\.TryGetValue\("(?!.*Intern)
```

**DO**:
```csharp
private static readonly string InstrumentMES = string.Intern("MES");

// Or use a symbol table for dynamic keys:
private readonly ConcurrentDictionary<string, int> _symbolTable = new();
public int GetSymbolId(string sym) =>
    _symbolTable.GetOrAdd(sym, _ => _nextId++);
```

**DON'T**:
```csharp
var key = "MES"; // ÔØî New string object every call if computed dynamically
_dict.TryGetValue(symbolName, out var data);
```

**Fix Suggestion**: Intern constant strings. Use integer symbol IDs for hot paths.

**Related Rules**: JS-050 (String Operations), JS-046 (Zero Allocation)

---

### Rule JS-093: No Dynamic Serialization in Production

**Category**: Serialization  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_SERIALIZATION.md, Pattern 12

**Description**: Never use dynamic or object types in production serialization paths.

**Pattern** (for automation):
```regex
dynamic\s+\w+|object\s+\w+\s*=\s*\w+\.Deserialize\(
```

**DO**:
```csharp
var msg = MessagePackSerializer.Deserialize<OrderMessage>(data);
```

**DON'T**:
```csharp
dynamic msg = MessagePackSerializer.Deserialize<dynamic>(data); // ÔØî Dynamic
```

**Fix Suggestion**: Always use strongly-typed generic deserialization.

**Related Rules**: JS-086 (Zero-Copy), JS-003 (Sealed Hierarchies)

---

### Rule JS-094: Idempotent Deserialization

**Category**: Serialization  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_SERIALIZATION.md, Pattern 13

**Description**: Deserializing the same bytes twice must produce equal objects.

**Pattern** (for automation):
```regex
Deserialize.*\n.*Deserialize(?!.*Assert\.Equal)
```

**DO**:
```csharp
[Fact]
public void Deserialize_SameBytes_ProducesEqualObjects()
{
    var msg1 = Deserialize(bytes);
    var msg2 = Deserialize(bytes);
    Assert.Equal(msg1, msg2);
}
```

**DON'T**:
```csharp
// Deserialization mutates shared state -- ÔØî non-idempotent
private static int _callCount;
public OrderMessage Deserialize(byte[] b) { _callCount++; ... }
```

**Fix Suggestion**: Make deserialization pure. No side effects. Use immutable output types.

**Related Rules**: JS-088 (Validate on Deserialization), JS-060 (Test Isolation)

---

### Rule JS-095: Use NodaTime for Timestamps

**Category**: Serialization  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_SERIALIZATION.md, Pattern 14

**Description**: Use DateTime.UtcNow (not DateTime.Now) for timestamps. Prefer NodaTime Instant in domain types.

**Pattern** (for automation):
```regex
DateTime\.Now(?!UTC)
```

**DO**:
```csharp
private DateTime _lastUpdate = DateTime.UtcNow; // UTC always
// Or in domain types:
public Instant Timestamp { get; init; } = SystemClock.Instance.GetCurrentInstant();
```

**DON'T**:
```csharp
private DateTime _lastUpdate = DateTime.Now; // ÔØî Local time -- BANNED in this codebase
```

**Fix Suggestion**: Replace DateTime.Now with DateTime.UtcNow. Use NodaTime Instant in new domain types.

**Related Rules**: JS-083 (Version Field), JS-084 (Backward Compat)

---

## Category: Philosophy (15 rules)

### Rule JS-096: Make Illegal States Unrepresentable

**Category**: Philosophy  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 2

**Description**: Design types so invalid states cannot be represented in the type system.

**Pattern** (for automation):
```regex
if\s*\(\w+\.State\s*==\s*\w+State\.\w+\s*&&\s*\w+\.State\s*!=
```

**DO**:
```csharp
// AuthenticatedConnection can only exist after authentication succeeds
public async ValueTask<AuthenticatedConnection> AuthenticateAsync(
    Connection<Connected> conn, Credentials creds)
{
    await conn.SendAsync(creds);
    return new AuthenticatedConnection(conn); // Only constructed here
}
```

**DON'T**:
```csharp
if (conn.IsConnected && conn.IsAuthenticated) // ÔØî Runtime check for invalid state
{
    // What if IsConnected=false, IsAuthenticated=true?
}
```

**Fix Suggestion**: Use phantom types to make invalid state transitions compile-time errors.

**Related Rules**: JS-016 (Type-Level FSM), JS-003 (Sealed Hierarchies)

---

### Rule JS-097: Prefer Compile-Time Errors over Runtime

**Category**: Philosophy  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 2

**Description**: Prefer designs that surface errors at compile time, not runtime.

**Pattern** (for automation):
```regex
throw\s+new\s+InvalidOperationException\("(?!.*unreachable)
```

**DO**:
```csharp
// Compile error if wrong type used
public void SendOrder<T>(T order) where T : struct, IValidatedOrder { }
```

**DON'T**:
```csharp
public void SendOrder(object order)
{
    if (order is not ValidatedOrder)
        throw new InvalidOperationException("Must be validated"); // ÔØî Runtime
}
```

**Fix Suggestion**: Use generic constraints, sealed types, or phantom types to enforce at compile time.

**Related Rules**: JS-096 (Illegal States), JS-016 (Type-Level FSM)

---

### Rule JS-098: Explicit Over Implicit

**Category**: Philosophy  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 6

**Description**: Make all side effects and control flow explicit. No implicit coercions.

**Pattern** (for automation):
```regex
implicit\s+operator\s+\w+\(\w+
```

**DO**:
```csharp
public static explicit operator double(Price p) => p._value;
public static Price FromDouble(double value) => new Price(value);
```

**DON'T**:
```csharp
public static implicit operator double(Price p) => p._value; // ÔØî Implicit coercion
```

**Fix Suggestion**: Replace implicit operator with explicit operator and named factory.

**Related Rules**: JS-097 (Compile-Time), JS-007 (Newtype Pattern)

---

### Rule JS-099: Separation of Pure and Effectful Code

**Category**: Philosophy  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 5

**Description**: Separate pure computation from effectful operations (I/O, state mutation).

**Pattern** (for automation):
```regex
public\s+\w+\s+\w+\(.*\)\s*\{.*_logger\.|.*File\.Write|.*Console\.Write
```

**DO**:
```csharp
public static OrderDecision Decide(MarketData data) => // Pure
    data.Bid > _threshold ? OrderDecision.Buy : OrderDecision.Hold;

public async Task ExecuteAsync(OrderDecision decision) => // Effectful
    await _gateway.SubmitAsync(decision);
```

**DON'T**:
```csharp
public OrderDecision Decide(MarketData data) // ÔØî Mixed pure + effect
{
    var d = data.Bid > _threshold ? Buy : Hold;
    _logger.Log(d); // Side effect in pure function
    return d;
}
```

**Fix Suggestion**: Extract pure decision logic from effectful execution. Return decisions, apply separately.

**Related Rules**: JS-012 (Bind), JS-014 (LINQ)

---

### Rule JS-100: Minimize Shared Mutable State

**Category**: Philosophy  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 3

**Description**: Avoid shared mutable state. If state must be shared, use atomic primitives.

**Pattern** (for automation):
```regex
private\s+static\s+\w+\s+_\w+;\s*\/\/(?!.*volatile|.*readonly|.*Interlocked)
```

**DO**:
```csharp
private volatile bool _isCopyEnabled;
private static readonly CopyEngine Instance = new CopyEngine();
```

**DON'T**:
```csharp
private static int _orderCount; // ÔØî Shared mutable state, no protection
```

**Fix Suggestion**: Use volatile, Interlocked, or immutable value. Remove static mutable state.

**Related Rules**: JS-021 (No Lock), JS-023 (Atomic Primitives)

---

### Rule JS-101: Composition over Inheritance

**Category**: Philosophy  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 4

**Description**: Prefer composition (has-a) over inheritance (is-a) for code reuse.

**Pattern** (for automation):
```regex
public\s+class\s+\w+\s*:\s*\w+(?!.*sealed|.*record).*\n.*override
```

**DO**:
```csharp
public sealed class OrderProcessor
{
    private readonly IOrderValidator _validator;
    private readonly IOrderGateway _gateway;
    
    public OrderProcessor(IOrderValidator v, IOrderGateway g)
    {
        _validator = v;
        _gateway = g;
    }
}
```

**DON'T**:
```csharp
public class OrderProcessor : BaseProcessor // ÔØî Inheritance for code reuse
{
    override protected void Validate() { }
}
```

**Fix Suggestion**: Use constructor injection and interfaces. Seal classes to prevent inheritance.

**Related Rules**: JS-003 (Sealed Hierarchies), JS-010 (Private Constructors)

---

### Rule JS-102: Dependency Injection Over Service Locator

**Category**: Philosophy  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 6

**Description**: Inject dependencies via constructor. Never use service locator pattern.

**Pattern** (for automation):
```regex
ServiceLocator\.Get<|Container\.Resolve<|DependencyResolver\.
```

**DO**:
```csharp
public sealed class CopyEngine
{
    public CopyEngine(IOrderGateway gateway, ILogger logger)
    {
        _gateway = gateway;
        _logger = logger;
    }
}
```

**DON'T**:
```csharp
public sealed class CopyEngine
{
    private IOrderGateway _gateway = ServiceLocator.Get<IOrderGateway>(); // ÔØî Service locator
}
```

**Fix Suggestion**: Add constructor parameters for dependencies. Remove service locator calls.

**Related Rules**: JS-101 (Composition), JS-010 (Private Constructors)

---

### Rule JS-103: Fail Fast

**Category**: Philosophy  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 8

**Description**: Detect and surface errors immediately. Do not continue in a known-bad state.

**Pattern** (for automation):
```regex
catch\s*\(\w+\)\s*\{\s*\/\/\s*(ignore|swallow|suppress)
```

**DO**:
```csharp
public void Initialize()
{
    if (_config == null) throw new InvalidOperationException("Config required");
    // Validated -- safe to proceed
}
```

**DON'T**:
```csharp
try { Initialize(); }
catch (Exception) { } // ÔØî Swallowed exception, broken state continues
```

**Fix Suggestion**: Remove empty catch blocks. Log and re-throw or surface as Result<T,E>.

**Related Rules**: JS-001 (Result<T,E>), JS-096 (Illegal States)

---

### Rule JS-104: Idempotent Operations

**Category**: Philosophy  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 9

**Description**: Make operations idempotent where possible. Calling twice = same result.

**Pattern** (for automation):
```regex
public\s+void\s+\w+\(.*\)\s*\{(?!.*if.*already|.*_initialized)
```

**DO**:
```csharp
public void EnsureInitialized()
{
    if (_isInitialized) return; // Idempotent guard
    Initialize();
    _isInitialized = true;
}
```

**DON'T**:
```csharp
public void Initialize()
{
    _listeners.Add(new Listener()); // ÔØî Adds duplicate on second call
}
```

**Fix Suggestion**: Add guard to check if already initialized before performing side effects.

**Related Rules**: JS-103 (Fail Fast), JS-094 (Idempotent Deserialization)

---

### Rule JS-105: Immutable by Default

**Category**: Philosophy  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 3

**Description**: Default to immutable. Only make things mutable when mutation is required.

**Pattern** (for automation):
```regex
public\s+\w+\s+\w+\s*\{\s*get;\s*set;\s*\}(?!\s*\/\/\s*mutable\s+required)
```

**DO**:
```csharp
public sealed record Order(int Id, string Symbol, int Qty);
// Mutate via 'with': var updated = order with { Qty = 2 };
```

**DON'T**:
```csharp
public class Order
{
    public int Id { get; set; }    // ÔØî Mutable by default
    public string Symbol { get; set; }
}
```

**Fix Suggestion**: Change classes to records. Replace `set` with `init` or `readonly`.

**Related Rules**: JS-008 (Readonly Structs), JS-011 (With Expressions)

---

### Rule JS-106: Measure Everything Meaningful

**Category**: Philosophy  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_PERFORMANCE_PATTERNS.md, Pattern 3

**Description**: Add telemetry for latency, error rates, and throughput of critical paths.

**Pattern** (for automation):
```regex
public\s+async\s+ValueTask\s+\w+Async\((?!.*Stopwatch|.*_metrics|.*Interlocked)
```

**DO**:
```csharp
public async ValueTask ProcessOrderAsync(Order order)
{
    var sw = Stopwatch.GetTimestamp();
    try
    {
        await _gateway.SubmitAsync(order);
        Interlocked.Increment(ref _successCount);
    }
    finally
    {
        var elapsed = Stopwatch.GetElapsedTime(sw);
        _latencyHistogram.Record(elapsed.TotalMicroseconds);
    }
}
```

**DON'T**:
```csharp
public async ValueTask ProcessOrderAsync(Order order)
{
    await _gateway.SubmitAsync(order); // ÔØî No telemetry
}
```

**Fix Suggestion**: Add latency, error count, and throughput telemetry to critical paths.

**Related Rules**: JS-043 (Profile First), JS-057 (BenchmarkDotNet)

---

### Rule JS-107: Defense in Depth

**Category**: Philosophy  
**Severity**: P1 (HIGH)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 8

**Description**: Multiple independent validation layers. One layer failing must not cause financial loss.

**Pattern** (for automation):
```regex
public\s+void\s+Submit\(\w+\)(?!.*if.*guard|.*validate|.*check)
```

**DO**:
```csharp
public Result<OrderId, string> Submit(Order order)
{
    // Layer 1: type-level validation (compile time)
    // Layer 2: business rule validation
    if (order.Qty <= 0) return Result.Err("Qty must be positive");
    // Layer 3: exchange-level validation (gateway returns error)
    return _gateway.Submit(order);
}
```

**DON'T**:
```csharp
public void Submit(Order order)
{
    _gateway.Submit(order); // ÔØî Single layer, no defense
}
```

**Fix Suggestion**: Add at least 2 independent validation layers before gateway calls.

**Related Rules**: JS-096 (Illegal States), JS-103 (Fail Fast)

---

### Rule JS-108: Observability at Every Layer

**Category**: Philosophy  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 9

**Description**: Every layer of the system must be independently observable.

**Pattern** (for automation):
```regex
catch\s*\(Exception\s+\w+\)\s*\{(?!.*_logger|.*Log|.*Telemetry)
```

**DO**:
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Order submission failed: {OrderId}", order.Id);
    Interlocked.Increment(ref _errorCount);
    throw;
}
```

**DON'T**:
```csharp
catch (Exception) { throw; } // ÔØî Exception propagated but not observed
```

**Fix Suggestion**: Log all caught exceptions with structured context before re-throwing.

**Related Rules**: JS-106 (Measure Everything), JS-103 (Fail Fast)

---

### Rule JS-109: Avoid Premature Abstraction

**Category**: Philosophy  
**Severity**: P2 (MEDIUM)  
**Source**: JANE_STREET_CODE_REVIEW.md, Pattern 10

**Description**: Do not create abstractions until you have at least two concrete implementations.

**Pattern** (for automation):
```regex
public\s+interface\s+I\w+\s*\{(?!.*\/\/\s*(2|3|multiple)\s+impl)
```

**DO**:
```csharp
// We have: FileLogger and CloudLogger -- abstraction justified
public interface ILogger { void Log(string msg, LogLevel level); }
```

**DON'T**:
```csharp
// Only one implementation ever exists
public interface IOrderProcessor { } // ÔØî Unnecessary abstraction
public sealed class OrderProcessor : IOrderProcessor { }
```

**Fix Suggestion**: Delete interface if only one implementation exists. Add back when second is needed.

**Related Rules**: JS-101 (Composition), JS-075 (Single Responsibility)

---

### Rule JS-110: Correctness Over Performance

**Category**: Philosophy  
**Severity**: P0 (CRITICAL)  
**Source**: JANE_STREET_CORE_PATTERNS.md, Pattern 1

**Description**: Never sacrifice correctness for performance. A fast wrong answer is always wrong.

**Pattern** (for automation):
```regex
\/\/\s*(PERF|perf|performance|fast).*skip.*check|\/\/\s*(PERF|perf).*bypass.*valid
```

**DO**:
```csharp
public Result<OrderId, string> Submit(Order order)
{
    var validation = ValidateOrder(order); // Never skip
    if (!validation.IsOk) return validation.Error;
    return _gateway.Submit(order);
}
```

**DON'T**:
```csharp
public OrderId Submit(Order order)
{
    // PERF: skip validation in hot path
    return _gateway.Submit(order); // ÔØî Correctness sacrificed for speed
}
```

**Fix Suggestion**: Keep validation. Profile first. Optimize the validator if needed, not remove it.

**Related Rules**: JS-043 (Profile First), JS-096 (Illegal States), JS-097 (Compile-Time Errors)

---

*Last Updated: 2026-07-11 (B190-T4 restore: JS-042..JS-110 appended)*
