# CSBuild

CSBuild is a **C# code generation tool** that turns **inline XML component syntax** into fully-typed, production-ready C# factory method calls. It makes building UI components fast, readable, and type-safe.

---

## Features

- **Inline XML Syntax**  
  Write UI components using a clean, XML-like structure directly in C#.  
  Example:
  ```csharp
  return (
      <div className="profile">
          <h1>{user.Name}</h1>
          <button onClick={() => EditUser(user.Id)}>Edit Profile</button>
      </div>
  );
  ```

* **Pure Components by Default**
  Components are pure: same inputs produce the same outputs. Local state and async tasks are supported without side effects.

* **Async-Friendly**
  Handle asynchronous operations safely:

  ```csharp
  var task = UseAsync(() => LoadUserAsync(userId));

  if (!task.IsCompleted)
  {
      return <Loading />;
  }

  var user = task.Result;
  ```

* **Automatic Code Generation**
  Generates fully-typed C# in `ProjectRoot/Generated/` automatically whenever you save `.xcs` files.

* **Watch Mode**
  Live development support: CSBuild watches your project and regenerates code instantly.

* **Nested Components & LINQ Support**
  Compose components naturally and use LINQ operations directly:

  ```csharp
  {items.Select(item => (
      <Card title={item.Title}>
          <p>{item.Description}</p>
      </Card>
  ))}
  ```

* **Consistent, Readable Output**
  Generated C# uses factory methods (`Document.CreateElement`) with strong typing and consistent patterns.

* **Self-Contained Component Folders**
  Each component can live in its own folder with associated styles, assets, and logic.

* **Extensible Factory Methods**
  Supports custom frameworks:

  ```bash
  csbuild --watch --factory MyFramework --create-element Build
  ```

  Produces:

  ```csharp
  MyFramework.Build("div", props, children)
  ```

* **Mixed Content Support**
  Combine text and C# expressions seamlessly:

  ```csharp
  <p>Hello {user.Name}, you have {messageCount} messages</p>
  ```

---

## Benefits

* **Readable & Maintainable** – Inline XML mirrors the UI structure.
* **Type-Safe** – Prop objects are fully typed and validated by the compiler.
* **Productive** – Less boilerplate, faster iteration, fewer errors.
* **Reusable** – Components naturally nest and compose.

---

## Getting Started

1. Install CSBuild:

   ```bash
   dotnet tool install --global CSBuild
   ```

2. Create a component `.xcs` file using inline XML.

3. Start the watcher:

   ```bash
   csbuild --watch
   ```

4. Edit your components and watch CSBuild generate C# code in `ProjectRoot/Generated/`.

---

## IDE Support

* `.xcs` files can be treated as C# in most IDEs to get basic syntax highlighting.
* Full inline XML intellisense requires a custom plugin (anyone is welcome to build one!).

---

CSBuild is designed to make building UI components **fast, type-safe, and maintainable** while keeping your code clean and readable.

Happy building!