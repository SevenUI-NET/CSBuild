namespace SevenUI.ExtendedSharp.Configuration;

/// <summary>
/// Configuration settings for the ExtendedSharp JSX-to-C# code generator.
/// </summary>
/// <remarks>
/// This struct defines how the preprocessor transforms JSX/XML syntax into C# factory calls.
/// It allows customization of the target UI framework's factory methods and naming conventions.
/// </remarks>
public struct ExtendedSharpConfig()
{
    /// <summary>
    /// Gets or sets the name of the factory class or namespace that contains element creation methods.
    /// </summary>
    /// <remarks>
    /// This value is prepended to the <see cref="CreateElementName"/> and <see cref="CreateTextName"/>
    /// methods when generating code. It represents the entry point for all code generation calls.
    /// For example, with Factory = "Solid" and CreateElementName = "CreateElement", generated code
    /// will call Solid.CreateElement(...).
    /// 
    /// Default value is "Document", suitable for DOM-based frameworks and web APIs.
    /// </remarks>
    /// <example>
    /// <code>
    /// var config = new ExtendedSharpConfig { Factory = "Solid" };
    /// // Generated code: Solid.CreateElement("div", props, children)
    /// </code>
    /// </example>
    public string Factory = "Document";
    
    /// <summary>
    /// Gets or sets the name of the method used to create elements in the target framework.
    /// </summary>
    /// <remarks>
    /// This method name is called on the <see cref="Factory"/> class when transforming JSX/XML elements.
    /// The method should accept:
    /// 1. A tag name (string for HTML elements, type reference for custom components)
    /// 2. A props object (properties object with attributes converted to PascalCase properties)
    /// 3. Variable arguments for child elements and text nodes (optional)
    /// 
    /// The method returns an element or node suitable for the target framework.
    /// Default value is "CreateElement", matching common frameworks like React and Solid.js.
    /// </remarks>
    /// <example>
    /// <code>
    /// var config = new ExtendedSharpConfig { CreateElementName = "h" };
    /// // Generated code: Document.h("div", props, children)
    /// 
    /// // Full example with attributes and children:
    /// // &lt;div className="container"&gt;
    /// //     &lt;h1&gt;Title&lt;/h1&gt;
    /// // &lt;/div&gt;
    /// // becomes:
    /// // Document.CreateElement("div", new HtmlDivProps { ClassName = "container" },
    /// //     Document.CreateElement("h1", new HtmlH1Props { },
    /// //         Document.CreateText("Title")
    /// //     )
    /// // )
    /// </code>
    /// </example>
    public string CreateElementName = "CreateElement";

    /// <summary>
    /// Gets or sets the name of the method used to create text nodes in the target framework.
    /// </summary>
    /// <remarks>
    /// This method name is called on the <see cref="Factory"/> class when transforming text content
    /// within JSX/XML elements. The method should accept a string containing the text content
    /// and return an element or node suitable for the target framework.
    /// 
    /// Text nodes are generated for static string content and interpolated text expressions found
    /// within element bodies. For example, the text "Submit" in &lt;button&gt;Submit&lt;/button&gt;
    /// is transformed to a call to this method.
    /// 
    /// The method is also used indirectly through the <see cref="XmlTextNode"/> class in the
    /// parsing pipeline, where text content is first wrapped in XmlTextNode instances and then
    /// transformed into CreateText calls during code generation.
    /// 
    /// Default value is "CreateText", matching common text node creation patterns in modern
    /// UI frameworks.
    /// </remarks>
    /// <example>
    /// <code>
    /// var config = new ExtendedSharpConfig { CreateTextName = "Text" };
    /// // Generated code for text nodes: Document.Text("Submit")
    /// 
    /// // In context with other elements:
    /// // &lt;button&gt;Submit&lt;/button&gt;
    /// // becomes:
    /// // Document.CreateElement("button", new HtmlButtonProps { },
    /// //     Document.Text("Submit")
    /// // )
    /// 
    /// // With code expressions:
    /// // &lt;p&gt;Hello {userName}&lt;/p&gt;
    /// // becomes:
    /// // Document.CreateElement("p", new HtmlPProps { },
    /// //     Document.Text("Hello " + userName)
    /// // )
    /// </code>
    /// </example>
    public string CreateTextName = "CreateText";
}