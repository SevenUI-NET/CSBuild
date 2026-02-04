using System.Text;
using SevenUI.ExtendedSharp.Configuration;

namespace SevenUI.ExtendedSharp.Syntax
{
    /// <summary>
    /// Transforms inline XML elements into generated C# factory method calls.
    /// 
    /// This class is responsible for converting parsed XML syntax into executable C# code that uses
    /// a factory pattern to instantiate UI elements. The transformation process handles three main aspects:
    /// 1. Converting XML attributes into property initialization objects
    /// 2. Recursively processing child elements and text nodes
    /// 3. Generating properly formatted factory method invocations with correct indentation
    /// 
    /// The output is formatted C# code that can be directly compiled and executed.
    /// </summary>
    public static class InlineXmlTransformer
    {
        /// <summary>
        /// Transforms an XML element tree into a factory method call string.
        /// 
        /// This is the primary entry point for the transformation process. It coordinates the three
        /// main steps: building the properties object, processing children, and constructing the
        /// factory call. The method handles indentation for code generation to maintain readability.
        /// </summary>
        /// <param name="element">The root XML element to transform. Must not be null.</param>
        /// <param name="config">Configuration object that specifies factory method names and behavior.</param>
        /// <param name="baseIndent">The base indentation level (in spaces) for the generated code. 
        /// Defaults to 0 for top-level elements.</param>
        /// <returns>A formatted C# code string representing a factory method call.</returns>
        /// <exception cref="ArgumentNullException">Thrown if element is null.</exception>
        public static string Transform(InlineXmlElement element, ExtendedSharpConfig config, int baseIndent = 0)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            // Convert the numeric indent level into actual whitespace for code formatting
            string indent = new string(' ', baseIndent);

            // Step 1: Process all attributes (both string literals and code expressions) into a properties object.
            // Example: <Button Text="Click me" Enabled={isEnabled} /> 
            // becomes: new ButtonProps { Text = "Click me", Enabled = isEnabled }
            var propsCode = BuildPropsObject(element);

            // Step 2: Recursively process child elements, text nodes, and code expressions.
            // Child indentation is increased by 4 spaces for nested readability.
            // This handles mixed content like <Div>Text <Button /> More text</Div>
            var childrenCode = BuildChildrenArguments(element, config, baseIndent + 4);

            // Step 3: Assemble the final factory method call with all components and proper formatting
            return BuildFactoryCall(element.TagName, propsCode, childrenCode, indent, config);
        }

        /// <summary>
        /// Builds the properties object initialization code from XML attributes.
        /// 
        /// This method processes both literal string properties and code expression properties,
        /// converting kebab-case attribute names (common in XML) into PascalCase property names
        /// (required by C# conventions). It determines the correct properties class name based on
        /// whether the element is a custom component or an HTML element.
        /// </summary>
        /// <param name="element">The XML element whose attributes should be converted to properties.</param>
        /// <returns>A C# code string representing property initialization, e.g., 
        /// "new ButtonProps { Text = \"Click\", IsEnabled = true }"</returns>
        private static string BuildPropsObject(InlineXmlElement element)
        {
            var props = new List<string>();

            // Process all string literal properties (e.g., Text="Hello")
            // Convert each key to PascalCase and format as property assignment
            foreach (var kv in element.StringProps)
                props.Add($"{ToPascalCase(kv.Key)} = {kv.Value}");

            // Process all code expression properties (e.g., Enabled={someVariable})
            // These are already in their raw form and just need the key conversion
            foreach (var kv in element.CodeProps)
                props.Add($"{ToPascalCase(kv.Key)} = {kv.Value}");

            // Determine the correct props class name:
            // - If the tag name is PascalCase (e.g., "Button"), it's a custom component, 
            //   so use "[TagName]Props" (e.g., "ButtonProps")
            // - If the tag name is lowercase (e.g., "div"), it's an HTML element, 
            //   so use "Html[TagName]Props" (e.g., "HtmlDivProps")
            string propsClassName = element.TagName != element.TagName.ToLower() 
                ? element.TagName + "Props" 
                : $"Html{ToPascalCase(element.TagName)}Props";

            // Generate the object initializer syntax. Empty properties still require an empty object.
            return props.Count > 0
                ? $"new {propsClassName} {{ {string.Join(", ", props)} }}"
                : $"new {propsClassName} {{ }}";
        }

        /// <summary>
        /// Processes child elements, text nodes, and code expressions into factory method arguments.
        /// 
        /// This method handles the three types of children that can appear in XML:
        /// 1. Text nodes - converted to XmlTextNode() calls with escaped strings
        /// 2. Element nodes - recursively transformed into nested factory calls
        /// 3. Code expressions - raw C# code that should be evaluated at runtime
        /// 
        /// Code expressions wrapped in braces (e.g., "{...}") are filtered out as they represent
        /// incomplete or placeholder syntax. Each child is indented consistently for readable output.
        /// </summary>
        /// <param name="element">The XML element whose children should be processed.</param>
        /// <param name="config">Configuration needed for recursive element transformation.</param>
        /// <param name="baseIndent">The indentation level for child elements in the output.</param>
        /// <returns>A list of C# code strings, each representing one child argument to the factory method.</returns>
        private static List<string> BuildChildrenArguments(InlineXmlElement element, ExtendedSharpConfig config, int baseIndent)
        {
            var childArgs = new List<string>();
            // Prepare indentation string for all children at this level
            string childIndent = new string(' ', baseIndent);

            foreach (var child in element.Children)
            {
               switch (child)
               {
                  case XmlTextNode textNode:
                     // This has been modified to just allow strings, consumers will probably have to 
                     // box or pattern match.
                     childArgs.Add(childIndent + $"\"{EscapeString(textNode.Text)}\"");
                     break;

                  case InlineXmlElement childElement:
                     // Recursively transform nested elements. The recursive call maintains the indent level.
                     // This produces nested factory calls that properly reflect the XML hierarchy.
                     // Example: <Button><Label>Text</Label></Button> becomes nested Transform calls
                     childArgs.Add(Transform(childElement, config, baseIndent));
                     break;

                  case string codeExpr:
                     // Code expressions are raw C# code fragments that should be evaluated at runtime.
                     // However, we filter out expressions that are wrapped in braces (e.g., "{...}")
                     // as these represent incomplete syntax or placeholder code.
                     var trimmed = codeExpr.Trim();
                     // Only include the expression if it doesn't start and end with braces
                     if (!trimmed.StartsWith("{") && !trimmed.EndsWith("}"))
                        childArgs.Add(childIndent + codeExpr);
                     break;
               }
            }

            return childArgs;
        }


