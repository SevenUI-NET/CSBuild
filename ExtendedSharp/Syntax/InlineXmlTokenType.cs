namespace SevenUI.ExtendedSharp.Syntax;

/// <summary>
/// Enumeration of all token types produced by the JSX tokenizer.
/// </summary>
/// <remarks>
/// These tokens represent the fundamental building blocks of JSX syntax.
/// A sequence of tokens is produced by the tokenizer and then consumed by the parser
/// to build the Abstract Syntax Tree (AST).
/// 
/// The tokenizer maintains context (e.g., whether a tag name is expected) to classify
/// identifiers correctly as either tag names or property names.
/// </remarks>
public enum InlineXmlTokenType
{
    /// <summary>
    /// Opening angle bracket of an opening or self-closing tag: '<'
    /// </summary>
    /// <remarks>
    /// This marks the start of an element tag. Always followed by a <see cref="TagName"/>.
    /// Example: In '<div className="test">', the '<' produces this token.
    /// </remarks>
    OpenTag,

    /// <summary>
    /// Opening sequence of a closing tag: '</'
    /// </summary>
    /// <remarks>
    /// Distinguishes closing tags from opening tags. Always followed by a <see cref="TagName"/>
    /// and then a <see cref="CloseTag"/>.
    /// Example: In '</div>', the '</' produces this token.
    /// </remarks>
    ClosingOpenTag,

    /// <summary>
    /// Closing angle bracket: '>'
    /// </summary>
    /// <remarks>
    /// Ends either an opening tag or a closing tag. Can also be part of a self-closing tag
    /// when preceded by '/'.
    /// Examples: 
    /// - In '<div>', the '>' produces this token
    /// - In '</div>', the '>' produces this token
    /// </remarks>
    CloseTag,

    /// <summary>
    /// Self-closing tag marker: '/>'
    /// </summary>
    /// <remarks>
    /// Indicates an element with no children (self-closing). Used for elements like '<br />' or
    /// components that take only properties.
    /// Example: In '<Loading />', the '/>' produces this token.
    /// </remarks>
    SelfClosingTag,

    /// <summary>
    /// An element tag name: 'div', 'span', 'Button', 'Loading', etc.
    /// </summary>
    /// <remarks>
    /// Can be either an HTML tag (lowercase) or a component name (PascalCase).
    /// Always appears immediately after an <see cref="OpenTag"/> or <see cref="ClosingOpenTag"/>.
    /// Examples:
    /// - In '<div>', the token value is "div"
    /// - In '<Button>', the token value is "Button"
    /// - In '</span>', the token value is "span"
    /// </remarks>
    TagName,

    /// <summary>
    /// A property/attribute name: 'className', 'onClick', 'disabled', etc.
    /// </summary>
    /// <remarks>
    /// Property names appear between the tag name and the closing of the tag, and are followed
    /// by an optional <see cref="Equals"/> and a value (<see cref="StringLiteral"/> or <see cref="CodeBlock"/>).
    /// Examples:
    /// - In '<div className="test">', the token value is "className"
    /// - In '<Button onClick={handleClick}>', the token value is "onClick"
    /// </remarks>
    PropName,

    /// <summary>
    /// Property assignment operator: '='
    /// </summary>
    /// <remarks>
    /// Separates a property name from its value. While not strictly required in some JSX forms,
    /// the tokenizer expects it in standard JSX syntax.
    /// Example: In 'className="test"', the '=' produces this token.
    /// </remarks>
    Equals,

    /// <summary>
    /// A string literal value: '"hello"', ''world'', etc.
    /// </summary>
    /// <remarks>
    /// String values are always quoted (either with single or double quotes).
    /// The token value includes the quotes, preserving them for C# code generation.
    /// Proper escape sequence handling ensures that '\"' and '\\' are preserved.
    /// Examples:
    /// - In 'className="my-class"', the token value is '"my-class"' (with quotes)
    /// - In 'title='Hello World'', the token value is ''Hello World'' (with quotes)
    /// </remarks>
    StringLiteral,

    /// <summary>
    /// A code expression block: '{expression}', '{variable}', '{func()}', etc.
    /// </summary>
    /// <remarks>
    /// Code blocks appear as property values or as child expressions. The token value includes
    /// the surrounding braces.
    /// 
    /// Proper handling ensures:
    /// - Nested braces are respected (e.g., '{obj => ({ x: 1 })}')
    /// - String literals inside code are ignored (e.g., '{x ? "yes" : "no"}')
    /// - Escaped quotes are preserved
    /// 
    /// Examples:
    /// - In 'onClick={handleClick}', the token value is '{handleClick}'
    /// - In '{count}' as a child, the token value is '{count}'
    /// - In 'disabled={!isEnabled}', the token value is '{!isEnabled}'
    /// </remarks>
    CodeBlock,

    /// <summary>
    /// Static text content between tags: 'Hello World', 'Click', etc.
    /// </summary>
    /// <remarks>
    /// Text nodes represent human-readable content that appears between opening and closing tags.
    /// Whitespace is trimmed from the token value.
    /// Example: In '<div>Hello World</div>', the token value is 'Hello World'.
    /// </remarks>
    TextContent
}

/// <summary>
/// Represents a single token produced by the JSX tokenizer.
/// </summary>
/// <remarks>
/// This record represents an atomic unit of JSX syntax. A sequence of these tokens
/// forms the input to the parser, which builds the Abstract Syntax Tree.
/// 
/// The Type field classifies what kind of token this is, and the Value field contains
/// the actual text from the source code (with minimal processing).
/// </remarks>
/// <param name="Type">The classification of this token.</param>
/// <param name="Value">The text content of this token (e.g., the tag name, property name, string value, etc.).</param>
/// <example>
/// <code>
/// // For the JSX: <div className="test">
/// // The tokenizer produces:
/// new InlineXmlToken(InlineXmlTokenType.OpenTag, "<")
/// new InlineXmlToken(InlineXmlTokenType.TagName, "div")
/// new InlineXmlToken(InlineXmlTokenType.PropName, "className")
/// new InlineXmlToken(InlineXmlTokenType.Equals, "=")
/// new InlineXmlToken(InlineXmlTokenType.StringLiteral, "\"test\"")
/// new InlineXmlToken(InlineXmlTokenType.CloseTag, ">")
/// </code>
/// </example>
public record InlineXmlToken(InlineXmlTokenType Type, string Value);