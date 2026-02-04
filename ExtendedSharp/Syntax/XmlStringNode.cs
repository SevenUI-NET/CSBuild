namespace SevenUI.ExtendedSharp.Syntax
{
	/// <summary>
	/// Represents a text node in parsed XML/inline element syntax.
	/// 
	/// This class wraps literal text content that appears directly within XML elements.
	/// Text nodes are distinct from element nodes and serve as a container for string content
	/// that should be rendered or processed as-is during code generation.
	/// 
	/// During the transformation process, text nodes are converted into XmlTextNode() constructor
	/// calls in the generated C# code, with the text properly escaped to handle special characters.
	/// 
	/// Example: The content "Hello World" in &lt;Div&gt;Hello World&lt;/Div&gt; 
	/// is represented as an XmlTextNode instance with Text = "Hello World".
	/// </summary>
	public class XmlTextNode(string text)
	{
		/// <summary>
		/// Gets the literal text content of this node.
		/// 
		/// This property holds the raw string content that appears within XML elements.
		/// The text is stored as-is during parsing and is escaped later during code generation
		/// to ensure special characters (quotes, newlines, etc.) are properly handled in the
		/// generated C# output.
		/// 
		/// This property is read-only and initialized via the primary constructor parameter,
		/// ensuring immutability once the node is created.
		/// </summary>
		/// <value>The literal text content of this XML text node.</value>
		public string Text { get; } = text;
	}
}