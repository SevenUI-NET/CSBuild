using System.Text;
using SevenUI.ExtendedSharp.Configuration;

namespace SevenUI.ExtendedSharp.Syntax
{
    /// <summary>
    /// Preprocesses source code to find, extract, and transform JSX/XML expressions into C# factory calls.
    /// </summary>
    /// <remarks>
    /// The preprocessor operates in a pipeline:
    /// 1. Extract JSX expressions from source code using a single universal rule
    /// 2. Tokenize each JSX expression
    /// 3. Parse tokens into an Abstract Syntax Tree
    /// 4. Transform the AST into C# code using the configured factory
    /// 5. Replace original expressions with transformed code in the source
    /// 
    /// Extraction Rule: Extract ANY ( followed by &lt; (with optional whitespace)
    /// This simple rule handles all patterns: top-level JSX, callbacks, map/select, arrow functions, etc.
    /// </remarks>
    public class InlineXmlPreprocessor
    {
        /// <summary>
        /// Preprocesses source code and transforms all JSX expressions to C# factory calls.
        /// </summary>
        public PreprocessResult Preprocess(string sourceCode, ExtendedSharpConfig config, List<XmlSyntaxTree>? jsxTrees = null)
        {
            var result = new PreprocessResult { OriginalCode = sourceCode };
            
            string currentCode = sourceCode;
            
            // RECURSIVE EXTRACTION: Keep extracting and transforming until no more JSX is found
            // This handles nested JSX at ANY depth
            while (true)
            {
                var matches = jsxTrees != null && currentCode == sourceCode
                    ? ConvertTreesToMatches(jsxTrees)
                    : ExtractJsxExpressions(currentCode);

                if (matches.Count == 0)
                {
                    // No more JSX found - we're done
                    break;
                }

                var sb = new StringBuilder(currentCode);
                int offset = 0;
                bool anyTransformed = false;

                foreach (var match in matches)
                {
                    try
                    {
                        var transformed = TransformJsx(match.Content, config);
                        
                        int adjustedStart = match.StartPos + offset;
                        int adjustedEnd = match.EndPos + offset;
                        int originalLength = adjustedEnd - adjustedStart + 1;
                        
                        sb.Remove(adjustedStart, originalLength);
                        sb.Insert(adjustedStart, transformed);
                        
                        offset += transformed.Length - originalLength;
                        anyTransformed = true;

                        result.Transformations.Add(new JsxTransformation
                        {
                            Original = match.Expression,
                            Transformed = transformed,
                            Success = true
                        });
                    }
                    catch (Exception ex)
                    {
                        result.Transformations.Add(new JsxTransformation
                        {
                            Original = match.Expression,
                            Error = ex.Message,
                            Success = false
                        });
                    }
                }

                currentCode = sb.ToString();
                
                // If nothing was transformed in this pass, break to avoid infinite loop
                if (!anyTransformed)
                    break;
            }

            result.TransformedCode = currentCode;
            return result;
        }

        /// <summary>
        /// Transforms a single JSX content string to a C# factory call.
        /// </summary>
        private string TransformJsx(string jsxContent, ExtendedSharpConfig config)
        {
            var tokens = Tokenize(jsxContent);
            var pos = 0;
            var element = InlineXmlParser.Parse(tokens, ref pos);
            return InlineXmlTransformer.Transform(element, config);
        }

        /// <summary>
        /// Converts pre-extracted syntax trees to the internal <see cref="JsxMatch"/> format.
        /// </summary>
        private List<JsxMatch> ConvertTreesToMatches(List<XmlSyntaxTree> trees)
        {
            return trees.Select(t => new JsxMatch
            {
                Expression = t.FullExpression,
                Content = t.JsxContent,
                StartPos = t.StartPos,
                EndPos = t.EndPos
            }).ToList();
        }