        /// <summary>
        /// Assembles the final factory method call with all processed components.
        /// 
        /// This method constructs the complete C# code for the factory method invocation using the
        /// configured factory pattern. It handles proper formatting, indentation, and multiline
        /// argument lists for readability. The method distinguishes between HTML tags (lowercase)
        /// and custom components (PascalCase) when constructing the first argument.
        /// </summary>
        /// <param name="tagName">The name of the XML element (tag).</param>
        /// <param name="propsCode">The generated properties object initialization code.</param>
        /// <param name="childrenCode">The list of child element/node codes as arguments.</param>
        /// <param name="indent">The indentation to apply to the entire factory call.</param>
        /// <param name="config">Configuration specifying factory name and method name.</param>
        /// <returns>A formatted C# factory method call string.</returns>
        private static string BuildFactoryCall(string tagName, string propsCode, List<string> childrenCode, string indent, ExtendedSharpConfig config)
        {
            var sb = new StringBuilder();

            // Apply base indentation to the entire call
            sb.Append(indent);

            // Determine the tag type and format the first argument accordingly:
            // - HTML tags (lowercase) are passed as string literals: "div"
            // - Custom components (PascalCase) are passed as type references: Button
            var isHtmlTag = tagName.ToLower() == tagName;
            var insert = isHtmlTag ? $"\"{tagName}\"" : tagName;

            // Build the factory method call opening with tag name and properties
            // Example: UiFactory.CreateElement("div", new HtmlDivProps { ... }
            sb.Append($"{config.Factory}.{config.CreateElementName}({insert}, {propsCode}");

            // If there are child elements, format them on separate lines for readability
            if (childrenCode.Count > 0)
            {
                // Move to next line after properties and add first child
                sb.AppendLine(",");
                // Join all children with comma-newline separation for clean formatting
                sb.Append(string.Join(",\n", childrenCode));
                // Move to closing paren on a new line with proper indentation
                sb.AppendLine();
                sb.Append(indent);
            }

            // Close the factory method call
            sb.Append(")");
            return sb.ToString();
        }

        /// <summary>
        /// Converts kebab-case or space-separated strings to PascalCase format.
        /// 
        /// This utility method is essential for converting XML attribute names (which use kebab-case
        /// like "data-value") into C# property names (which use PascalCase like "DataValue").
        /// It splits on hyphens and capitalizes the first letter of each segment.
        /// </summary>
        /// <param name="input">The input string to convert (e.g., "my-property-name").</param>
        /// <returns>The PascalCase version (e.g., "MyPropertyName"). Returns empty/null input unchanged.</returns>
        /// <example>
        /// ToPascalCase("data-bind") returns "DataBind"
        /// ToPascalCase("onclick") returns "Onclick"
        /// ToPascalCase("") returns ""
        /// </example>
        private static string ToPascalCase(string input)
        {
            // Handle null or empty input by returning as-is
            if (string.IsNullOrEmpty(input)) return input;
            
            // Split on hyphens (kebab-case delimiter) to get individual words
            var parts = input.Split('-');
            
            // Capitalize the first letter of each part and concatenate them
            // For single-letter parts, just use the uppercase version
            return string.Concat(parts.Select(p => char.ToUpper(p[0]) + (p.Length > 1 ? p.Substring(1) : "")));
        }

        /// <summary>
        /// Escapes special characters in strings for safe C# code generation.
        /// 
        /// This method ensures that string content containing special characters (quotes, newlines, etc.)
        /// is properly escaped so it can be safely embedded as a string literal in generated C# code.
        /// Without proper escaping, strings containing these characters would break the syntax of the
        /// generated code.
        /// </summary>
        /// <param name="str">The raw string to escape.</param>
        /// <returns>The escaped string safe for use in C# string literals.</returns>
        /// <example>
        /// EscapeString("Hello\nWorld") returns "Hello\\nWorld"
        /// EscapeString("Say \"Hi\"") returns "Say \\\"Hi\\\""
        /// </example>
        private static string EscapeString(string str)
        {
            // Apply escape sequences in order of complexity to avoid double-escaping
            return str.Replace("\\", "\\\\")  // Backslash must be escaped first
                      .Replace("\"", "\\\"")  // Quote characters for string terminators
                      .Replace("\n", "\\n")   // Newline characters
                      .Replace("\r", "\\r")   // Carriage return characters
                      .Replace("\t", "\\t");  // Tab characters
        }
    }
}