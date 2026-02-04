using System.Text;

namespace SevenUI.ExtendedSharp.Syntax
{
    /// <summary>
    /// Parses a flat list of tokens into a hierarchical Abstract Syntax Tree (AST) of <see cref="InlineXmlElement"/> nodes.
    /// </summary>
    /// <remarks>
    /// This is a recursive descent parser that consumes tokens sequentially, building up the element structure.
    /// It properly handles:
    /// - Nested elements and components
    /// - Both string literal and code expression properties
    /// - Text content and code interpolation in children
    /// - Self-closing tags (e.g., &lt;br /&gt;)
    /// - Proper matching of opening and closing tags
    /// 
    /// The parser maintains a position reference that is advanced as tokens are consumed, allowing
    /// recursive calls to continue parsing from where the parent left off.
    /// </remarks>
    public static class InlineXmlParser
    {
        /// <summary>
        /// Parses a sequence of tokens starting at the current position and returns the root element.
        /// </summary>
        /// <param name="tokens">The list of tokens produced by the tokenizer.</param>
        /// <param name="pos">
        /// A reference to the current position in the token list. This is advanced as tokens are consumed.
        /// On entry, should point to an <see cref="InlineXmlTokenType.OpenTag"/> token.
        /// On exit, points to the token after the element's closing tag.
        /// </param>
        /// <returns>A fully parsed <see cref="InlineXmlElement"/> with all properties and children populated.</returns>
        /// <exception cref="Exception">Thrown when the token stream is malformed (e.g., missing tag name, unmatched tags).</exception>
        /// <example>
        /// <code>
        /// var tokens = tokenizer.Tokenize(jsxString);
        /// int pos = 0;
        /// var element = InlineXmlParser.Parse(tokens, ref pos);
        /// </code>
        /// </example>
        public static InlineXmlElement Parse(List<InlineXmlToken> tokens, ref int pos)
        {
            // Verify we're starting with an opening tag and have a token to read
            if (pos >= tokens.Count || tokens[pos].Type != InlineXmlTokenType.OpenTag)
                throw new Exception("Expected OpenTag at start of element");

            pos++; // skip '<' - move to the tag name
            
            // === PARSE TAG NAME ===
            // The tag name must immediately follow the opening tag
            if (pos >= tokens.Count || tokens[pos].Type != InlineXmlTokenType.TagName)
                throw new Exception("Expected tag name after <");

            var element = new InlineXmlElement();
            element.TagName = tokens[pos].Value;
            pos++; // advance past tag name

            // === PARSE PROPERTIES ===
            // Properties can be either:
            // - propName="string value"
            // - propName={codeExpression}
            // We continue until we hit either '>' (opening tag end) or '/>' (self-closing tag)
            while (pos < tokens.Count &&
                   tokens[pos].Type != InlineXmlTokenType.CloseTag &&
                   tokens[pos].Type != InlineXmlTokenType.SelfClosingTag)
            {
                // Only process property names; skip unexpected tokens
                if (tokens[pos].Type != InlineXmlTokenType.PropName)
                {
                    pos++;
                    continue;
                }

                string propName = tokens[pos].Value;
                pos++;

                // The '=' is optional in some JSX forms, but we expect it
                if (pos < tokens.Count && tokens[pos].Type == InlineXmlTokenType.Equals)
                    pos++;

                // Process the property value (string or code block)
                if (pos < tokens.Count)
                {
                    var valueToken = tokens[pos];

                    if (valueToken.Type == InlineXmlTokenType.StringLiteral)
                    {
                        // String properties store the quoted value as-is (quotes included)
                        // e.g., "\"hello\"" stays as-is for C# code generation
                        element.StringProps[propName] = valueToken.Value;
                        pos++;
                    }
                    else if (valueToken.Type == InlineXmlTokenType.CodeBlock)
                    {
                        // Code block properties have their braces stripped
                        // Input: "{handleClick}" -> Output: "handleClick"
                        var codeValue = valueToken.Value.Trim();
                        if (codeValue.StartsWith("{") && codeValue.EndsWith("}"))
                            codeValue = codeValue.Substring(1, codeValue.Length - 2);
                        
                        element.CodeProps[propName] = codeValue.Trim();
                        pos++;
                    }
                }
            }

            // === HANDLE SELF-CLOSING TAGS ===
            // Tags like <br /> or <Loading /> end with '/>' and have no children
            if (pos < tokens.Count && tokens[pos].Type == InlineXmlTokenType.SelfClosingTag)
            {
                pos++; // skip '/>'
                return element; // Return early - no children to parse
            }

            // === SKIP OPENING TAG CLOSE ===
            // Move past the '>' that closes the opening tag (e.g., in <div> ... </div>)
            if (pos < tokens.Count && tokens[pos].Type == InlineXmlTokenType.CloseTag)
                pos++;

            // === PARSE CHILDREN ===
            // Children can be:
            // - Text content (static strings)
            // - Code expressions ({variable}, {function()}, etc.)
            // - Nested elements
            // We continue until we find the closing tag that matches our element
            while (pos < tokens.Count)
            {
                var token = tokens[pos];

                // === CHECK FOR CLOSING TAG ===
                // Closing tags are in the form: </ TagName >
                // We must match the tag name to ensure we don't prematurely close a parent element
                if (token.Type == InlineXmlTokenType.ClosingOpenTag)
                {
                    // Look ahead: next token should be the tag name
                    if (pos + 1 < tokens.Count &&
                        tokens[pos + 1].Type == InlineXmlTokenType.TagName &&
                        tokens[pos + 1].Value == element.TagName)
                    {
                        // Verify the closing '>' exists
                        if (pos + 2 < tokens.Count &&
                            tokens[pos + 2].Type == InlineXmlTokenType.CloseTag)
                        {
                            // Found our closing tag - skip all three tokens (</ TagName >)
                            pos += 3;
                            break; // Exit the children parsing loop
                        }
                    }
                }

                switch (token.Type)
                {
                    case InlineXmlTokenType.TextContent:
                        // Add static text as an XmlTextNode child
                        element.Children.Add(new XmlTextNode(token.Value));
                        pos++;
                        break;

                    case InlineXmlTokenType.CodeBlock:
                        // Code expressions in children (e.g., {count}, {user.name})
                        // Strip the surrounding braces and add as a string child
                        var trimmed = token.Value.Trim();
                        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                            trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
                        
                        if (!string.IsNullOrEmpty(trimmed))
                            element.Children.Add(trimmed);
                        pos++;
                        break;

                    case InlineXmlTokenType.OpenTag:
                        // Recursively parse nested elements
                        // The recursive call will advance pos to after the nested element
                        var child = Parse(tokens, ref pos);
                        element.Children.Add(child);
                        break;

                    default:
                        // Skip any unexpected tokens (shouldn't happen with correct tokenizer)
                        pos++;
                        break;
                }
            }

            return element;
        }
    }
}