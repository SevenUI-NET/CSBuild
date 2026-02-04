namespace SevenUI.ExtendedSharp.Syntax
{
    /// <summary>
    /// Represents a complete, validated XML/JSX tree structure with all parsed children and metadata.
    /// 
    /// This class serves as a semantic representation of an entire XML element hierarchy after parsing
    /// and validation. It captures not only the element tree itself but also positional information,
    /// nesting depth, and validation state. This allows consumers to understand the structure of the
    /// parsed XML and make decisions about code generation or transformation.
    /// 
    /// The syntax tree is produced by the XML parser and serves as input to transformation engines
    /// like the InlineXmlTransformer. The validation state indicates whether the tree is safe for
    /// code generation without further checks.
    /// 
    /// Typical usage:
    /// 1. Parser produces XmlSyntaxTree from raw XML input
    /// 2. Validation checks mark IsValid = true/false based on syntax rules
    /// 3. Transformers use the tree to generate C# factory method calls
    /// 4. Position metadata enables error reporting back to source locations
    /// </summary>
    public class XmlSyntaxTree
    {
        /// <summary>
        /// Gets or sets the complete original expression that was parsed.
        /// 
        /// This property stores the full raw text of the XML expression as it appeared in the source code,
        /// before any parsing or transformation. This is useful for:
        /// - Preserving source information for error reporting and diagnostics
        /// - Round-tripping (converting back to original syntax)
        /// - Debugging parsing issues by comparing input to output
        /// 
        /// Example: "&lt;Button Text="Click me" IsEnabled={isActive}&gt;Submit&lt;/Button&gt;"
        /// </summary>
        public string FullExpression { get; set; }

        /// <summary>
        /// Gets or sets the parsed and structured JSX/XML content representation.
        /// 
        /// This property contains the semantic representation of the parsed XML structure. Depending on
        /// the parsing strategy, this could be:
        /// - The serialized form of the parsed element tree
        /// - An intermediate representation suitable for transformation
        /// - The extracted content stripped of outer delimiters
        /// 
        /// This is the "meaningful" version of FullExpression after syntax analysis has been performed,
        /// and it's what's typically passed to transformation engines for code generation.
        /// </summary>
        public string JsxContent { get; set; }

        /// <summary>
        /// Gets or sets the character position where this XML expression begins in the source code.
        /// 
        /// This property marks the starting position of the entire XML tree in the original source file.
        /// It's measured as a zero-based character offset from the beginning of the source. This metadata
        /// is essential for:
        /// - Mapping generated code back to source locations (source mapping)
        /// - Generating accurate error messages with line/column information
        /// - Supporting IDE features like "go to definition" and error highlighting
        /// - Enabling range-based selections in editors
        /// 
        /// Example: If XML starts at character 42 in the source, StartPos = 42
        /// </summary>
        public int StartPos { get; set; }

        /// <summary>
        /// Gets or sets the character position where this XML expression ends in the source code.
        /// 
        /// This property marks the ending position (exclusive) of the entire XML tree in the original source file.
        /// It's also measured as a zero-based character offset. Together with StartPos, this enables:
        /// - Determining the span of the XML expression in the source
        /// - Extracting the original text via source[StartPos..EndPos]
        /// - Generating precise error locations and diagnostics
        /// - Supporting editor features that need to operate on the XML region
        /// 
        /// Example: If XML ends at character 156, EndPos = 156
        /// </summary>
        public int EndPos { get; set; }

        /// <summary>
        /// Gets or sets the count of nested XML elements contained within this tree.
        /// 
        /// This property tracks the depth or complexity of the element hierarchy by counting how many
        /// child XML elements (not including text nodes) are nested within the root element.
        /// This metric is useful for:
        /// - Assessing the structural complexity of the parsed expression
        /// - Optimization decisions (e.g., caching or simplification strategies for complex trees)
        /// - Validation rules that may impose limits on nesting depth
        /// - Performance monitoring and analysis of parsing workloads
        /// - Diagnostics and reporting on code structure
        /// 
        /// Note: This typically counts element nodes only, not text nodes or attributes.
        /// Example: &lt;Div&gt;&lt;Button/&gt;&lt;Label/&gt;&lt;/Div&gt; has NestedJsxCount = 2
        /// </summary>
        public int NestedJsxCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this syntax tree passed validation.
        /// 
        /// This property indicates whether the parsed XML structure is valid according to the syntax rules
        /// defined by the parser and validator. A valid tree can be safely transformed into C# code without
        /// additional error checking. Invalid trees may represent:
        /// - Malformed XML (unclosed tags, invalid nesting, etc.)
        /// - Semantic violations (e.g., unsupported attribute types, invalid property assignments)
        /// - Constraint violations (e.g., exceeding nesting depth limits, forbidden combinations)
        /// 
        /// Consumers should check this flag before attempting transformation. If IsValid = false, the tree
        /// should be inspected further or rejected depending on the use case.
        /// 
        /// Typical workflow:
        /// - IsValid = true  → Safe to transform and generate code
        /// - IsValid = false → Report validation errors; do not transform
        /// </summary>
        public bool IsValid { get; set; }
    }
    
}