        /// <summary>
        /// Extracts all JSX expressions from source code using a universal rule.
        /// </summary>
        /// <remarks>
        /// Rule: Extract ANY ( followed by &lt; (with optional whitespace between them)
        /// 
        /// This single, simple rule handles:
        /// - return ( &lt;div&gt; ) - top level
        /// - onClick={() => ( &lt;Modal&gt; )} - callbacks
        /// - .Select(role => ( &lt;div&gt; )) - map/filter/select
        /// - condition ? ( &lt;A&gt; ) : ( &lt;B&gt; ) - ternaries
        /// - [ ( &lt;Item&gt; ) ] - arrays
        /// - Any other pattern with ( followed by &lt;
        /// </remarks>
        private List<JsxMatch> ExtractJsxExpressions(string code)
        {
            var results = new List<JsxMatch>();
            var pos = 0;

            while (pos < code.Length)
            {
                // Find next '('
                var openParenIdx = code.IndexOf('(', pos);
                if (openParenIdx == -1) break;

                // Skip whitespace after '('
                var i = openParenIdx + 1;
                while (i < code.Length && char.IsWhiteSpace(code[i]))
                    i++;

                // Check if followed by '<' - this is JSX to extract
                if (i < code.Length && code[i] == '<')
                {
                    // Find matching closing paren
                    var closeIdx = FindClosingParen(code, openParenIdx);
                    if (closeIdx != -1)
                    {
                        // Extract content (from the '<' to just before ')')
                        var contentStart = i; // Already at the '<'
                        var contentEnd = closeIdx;
                        while (contentEnd > contentStart && char.IsWhiteSpace(code[contentEnd - 1]))
                            contentEnd--;

                        string content = code.Substring(contentStart, contentEnd - contentStart);
                        string fullExpr = code.Substring(openParenIdx, closeIdx - openParenIdx + 1);

                        results.Add(new JsxMatch
                        {
                            Expression = fullExpr,
                            Content = content,
                            StartPos = openParenIdx,
                            EndPos = closeIdx
                        });

                        pos = closeIdx + 1;
                        continue;
                    }
                }

                pos = openParenIdx + 1;
            }

            return results;
        }

        /// <summary>
        /// Finds the closing parenthesis matching an opening parenthesis.
        /// Accounts for nesting and string literals.
        /// </summary>
        private static int FindClosingParen(string code, int openPos)
        {
            var depth = 1;
            var i = openPos + 1;
            var inString = false;
            var stringChar = '\0';

            while (i < code.Length && depth > 0)
            {
                var c = code[i];

                if (inString)
                {
                    if (c == stringChar && (i == 0 || code[i - 1] != '\\'))
                        inString = false;
                    i++;
                    continue;
                }

                switch (c)
                {
                    case '"' or '\'':
                        inString = true;
                        stringChar = c;
                        break;
                    case '(':
                        depth++;
                        break;
                    case ')':
                        depth--;
                        break;
                }

                i++;
            }

            return depth == 0 ? i - 1 : -1;
        }

