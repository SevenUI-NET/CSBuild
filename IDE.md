# IDE Support for CSBuild Inline XML Files

CSBuild uses **inline XML syntax** in `.xcs` files to declare components. While this syntax isn’t natively supported by most IDEs, there are ways to make development smoother.

## Current IDE Behavior

- Most IDEs (like Visual Studio, Rider, or VS Code) do **not recognize inline XML in C# files** by default.
- This means you won’t get full syntax highlighting or intellisense for inline XML nodes or attributes.

## Workarounds

- You can **associate `.xcs` files with C#** in your IDE:
    - In VS Code, for example:  
      Open the command palette → `Change Language Mode` → `C#`
    - In Visual Studio, you can set the default editor for `.xcs` to be the C# editor.
- This allows your IDE to treat the file as C#, so you still get:
    - C# syntax highlighting for expressions in `{...}`
    - Error checking on regular C# code
- You **won’t get full inline XML intellisense** until a dedicated plugin is developed.

## Full Intellisense

- To get true intellisense for inline XML, including element names, attributes, and nested structure:
    - A custom plugin or extension will need to be built for your IDE.
    - Until then, you can rely on:
        - Generated `.g.cs` files for intellisense
        - Regular C# tooling to catch type errors in expressions

## Community Note

- Anyone is welcome to **build IDE plugins or extensions** for `.xcs` files to add full inline XML support.
- Contributions are encouraged! A good plugin could provide:
    - Element and attribute intellisense
    - Syntax highlighting and folding
    - Error checking for inline XML

## Recommendation

- Keep `.xcs` files associated with C# for the best current experience.
- Use the **watcher and generated code** to see how inline XML translates to typed C# calls.
- Treat inline XML blocks as mostly “decorative” in the IDE for now — real type safety comes from the generated `.g.cs` files.
