# CSBuild Documentation

## What is CSBuild?

CSBuild is a code generation tool that transforms declarative **inline XML component syntax** into strongly-typed C# factory method calls. It bridges the gap between intuitive, maintainable UI declarations and the verbose factory patterns required by modern C# frameworks.

Instead of writing chain after chain of factory method calls, you write clean, expressive **inline XML markup** that mirrors your UI structure.

---

## Installation

```bash
# Install globally
dotnet tool install --global CSBuild

# Or install locally
dotnet tool install CSBuild
````

---

## Quick Start

### 1. Write Components with Inline XML

Create `.xcs` files in your project using **inline XML-style component code**:

```csharp
namespace MyApp.Components;

public static class UserProfile
{
    public static Node Render(User user)
    {
        return (
            <div className="profile">
                <h1>{user.Name}</h1>
                <p>{user.Bio}</p>
                <button onClick={() => HandleClick()}>
                    Edit Profile
                </button>
            </div>
        );
    }
}
```

### 2. Run the Watcher

```bash
csbuild --watch
```

Keep this terminal open while developing. CSBuild monitors your project and **automatically regenerates C# code whenever you save a file**.

### 3. Generated Code is Ready to Use

CSBuild outputs a `.g.cs` file in your **project root `Generated/` directory** with fully-typed, production-ready C# code.

---

## Why CSBuild Uses `(<` to Identify Inline XML

CSBuild detects **inline XML expressions** by looking for the `(<` pattern in your C# code. This design choice was made for several reasons:

1. **Simplicity in Parsing**

    * `(<` is easy to identify programmatically.
    * Without it, distinguishing inline XML from normal C# code would require complex heuristics.

2. **Invalid Outside Context**

    * `(<` is otherwise invalid syntax in C#.
    * Whenever CSBuild sees it, it can be confident that an inline XML block follows.

3. **Keeps XML Structured and Readable**

    * Wrapping the inline XML in `()` ensures **well-indented, nicely structured markup**, even for multi-line or nested components.

---

## Pure Components and State

All components should be **pure by default**:

* **Pure by nature** – same inputs always produce the same outputs.
* **State is allowed** – components can maintain local state (tasks, cached values, etc.), but side effects should be controlled.

### Async State Example

Use a pattern like this for async tasks:

```csharp
var task = UseAsync(() => LoadUserAsync(userId));

if (!task.IsCompleted)
{
    return <Loading />;
}

var user = task.Result;

return (
    <div className="profile">
        <h1>{user.Name}</h1>
        <p>{user.Bio}</p>
        <button onClick={() => EditUser(user.Id)}>Edit Profile</button>
    </div>
);
```

* `UseAsync` wraps an asynchronous operation.
* `task.IsCompleted` lets you show a loading state **without capturing async state mid-render**.
* The component remains pure; its output depends only on the resolved task and props.

---

## Transformation Process

1. **Extraction** – CSBuild scans your code for `(<` patterns to find inline XML expressions.
2. **Tokenization** – Breaks inline XML into semantic units: tags, attributes, text nodes, and code expressions.
3. **Parsing** – Builds an AST representing element hierarchy with properties and children.
4. **Code Generation** – Transforms AST into fully-typed factory calls (`Document.CreateElement(...)`).
5. **Replacement** – Original inline XML expressions are replaced with generated C# code.

---

## Watch Mode: Live Development

### Why Keep the Watcher Running?

CSBuild's watch mode is **essential for rapid development**. When enabled:

✅ **Instant Feedback** – Save a `.xcs` file → see generated C# code within milliseconds

✅ **Type Safety** – Generated code is fully typed and validated by your IDE

✅ **Live Updates** – Modify components and see changes immediately

✅ **Error Reporting** – Syntax errors are caught and reported instantly

### Running Watch Mode

```bash
csbuild --watch
```

This starts a file system watcher that:

* Monitors all `.xcs` files in your project
* Detects changes (with 500ms debounce)
* Automatically regenerates `.g.cs` files in `ProjectRoot/Generated/`
* Reports successes and errors to the console

### One-Time Build (Without Watch)

```bash
csbuild
```

Processes all `.xcs` files and exits. Useful for CI/CD pipelines.

---

## Transformation Features

### Mixed Content (Text + Code Expressions)

```csharp
<p>Hello {user.Name}, you have {messageCount} messages</p>
```

Transforms to:

```csharp
"Hello",
user.Name,
", you have",
messageCount,
"messages"
```

### Self-Closing Components

```csharp
<Loading />
<hr />
<MyButton />
```

### Code Expressions in Attributes

```csharp
<div id={"user-" + user.Id} className={isActive ? "active" : "inactive"} />
```

### Nested Components and LINQ

```csharp
{items.Select(item => (
    <Card title={item.Title}>
        <p>{item.Description}</p>
    </Card>
))}

{condition && (
    <AlertBox>
        Warning: This action cannot be undone
    </AlertBox>
)}
```

---

## Benefits of Declarative Inline XML

1. **Readability** – Visual structure mirrors the DOM.
2. **Maintainability** – Modify structure directly without worrying about nested factory calls.
3. **Type Safety** – Props are fully typed; IDE validation.
4. **Consistency** – All components follow the same pattern.
5. **Productivity** – Less boilerplate, faster iteration.
6. **Reusability** – Components nest naturally, supporting higher-order components.

---

## File Organization

CSBuild respects your project structure, and it’s best to keep **each component in its own folder**, alongside any assets or styles it needs:

```
MyProject/
  Components/
    UserCard/
      UserCard.xcs           → Component source
      UserCard.css           → Component-specific styles
      Avatar.png             → Component assets
    Dashboard/
      Dashboard.xcs
      Dashboard.css
      Charts.json
  Pages/
    HomePage/
      HomePage.xcs
```

> All generated `.g.cs` files are output in **ProjectRoot/Generated/**, preserving folder hierarchy from the source components:

```
ProjectRoot/Generated/
  Components/
    UserCard.g.cs
    Dashboard.g.cs
  Pages/
    HomePage.g.cs
```

### Benefits of One Folder per Component

1. **Self-contained** – All related assets, styles, and helper files are together.
2. **Easier maintenance** – No searching across the project for related files.
3. **Simplifies packaging** – Components can be moved, copied, or reused easily.

---

## Configuration

### Custom Factory Methods

```bash
csbuild --watch --factory MyFramework --create-element Build
```

Generates:

```csharp
MyFramework.Build("div", props, children)
```

### Default Configuration

By default, CSBuild generates:

```csharp
Document.CreateElement(...)
```

---

## Workflow

1. **Start the Watcher**

```bash
csbuild --watch
```

2. **Create/Edit Components** – Write inline XML in `.xcs` files.
3. **CSBuild Transforms Automatically** – `.g.cs` files are regenerated in `Generated/`.
4. **Use Generated Code** – IDE intellisense and type checking available.
5. **Repeat** – Modify, save, and see instant updates.

---

## Best Practices

1. Keep `.xcs` files focused – one component per folder.
2. Use inline XML for structure, C# for logic.
3. Name components meaningfully – filename maps to class name.
4. Commit `.xcs` files to version control – generated files can be ignored.
5. Monitor watcher output for errors.

---

## Troubleshooting

### Generated Code Not Updating

* Ensure the watcher is running (`csbuild --watch`)
* Verify the file was saved
* Check the watcher terminal for errors

### Type Errors

* Validate prop names
* Ensure code expressions are valid C#

### Syntax Errors in Inline XML

* Properly close tags (`<div>...</div>`)
* Correct attribute syntax (`prop="value"` or `prop={expression}`)
* Ensure expressions in `{...}` are valid C#

---

## Summary

CSBuild bridges the gap between intuitive UI declaration and type-safe C# code generation. Using **inline XML** makes your components readable, maintainable, and type-safe, while the watcher ensures instant feedback during development.

All components are pure by default, with state allowed via `UseAsync` or other local state patterns. Generated code lives in `ProjectRoot/Generated/`, and component source files remain self-contained in their own folders for easy management.

Happy building! 