        /// <summary>
        /// Tokenizes a JSX content string into a list of tokens.
        /// </summary>
        private List<InlineXmlToken> Tokenize(string jsxContent)
        {
            var tokens = new List<InlineXmlToken>();
            var i = 0;
            var expectTagName = false;
            var inTagAttributes = false;

            while (i < jsxContent.Length)
            {
                char c = jsxContent[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                switch (c)
                {
                    case '<':
                    {
                        if (i + 1 < jsxContent.Length && jsxContent[i + 1] == '/')
                        {
                            tokens.Add(new InlineXmlToken(InlineXmlTokenType.ClosingOpenTag, "</"));
                            i += 2;
                        }
                        else
                        {
                            tokens.Add(new InlineXmlToken(InlineXmlTokenType.OpenTag, "<"));
                            i++;
                        }

                        expectTagName = true;
                        break;
                    }
                    case '>':
                        tokens.Add(new InlineXmlToken(InlineXmlTokenType.CloseTag, ">"));
                        i++;
                        expectTagName = false;
                        inTagAttributes = false;
                        break;
                    case '/' when i + 1 < jsxContent.Length && jsxContent[i + 1] == '>':
                        tokens.Add(new InlineXmlToken(InlineXmlTokenType.SelfClosingTag, "/>"));
                        i += 2;
                        expectTagName = false;
                        inTagAttributes = false;
                        break;
                    case '=':
                        tokens.Add(new InlineXmlToken(InlineXmlTokenType.Equals, "="));
                        i++;
                        break;
                    case '"':
                    case '\'':
                    {
                        var (token, newPos) = ReadString(jsxContent, i);
                        tokens.Add(token);
                        i = newPos;
                        break;
                    }
                    case '{':
                    {
                        var (token, newPos) = ReadCodeBlock(jsxContent, i);
                        tokens.Add(token);
                        i = newPos;
                        break;
                    }
                    default:
                    {
                        if (char.IsLetter(c) || c == '_')
                        {
                            var (token, newPos) = ReadIdentifier(jsxContent, i);

                            if (expectTagName)
                            {
                                token = new InlineXmlToken(InlineXmlTokenType.TagName, token.Value);
                                expectTagName = false;
                                inTagAttributes = true;
                            }
                            else if (!inTagAttributes)
                            {
                                int textStart = i;
                                while (i < jsxContent.Length && jsxContent[i] != '<' && jsxContent[i] != '{')
                                    i++;
                                var text = jsxContent.Substring(textStart, i - textStart).Trim();
                                if (!string.IsNullOrEmpty(text))
                                    tokens.Add(new InlineXmlToken(InlineXmlTokenType.TextContent, text));
                                break;
                            }

                            tokens.Add(token);
                            i = newPos;
                        }
                        else
                        {
                            int start = i;
                            while (i < jsxContent.Length && jsxContent[i] != '<' && jsxContent[i] != '{')
                                i++;
                            var text = jsxContent.Substring(start, i - start).Trim();
                            if (!string.IsNullOrEmpty(text))
                                tokens.Add(new InlineXmlToken(InlineXmlTokenType.TextContent, text));
                        }

                        break;
                    }
                }
            }

            return tokens;
        }

        /// <summary>
        /// Reads an identifier from the source.
        /// </summary>
        private static (InlineXmlToken, int) ReadIdentifier(string code, int start)
        {
            var i = start;
            while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_' || code[i] == '-'))
                i++;

            var name = code.Substring(start, i - start);
            return (new InlineXmlToken(InlineXmlTokenType.PropName, name), i);
        }

        /// <summary>
        /// Reads a string literal from the source.
        /// </summary>
        private (InlineXmlToken, int) ReadString(string code, int start)
        {
            var quote = code[start];
            var i = start + 1;

            while (i < code.Length)
            {
                if (code[i] == '\\' && i + 1 < code.Length)
                {
                    i += 2;
                }
                else if (code[i] == quote)
                {
                    i++;
                    break;
                }
                else
                {
                    i++;
                }
            }

            var value = code.Substring(start, i - start);
            return (new InlineXmlToken(InlineXmlTokenType.StringLiteral, value), i);
        }

        /// <summary>
        /// Reads a code block ({...}) from the source.
        /// </summary>
        private (InlineXmlToken, int) ReadCodeBlock(string code, int start)
        {
            var depth = 0;
            var i = start;
            var inString = false;
            var stringChar = '\0';

            while (i < code.Length)
            {
                char c = code[i];

                if (inString)
                {
                    if (c == '\\' && i + 1 < code.Length)
                    {
                        i += 2;
                        continue;
                    }
                    else if (c == stringChar)
                    {
                        inString = false;
                    }
                    i++;
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    inString = true;
                    stringChar = c;
                    i++;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        i++;
                        break;
                    }
                }

                i++;
            }

            var value = code.Substring(start, i - start);
            return (new InlineXmlToken(InlineXmlTokenType.CodeBlock, value), i);
        }
    }

    /// <summary>
    /// Represents a single extracted JSX expression with position information.
    /// </summary>
    public class JsxMatch
    {
        /// <summary>Gets or sets the full expression including parentheses.</summary>
        public string Expression { get; init; }
        
        /// <summary>Gets or sets the JSX/XML content.</summary>
        public string Content { get; init; }
        
        /// <summary>Gets or sets the starting position in source code.</summary>
        public int StartPos { get; init; }
        
        /// <summary>Gets or sets the ending position in source code.</summary>
        public int EndPos { get; init; }
    }

    /// <summary>
    /// Records the result of transforming a single JSX expression.
    /// </summary>
    public class JsxTransformation
    {
        /// <summary>Gets or sets the original JSX expression.</summary>
        public string Original { get; set; }
        
        /// <summary>Gets or sets the transformed C# code.</summary>
        public string Transformed { get; set; }
        
        /// <summary>Gets or sets the error message if transformation failed.</summary>
        public string Error { get; set; }
        
        /// <summary>Gets a value indicating whether the transformation succeeded.</summary>
        public bool Success { get; init; }
    }

    /// <summary>
    /// The complete result of preprocessing source code.
    /// </summary>
    public class PreprocessResult
    {
        /// <summary>Gets or sets the original source code.</summary>
        public string OriginalCode { get; set; }
        
        /// <summary>Gets or sets the transformed source code.</summary>
        public string TransformedCode { get; set; }
        
        /// <summary>Gets the list of transformation results.</summary>
        public List<JsxTransformation> Transformations { get; } = new();
    }
}