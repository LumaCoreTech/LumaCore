# LumaCore Engineering Guidelines

The principles, philosophy, and thinking behind how we build software.

---

## Table of Contents

1. [Philosophy](#philosophy)
2. [Design Principles](#design-principles)
   - [SOLID Principles](#solid-principles)
   - [Other Fundamental Principles](#other-fundamental-principles)
   - [Balancing the Principles](#balancing-the-principles)
3. [When to Break the Rules](#when-to-break-the-rules)
4. [Using These Principles in Code Reviews](#using-these-principles-in-code-reviews)
5. [Learning Resources](#learning-resources)

---

**Who should read this**
- New contributors who want to understand how LumaCore thinks about design
- Experienced developers who want to align architecture discussions on shared principles

**How to read it**
- Read it once end-to-end to get the mental model
- Later, jump to specific principles (SRP, OCP, DRY, KISS, YAGNI, …) as a reference

**Where to find concrete rules**
- For naming, formatting, file structure, and other day-to-day rules, see the [Coding Standards](coding-standards.md).

---

## Philosophy

LumaCore's engineering culture is built on four pillars:

1. **Consistency** – Code should look like it was written by one person, even if it wasn't.
2. **Clarity** – The intent of a piece of code should be obvious at a glance.
3. **Documentation** – Important decisions, contracts, and assumptions are written down.
4. **Production Quality** – We build as if the code will run in production tomorrow.

These are not abstract ideals. They are the lens we use when we design APIs, review pull requests, and decide whether something is "good enough".

- **Consistency** means that files follow the same structure, patterns repeat, and surprises are rare.  
  When you open a new feature, you should be able to guess where things live and how they are wired.

- **Clarity** means that names, types, and control flow tell a straightforward story.  
  If you need three paragraphs of explanation for a method, the method is probably too clever.

- **Documentation** means that we do not rely on tribal knowledge.  
  If something is important for future readers, it should exist in code comments, XML documentation, or markdown — not only in someone's head.

- **Production Quality** means we think about failure modes, observability, performance, and upgrade paths.  
  "It works on my machine" is the starting point, not the finish line.

All other guidelines in this document — from SOLID to DRY, KISS, and YAGNI — are tools to support these four pillars.  
If you are in doubt during an implementation or a review, ask:

> *"Does this change improve consistency, clarity, documentation, or production quality — or does it make one of them worse?"*

---

## Design Principles

LumaCore uses a small set of well-known design principles as a **shared language** for design and code reviews.  
They are not laws to obey blindly – they are tools to reason about trade-offs.

At a high level:

- **SOLID** helps us shape types, responsibilities, and dependencies.  
- **DRY, KISS, and YAGNI** help us control duplication and complexity over time.

You will see these names in discussions and pull-request reviews. The goal is not to "collect principles", but to write code that is:

- easy to **understand**,
- safe to **change**,
- and predictable in **behavior**.

When in doubt, we prefer code that is boringly obvious to the next person reading it over code that is clever but fragile.

### SOLID Principles

SOLID is an acronym for five design principles that help create maintainable, flexible, and understandable software.

- **S** – Single Responsibility Principle  
- **O** – Open/Closed Principle  
- **L** – Liskov Substitution Principle  
- **I** – Interface Segregation Principle  
- **D** – Dependency Inversion Principle

#### Single Responsibility Principle (SRP)

> A class should have one clear purpose. When requirements change, only one type of change should affect this class.

```csharp
// ❌ Bad - Multiple responsibilities
public class UserService
{
    public User GetUser(int id) { ... }
    public void SaveUser(User user) { ... }
    public string GenerateReport(User user) { ... }  // Reporting concern
    public void SendEmail(User user, string message) { ... }  // Email concern
}

// ✅ Good - Single responsibility each
public class UserService
{
    public User GetUser(int id) { ... }
    public void SaveUser(User user) { ... }
}

public class UserReportGenerator
{
    public string GenerateReport(User user) { ... }
}

public class EmailService
{
    public void SendEmail(string to, string message) { ... }
}
```

##### Why it matters

SRP is what keeps classes small enough to understand and change safely.  
If one class owns one reason to change, then:

- a new requirement touches **fewer files**,
- tests stay focused instead of mocking half the system,
- version control diffs are small and reviewable,
- and it's easier to see where a bug *could* be hiding.

When a class mixes unrelated concerns (persistence, formatting, emailing, caching …), every change risks breaking something you were not thinking about.  
SRP doesn't demand tiny classes – it demands **coherent ones**.

*See `JwtTokenFactory` in the codebase for a good example of SRP in practice.*

#### Open/Closed Principle (OCP)

> Software entities should be open for extension but closed for modification. Add new behavior by adding new code, not by changing existing code.

```csharp
// ❌ Bad - Must modify existing code for new payment types
public class PaymentProcessor
{
    public void ProcessPayment(Payment payment)
    {
        if (payment.Type == "CreditCard")
        {
            // Credit card logic
        }
        else if (payment.Type == "PayPal")
        {
            // PayPal logic
        }
        else if (payment.Type == "Bitcoin")  // New requirement = modify existing code
        {
            // Bitcoin logic
        }
    }
}

// ✅ Good - Extend by adding new classes
public interface IPaymentProcessor
{
    bool CanProcess(Payment payment);
    void Process(Payment payment);
}

public class CreditCardProcessor : IPaymentProcessor
{
    public bool CanProcess(Payment payment) => payment.Type == "CreditCard";
    public void Process(Payment payment) { /* Credit card logic */ }
}

public class PayPalProcessor : IPaymentProcessor
{
    public bool CanProcess(Payment payment) => payment.Type == "PayPal";
    public void Process(Payment payment) { /* PayPal logic */ }
}

// Adding Bitcoin support = add new class, don't touch existing code
public class BitcoinProcessor : IPaymentProcessor
{
    public bool CanProcess(Payment payment) => payment.Type == "Bitcoin";
    public void Process(Payment payment) { /* Bitcoin logic */ }
}
```

##### Why it matters

OCP keeps existing, working code **stable** while the system grows around it.  
If new behavior can be added by creating new types instead of editing old ones, then:

- you don't accidentally break proven code while adding a new case,
- reviews can focus on the new class instead of re-auditing a big switch statement,
- and different teams can extend a feature without constantly touching the same file.

OCP is not about predicting every future requirement.  
It's about shaping code so that the **most likely** changes become "add here" instead of "surgery in the middle of fragile logic".

#### Liskov Substitution Principle (LSP)

> Subtypes must be usable through their base type interface without the caller knowing the difference. A subtype should honor the contract of its base type – no surprises.

This principle is often violated when inheritance is used incorrectly:

```csharp
// ❌ Bad - Square violates expectations of Rectangle
public class Rectangle
{
    public virtual int Width { get; set; }
    public virtual int Height { get; set; }
    
    public int Area => Width * Height;
}

public class Square : Rectangle
{
    public override int Width
    {
        get => base.Width;
        set { base.Width = value; base.Height = value; }  // Surprise! Also changes Height
    }
    
    public override int Height
    {
        get => base.Height;
        set { base.Height = value; base.Width = value; }  // Surprise! Also changes Width
    }
}

// This code breaks with Square:
void ResizeRectangle(Rectangle rect)
{
    rect.Width = 10;
    rect.Height = 5;
    Debug.Assert(rect.Area == 50);  // Fails with Square! Area is 25 (5×5)
}
```

```csharp
// ✅ Good - Separate types, no misleading inheritance
public interface IShape
{
    int Area { get; }
}

public class Rectangle : IShape
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Area => Width * Height;
}

public class Square : IShape
{
    public int Side { get; set; }
    public int Area => Side * Side;
}
```

**Another common violation – throwing exceptions for "unsupported" operations:**

```csharp
// ❌ Bad - Violates LSP
public interface IRepository<T>
{
    T GetById(int id);
    void Save(T entity);
    void Delete(T entity);
}

public class ReadOnlyRepository<T> : IRepository<T>
{
    public T GetById(int id) { /* works */ }
    public void Save(T entity) => throw new NotSupportedException();  // Surprise!
    public void Delete(T entity) => throw new NotSupportedException();  // Surprise!
}

// ✅ Good - Separate interfaces
public interface IReadRepository<T>
{
    T GetById(int id);
}

public interface IWriteRepository<T>
{
    void Save(T entity);
    void Delete(T entity);
}

public interface IRepository<T> : IReadRepository<T>, IWriteRepository<T> { }
```

> [!NOTE]
> Sometimes this pattern is unavoidable, especially with framework-imposed designs like `Stream` (which uses `CanRead`, `CanWrite`, `CanSeek` to signal capabilities). For your own APIs, prefer smaller, segregated interfaces.

##### Why it matters

LSP is what makes abstractions **trustworthy**.

When you see a variable of type `IShape`, you should not have to remember that "except for Square, which throws here, and SpecialRectangle, which ignores that property".  
If subtypes quietly break expectations:

- code becomes full of `if (x is SpecialCase)` branches,
- unit tests must know internal quirks of each implementation,
- and bugs appear only in specific subtype combinations at runtime.

Honoring LSP means:  
if something is declared as a `BaseType` or `Interface`, it behaves like one – no surprises, no "this works, except when…".  
That trust is what makes polymorphism worth using.

#### Interface Segregation Principle (ISP)

> Clients should not be forced to depend on interfaces they don't use. Prefer many small, specific interfaces over one large, general-purpose interface.

```csharp
// ❌ Bad - Fat interface forces unnecessary implementations
public interface IWorker
{
    void Work();
    void Eat();
    void Sleep();
    void AttendMeeting();
    void WriteReport();
}

public class Robot : IWorker
{
    public void Work() { /* works */ }
    public void Eat() => throw new NotSupportedException();  // Robots don't eat!
    public void Sleep() => throw new NotSupportedException();  // Robots don't sleep!
    public void AttendMeeting() { /* works */ }
    public void WriteReport() { /* works */ }
}

// ✅ Good - Segregated interfaces
public interface IWorkable
{
    void Work();
}

public interface IFeedable
{
    void Eat();
}

public interface ISleepable
{
    void Sleep();
}

public class Human : IWorkable, IFeedable, ISleepable
{
    public void Work() { /* works */ }
    public void Eat() { /* works */ }
    public void Sleep() { /* works */ }
}

public class Robot : IWorkable
{
    public void Work() { /* works */ }
    // No need to implement Eat() or Sleep()
}
```

##### Why it matters

ISP keeps contracts **honest and focused**.

When an interface is too big, implementers pay the price:

- they add dummy methods that throw,
- they depend on things they don't actually need,
- and a small change in the interface forces many classes to be updated.

Smaller, well-focused interfaces make it obvious **what a class actually does**.  
Clients can depend on the minimal surface they need, and changes to one area don't ripple through unrelated code.  
ISP is less about "more interfaces" and more about "no one should pretend to support features they don't really have".

#### Dependency Inversion Principle (DIP)

> High-level modules should not depend on low-level modules. Both should depend on abstractions. Depend on interfaces, not concrete implementations.

```csharp
// ❌ Bad - High-level module depends on low-level details
public class OrderService
{
    private readonly SqlDatabase mDatabase = new SqlDatabase();  // Concrete!
    private readonly SmtpEmailSender mEmailSender = new SmtpEmailSender();  // Concrete!
    
    public void PlaceOrder(Order order)
    {
        mDatabase.Save(order);
        mEmailSender.Send(order.CustomerEmail, "Order confirmed");
    }
}

// Problems:
// - Can't test without real database and SMTP server
// - Can't switch to different database or email provider
// - OrderService knows too much about infrastructure details

// ✅ Good - Depend on abstractions
public class OrderService
{
    private readonly IOrderRepository mRepository;
    private readonly IEmailSender mEmailSender;
    
    public OrderService(IOrderRepository repository, IEmailSender emailSender)
    {
        mRepository = repository;
        mEmailSender = emailSender;
    }
    
    public void PlaceOrder(Order order)
    {
        mRepository.Save(order);
        mEmailSender.Send(order.CustomerEmail, "Order confirmed");
    }
}

// Now we can:
// - Inject mock implementations for testing
// - Switch database providers without touching OrderService
// - Configure different email providers per environment
```

##### Why it matters

DIP keeps **business logic and infrastructure** cleanly separated.

When high-level code (`OrderService`, `PersonaOrchestrator`, …) depends directly on concrete classes (`SqlDatabase`, `SmtpClient`, `HttpClient` wiring), then:

- tests need real infrastructure or heavy mocking,
- swapping a database or email provider becomes a risky refactor,
- and deployment details leak into domain code.

If both sides depend on **abstractions** (`IOrderRepository`, `IEmailSender`), we can:

- test with simple in-memory or fake implementations,
- change infrastructure in one place (composition root),
- and keep domain logic readable and focused.

DIP is what lets LumaCore evolve technically without rewriting the business story.

### Other Fundamental Principles

#### DRY (Don't Repeat Yourself)

> Every piece of knowledge should have a single, authoritative representation in the system. When you copy-paste, you're creating a maintenance burden.

DRY is less about "never write the same line twice" and more about **not duplicating knowledge**.  
If a rule or decision changes, you should know exactly where to go – and ideally, it is only one place.

```csharp
// ❌ Bad - Duplicated validation logic
public class UserController
{
    public IActionResult CreateUser(CreateUserRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || !request.Email.Contains("@"))
            return BadRequest("Invalid email");
        if (request.Password.Length < 8)
            return BadRequest("Password too short");
        // ...
    }
    
    public IActionResult UpdateUser(UpdateUserRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || !request.Email.Contains("@"))
            return BadRequest("Invalid email");  // Same validation, copied!
        if (request.Password.Length < 8)
            return BadRequest("Password too short");  // Same validation, copied!
        // ...
    }
}

// ✅ Good - Single source of truth
public static class ValidationRules
{
    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrEmpty(email) && email.Contains("@");
    
    public static bool IsValidPassword(string password) =>
        password.Length >= 8;
}

// Or better - use DataAnnotations as single source of truth
public class UserRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;
}
```

**DRY is not just about code – it applies to:**
- Configuration (don't hardcode the same value in multiple places)
- Documentation (link to a single source rather than duplicating)
- Database schemas (normalize appropriately)
- API contracts (use OpenAPI as single source of truth)

##### Why it matters

Every duplicate piece of knowledge is a future **bug hotspot**.

When validation rules, magic numbers, or mapping logic are copied into several places:

- fixes have to be made in multiple files,
- one caller inevitably drifts out of sync,
- and no one is sure which copy is the "real" one.

A single, authoritative representation means:

- one change updates all callers,
- behavior stays consistent across the codebase,
- and new contributors quickly see "where truth lives".

DRY is not about golfing code or clever abstractions – it's about **one source of truth** for each important decision.

#### KISS (Keep It Simple, Stupid)

> The simplest solution that works is usually the best. Complexity should be added only when necessary, not preemptively.

KISS reminds us that every extra layer, pattern, or abstraction has a cost.  
We aim for solutions that are simple enough to explain in a few sentences – and only grow complexity when real needs demand it.

A method should do one thing and fit comfortably on a screen. If it's longer than ~25 lines, ask yourself: can this be split into smaller, named steps?

```csharp
// ❌ Bad - Over-engineered for a simple task
public interface IStringReverserStrategy
{
    string Reverse(string input);
}

public class RecursiveStringReverser : IStringReverserStrategy
{
    public string Reverse(string input) =>
        input.Length <= 1 ? input : Reverse(input[1..]) + input[0];
}

public class StringReverserFactory
{
    public IStringReverserStrategy CreateReverser(StringReverserType type) =>
        type switch
        {
            StringReverserType.Recursive => new RecursiveStringReverser(),
            StringReverserType.Iterative => new IterativeStringReverser(),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
}

// ✅ Good - Simple and direct
public static string Reverse(string input) =>
    new string(input.Reverse().ToArray());

// Or even simpler if performance matters:
public static string Reverse(string input)
{
    var chars = input.ToCharArray();
    Array.Reverse(chars);
    return new string(chars);
}
```

**Signs you're violating KISS:**
- You're adding "flexibility" for requirements that don't exist
- It takes more than a minute to explain what a class does
- Readability was sacrificed without clear benefit

##### Why it matters

Complexity is a cost you pay **every time** you touch the code.

Over-engineered solutions:

- are harder to explain,
- hide the core idea in layers of indirection,
- and break in surprising ways when requirements change slightly.

Simple code:

- is easier to read and debug,
- can be refactored when real needs appear,
- and lets new team members contribute without a long initiation ritual.

KISS doesn't mean "naive" or "sloppy".  
It means solving today's problem with the **least** amount of moving parts needed — and resisting the urge to build a framework where a function is enough.

#### YAGNI (You Ain't Gonna Need It)

> Don't implement something until you actually need it. Predicted requirements often turn out to be wrong.

YAGNI is a brake pedal for our imagination.  
Instead of building for hypothetical futures, we implement what is actually required now – and trust ourselves to refactor when real demands appear.

**In practice:** No "maybe"-features. If you can't name a user who needs it *this month*, don't build it.

```csharp
// ❌ Bad - Building for imaginary future requirements
public interface IUserRepository
{
    User GetById(int id);
    User GetByEmail(string email);
    User GetByUsername(string username);
    User GetByPhoneNumber(string phone);  // "We might need this someday"
    IEnumerable<User> GetByDepartment(int deptId);  // "Just in case"
    IEnumerable<User> GetByRole(string role);  // "Could be useful"
    IEnumerable<User> GetByDateRange(DateTime start, DateTime end);  // "For reporting"
    IEnumerable<User> Search(UserSearchCriteria criteria);  // "For advanced search"
}

// ✅ Good - Only what's needed now
public interface IUserRepository
{
    User GetById(int id);
    User GetByEmail(string email);
}

// Add more methods when (and if!) they're actually needed
```

**Signs you're violating YAGNI:**
- "We might need this later"
- "Let's make it configurable just in case"
- "This abstraction will be useful when we add..."
- Building features before they're in the requirements

**The cost of YAGNI violations:**
- Time spent building unused features
- Code complexity for features that may never be used
- Maintenance burden for dead code
- Wrong abstractions based on guessed requirements

##### Why it matters

Speculative features feel like progress, but they create **hidden drag**.

Code written "just in case":

- still needs to be reviewed, tested, and maintained,
- often guesses the wrong abstraction,
- and limits future design choices because "we already built something for that".

By implementing only what is actually needed now:

- you keep the codebase smaller and easier to navigate,
- you design abstractions based on real usage, not imagination,
- and you preserve flexibility for when real requirements appear.

YAGNI is not laziness – it's discipline:  
we invest engineering time in the **problems we have**, not in shadows of possible futures.

### Balancing the Principles

These principles are not meant to be maximized independently.  
They **pull in different directions**, and good design is often about choosing the right trade-off for the situation.

Some typical tensions and how we resolve them in LumaCore:

| Conflict | Resolution |
|----------|------------|
| DRY vs KISS | Don't create complex abstractions just to avoid duplication. Some duplication is acceptable. |
| YAGNI vs OCP | Build for today's requirements, but structure code so it's easy to extend later. |
| SRP vs KISS | Don't split classes so much that you have hundreds of tiny classes. Find the right granularity. |
| DIP vs KISS | Not everything needs an interface. Use abstractions where they provide value (testability, flexibility). |

> [!IMPORTANT]
> **The golden rule:** These are guidelines, not laws. Apply them thoughtfully based on your specific context. The goal is maintainable, understandable code – not perfect adherence to principles.

---

## When to Break the Rules

These guidelines aren't absolute laws. They are strong defaults.

Breaking them is allowed – **as long as it is intentional, justified, and visible**.

Typical reasons to bend or break a guideline:

1. **Interfacing with external code** that has different conventions
2. **Performance critical paths** where conventions add overhead
3. **Generated code** that follows different patterns
4. **The rule doesn't apply** to your specific scenario

When breaking a rule, add a comment explaining why:

```csharp
// Intentionally not using ConfigureAwait(false) here because
// we need to capture the synchronization context for UI updates.
await UpdateUIAsync();
```

---

## Using These Principles in Code Reviews

You don't have to mention every acronym in every review.  
Instead, use the principles as a mental checklist when you look at a change:

- **Responsibility / SRP** – Does this class or method clearly do one thing?
- **OCP** – Will the next obvious change be an extension, or surgery in the middle of existing logic?
- **LSP / ISP** – Do our abstractions behave as their names suggest, without surprises?
- **DIP** – Does high-level code depend on clear abstractions rather than concrete infrastructure?
- **DRY** – Is this decision or rule defined in one authoritative place?
- **KISS** – Is there a simpler way to achieve the same result?
- **YAGNI** – Are we building only what we actually need right now?

If a discussion gets stuck, it often helps to say out loud which principle you're optimizing for.

---

## Learning Resources

### Internal Resources

- [Coding Standards](coding-standards.md) — The rules and conventions
- [Architecture Principles](../architecture/principles.md) — Why LumaCore is structured the way it is
- [Feature Pattern](../architecture/feature-pattern.md) — How features are organized and wired

### External Resources

These resources complement LumaCore's standards and give more background:

- [Microsoft C# Coding Conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/)
- [Async/Await Best Practices](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/)
- [Nullable Reference Types](https://learn.microsoft.com/dotnet/csharp/nullable-references)

---

## Questions or Suggestions?

These guidelines evolve with the project. If you have:
- Questions about a principle
- Suggestions for improvement
- Cases where guidelines don't fit

Open a discussion on GitHub or propose changes via pull request.

**Remember:** Understanding the "why" is more valuable than memorizing the "what".

---

© 2025 LumaCoreTech • MIT License
