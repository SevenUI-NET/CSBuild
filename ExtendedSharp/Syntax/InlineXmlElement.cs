namespace SevenUI.ExtendedSharp.Syntax;

/// <summary>
/// Represents a parsed XML/JSX element in the Abstract Syntax Tree (AST).
/// </summary>
/// <remarks>
/// This class is produced by <see cref="InlineXmlParser"/> and represents a single JSX/XML element
/// with all its properties and children. It forms the intermediate representation between tokenization
/// and code generation.
/// 
/// Elements can be nested arbitrarily deep, with children being either text nodes, code expressions,
/// or other <see cref="InlineXmlElement"/> instances.
/// </remarks>
/// <example>
/// <code>
/// // For JSX like: &lt;Button onClick={() => doSomething()}&gt;Click me&lt;/Button&gt;
/// // This creates an InlineXmlElement with:
/// // - TagName = "Button"
/// // - CodeProps = { "onClick", "() => doSomething()" }
/// // - Children = { "Click me" }
/// </code>
/// </example>
public class InlineXmlElement
{
    /// <summary>
    /// Gets or sets the name of the element's tag (e.g., "div", "Button", "Loading").
    /// </summary>
    /// <remarks>
    /// This can be either an HTML tag name (lowercase) or a component name (PascalCase).
    /// The tag name is used during code generation to determine whether to create a string literal
    /// for HTML tags or a reference to a component type for custom components.
    /// </remarks>
    /// <value>
    /// The element's tag name. Defaults to an empty string.
    /// </value>
    /// <example>
    /// <code>
    /// element.TagName = "div";      // HTML element
    /// element.TagName = "Button";   // Custom component
    /// </code>
    /// </example>
    public string TagName { get; set; } = "";
    
    /// <summary>
    /// Gets the collection of string-valued properties (attributes) on this element.
    /// </summary>
    /// <remarks>
    /// Properties with string literal values (e.g., className="my-class") are stored here.
    /// The dictionary key is the property name (automatically converted to PascalCase for C#),
    /// and the value is the quoted string from the JSX source.
    /// 
    /// This collection is initialized on construction and cannot be replaced, only populated.
    /// </remarks>
    /// <value>
    /// A dictionary mapping property names to their string values. Never null.
    /// </value>
    /// <example>
    /// <code>
    /// // For: &lt;div className="container" id="main"&gt;
    /// StringProps["ClassName"] = "\"container\"";
    /// StringProps["Id"] = "\"main\"";
    /// </code>
    /// </example>
    public Dictionary<string, string> StringProps { get; } = new();
    
    /// <summary>
    /// Gets the collection of code expression properties on this element.
    /// </summary>
    /// <remarks>
    /// Properties with code block values (e.g., onClick={handleClick}) are stored here.
    /// The dictionary key is the property name (automatically converted to PascalCase for C#),
    /// and the value is the code expression without the surrounding braces.
    /// 
    /// This collection is initialized on construction and cannot be replaced, only populated.
    /// </remarks>
    /// <value>
    /// A dictionary mapping property names to their code expressions. Never null.
    /// </value>
    /// <example>
    /// <code>
    /// // For: &lt;Button onClick={(e) => handleClick(e)} disabled={!isEnabled}&gt;
    /// CodeProps["OnClick"] = "(e) => handleClick(e)";
    /// CodeProps["Disabled"] = "!isEnabled";
    /// </code>
    /// </example>
    public Dictionary<string, string> CodeProps { get; } = new();
    
    /// <summary>
    /// Gets the collection of child nodes for this element.
    /// </summary>
    /// <remarks>
    /// Children can be of three types:
    /// - <see cref="XmlTextNode"/>: Static text content
    /// - <see cref="InlineXmlElement"/>: Nested JSX elements
    /// - <see cref="string"/>: Code expressions from JSX interpolation
    /// 
    /// The parser ensures that closing tags are never added as children.
    /// This collection is initialized on construction and cannot be replaced, only populated.
    /// </remarks>
    /// <value>
    /// A list of child objects (text nodes, elements, or expressions). Never null.
    /// </value>
    /// <example>
    /// <code>
    /// // For: &lt;div&gt;Hello &lt;span&gt;World&lt;/span&gt; {count}&lt;/div&gt;
    /// Children[0] = new XmlTextNode("Hello ");
    /// Children[1] = new InlineXmlElement { TagName = "span", ... };
    /// Children[2] = "count";
    /// </code>
    /// </example>
    public List<object> Children { get; } = new();
}