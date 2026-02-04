using SevenUI.ExtendedSharp.Configuration;
using SevenUI.ExtendedSharp.Syntax;

namespace SevenUI.ExtendedSharp;

/// <summary>
/// Entry point for the ExtendedSharp code generation build system.
/// 
/// This class implements a command-line tool that transforms .xcs files (ExtendedSharp XML syntax files)
/// into generated C# code. It supports two modes of operation:
/// 
/// 1. **Build Mode**: One-time processing of all .xcs files in the project, generating corresponding
///    .g.cs files in the Generated/ directory. Provides summary statistics and timing information.
/// 
/// 2. **Watch Mode**: Continuous monitoring of .xcs files with automatic rebuild on changes. Uses
///    debouncing to handle rapid file changes from editors and includes robust file I/O handling
///    for scenarios where files are locked or still being written.
/// 
/// The tool automatically locates the project root by searching for .csproj files and can be configured
/// via command-line arguments to specify custom factory methods and element creation function names.
/// 
/// Architecture overview:
/// - Program initialization discovers the project context and parses configuration
/// - Build phase orchestrates file discovery and processing
/// - Watch phase monitors the file system and triggers incremental rebuilds
/// - Individual file processing handles XML transformation with error tracking
/// - File I/O includes retry logic to handle editor lock contention
/// </summary>
class Program
{
    /// <summary>
    /// The XML preprocessor responsible for transforming .xcs syntax into C# code.
    /// Initialized as a static readonly field to maintain state across multiple invocations.
    /// </summary>
    private static readonly InlineXmlPreprocessor _processor = new();

    /// <summary>
    /// The parsed configuration object containing factory method names and code generation settings.
    /// Populated during Main() from command-line arguments.
    /// </summary>
    private static ExtendedSharpConfig _config;

    /// <summary>
    /// The root directory of the C# project, determined by locating the nearest .csproj file.
    /// Used as the base path for file discovery and the Generated/ output directory.
    /// </summary>
    private static string _projectRoot = null!;

    /// <summary>
    /// Main entry point for the ExtendedSharp build system.
    /// 
    /// This method orchestrates the entire build lifecycle:
    /// 1. Locates the project and determines the project root directory
    /// 2. Parses command-line configuration arguments
    /// 3. Displays configuration information to the user
    /// 4. Executes either a one-time build or enters watch mode based on --watch flag
    /// 
    /// Command-line arguments:
    /// - --factory [name]        : Specifies the factory class name (e.g., "UiFactory")
    /// - --create-element [name] : Specifies the factory method name (e.g., "CreateElement")
    /// - --watch                 : Enables continuous watch mode instead of one-time build
    /// </summary>
    static void Main(string[] args)
    {
        // Step 1: Locate the project file (.csproj) by searching up the directory tree
        // This establishes the project context and ensures we're working in the right directory
        var projectFile = LocateProject();
        _projectRoot = projectFile.DirectoryName!;

        // Provide user feedback about the loaded project
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Loaded {projectFile.Name}");
        Console.ResetColor();

        // Step 2: Parse command-line arguments into configuration
        // This allows users to customize factory method names for their specific codebase
        _config = ParseConfig(args);

        // Step 3: Display the active configuration to the user for verification
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Factory: {_config.Factory}");
        Console.WriteLine($"CreateElement: {_config.CreateElementName}");
        Console.ResetColor();

        // Step 4: Execute the appropriate workflow
        // If --watch is present, do an initial build then enter continuous watch mode
        // Otherwise, do a single build and exit
        if (args.Contains("--watch"))
        {
            Build();
            Watch();
        }
        else
        {
            Build();
        }
    }

    /// <summary>
    /// Monitors the project directory for changes to .xcs files and triggers incremental rebuilds.
    /// 
    /// This method implements a file watcher that continuously listens for file system events.
    /// Key features:
    /// - Watches all subdirectories recursively
    /// - Debounces rapid changes (500ms) to handle editor save operations that generate multiple events
    /// - Includes file existence checks to avoid processing deleted or inaccessible files
    /// - Per-file debounce timers prevent excessive rebuilds from the same file
    /// - Provides real-time user feedback with timestamps and change notifications
    /// - Gracefully disposes of resources on application exit
    /// 
    /// The debouncing strategy is crucial because:
    /// - Text editors often generate multiple file change events for a single save operation
    /// - Network file systems may have race conditions where files are not immediately accessible
    /// - The 500ms delay allows editors to finish writing and flush buffers
    /// </summary>
    static void Watch()
    {
        // Display watch mode activation message
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n⏱️  Watching for changes...\n");
        Console.ResetColor();

        // Maintain a timer per file to debounce rapid consecutive changes
        // This prevents processing the same file multiple times when editors generate burst events
        var debounceTimers = new Dictionary<string, System.Timers.Timer>();

        // Configure the file system watcher to monitor .xcs files only
        // NotifyFilter limits events to those we care about (LastWrite, Size)
        // IncludeSubdirectories ensures we catch changes in nested project structures
        var watcher = new FileSystemWatcher(_projectRoot)
        {
            Filter = "*.xcs",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        /// <summary>
        /// Inner function that handles a file change event with debouncing logic.
        /// 
        /// This function is called for both Changed and Created file system events.
        /// It implements debouncing by:
        /// 1. Checking if a timer already exists for this file
        /// 2. Canceling and disposing the old timer if present
        /// 3. Creating a new 500ms timer to delay processing
        /// 4. Processing the file only after the timer elapses with no new events
        /// </summary>
        void OnFileChanged(string filePath)
        {
            // Quick safety check: if the file no longer exists by the time we see the event,
            // skip processing (handles rapid delete-and-recreate scenarios)
            if (!File.Exists(filePath))
                return;

            var fileName = Path.GetFileName(filePath);
            
            // Notify user of detected change with timestamp for debugging
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Detected change: {fileName}");
            Console.ResetColor();

            // DEBOUNCE: Cancel any existing timer for this file
            // This ensures we don't build the file multiple times if rapid events occur
            if (debounceTimers.TryGetValue(filePath, out var existingTimer))
            {
                existingTimer.Stop();
                existingTimer.Dispose();
                debounceTimers.Remove(filePath);
            }

            // Create a new debounce timer that will trigger the build after a delay
            var timer = new System.Timers.Timer(500); // 500ms debounce - gives editors time to flush
            timer.AutoReset = false; // Only fire once per Start()
            timer.Elapsed += (s, e) =>
            {
                // Clean up the timer reference
                timer.Dispose();
                debounceTimers.Remove(filePath);

                try
                {
                    // SAFETY CHECK: Verify file still exists and is accessible
                    // This handles the case where a file was deleted between change detection and debounce completion
                    if (!File.Exists(filePath))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] File no longer exists: {fileName}");
                        Console.ResetColor();
                        return;
                    }

                    // Perform the incremental build for this single file
                    BuildFile(filePath);
                }
                catch (Exception ex)
                {
                    // Report build errors to the user without stopping the watch loop
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✖ Error: {ex.Message}");
                    Console.ResetColor();
                }
            };

            // Store the timer reference and start the debounce countdown
            debounceTimers[filePath] = timer;
            timer.Start();
        }

        // Wire up the file system watcher events to the debounce handler
        // Both Changed (file modified) and Created (new file) events trigger builds
        watcher.Changed += (s, e) => OnFileChanged(e.FullPath);
        watcher.Created += (s, e) => OnFileChanged(e.FullPath);

        // Display watch mode status to user
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Watching: {_projectRoot}");
        Console.ResetColor();
        Console.WriteLine("Press Ctrl+C to stop...\n");
        
        try
        {
            // Block the main thread indefinitely, allowing the file watcher to continue operating
            // The watcher runs on background threads, so this just prevents Main() from exiting
            Thread.Sleep(Timeout.Infinite);
        }
        finally
        {
            // Ensure proper cleanup when the application exits (Ctrl+C)
            watcher.Dispose();
            foreach (var timer in debounceTimers.Values)
                timer?.Dispose();
        }
    }

    /// <summary>
    /// Performs a complete build of all .xcs files in the project.
    /// 
    /// This method:
    /// 1. Creates the Generated/ output directory structure
    /// 2. Discovers all .xcs files recursively in the project
    /// 3. Processes each file through the XML transformer
    /// 4. Reports summary statistics (file count, total time)
    /// 
    /// This is typically called once on startup, or continuously when in watch mode.
    /// </summary>
    static void Build()
    {
        // Ensure the Generated/ directory exists for output files
        var generatedRoot = Path.Combine(_projectRoot, "Generated");
        Directory.CreateDirectory(generatedRoot);

        // Discover all .xcs files in the project tree
        // Recursive search ensures we process files in subdirectories
        var files = Directory.GetFiles(
            _projectRoot,
            "*.xcs",
            SearchOption.AllDirectories
        );

        // Handle the case where no files are found (empty project or misconfiguration)
        if (files.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠️  No .xcs files found");
            Console.ResetColor();
            return;
        }

        // Begin timing the entire build operation
        Console.WriteLine();
        var startTime = DateTime.UtcNow;

        // Process each discovered file through the transformer
        // Individual file errors are caught and reported without stopping the build
        foreach (var file in files)
        {
            BuildFile(file);
        }

        // Report build completion statistics
        var elapsed = DateTime.UtcNow - startTime;
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Built {files.Length} file{(files.Length != 1 ? "s" : "")} in {elapsed.TotalMilliseconds:F0}ms");
        Console.ResetColor();
    }

    /// <summary>
    /// Processes a single .xcs file: reads it, transforms it, and writes the generated .g.cs output.
    /// 
    /// This method handles the complete lifecycle of a single file:
    /// 1. Reads the source .xcs file with retry logic (handles file locks from editors)
    /// 2. Transforms the XML syntax into C# code using the preprocessor
    /// 3. Writes the output .g.cs file to the Generated/ directory
    /// 4. Reports status, timing, and any transformation errors
    /// 5. Catches and reports fatal errors without stopping the build
    /// 
    /// The method includes sophisticated file I/O handling because:
    /// - Editors may lock files during saves
    /// - Network file systems have race conditions
    /// - File permissions may prevent immediate access
    /// 
    /// Error Reporting:
    /// - Individual transformation errors are collected and displayed
    /// - Fatal I/O errors are caught and reported separately
    /// - Both success and error cases include precise timing information
    /// </summary>
    static void BuildFile(string file)
    {
        try
        {
            // Begin timing this specific file's processing
            var fileStartTime = DateTime.UtcNow;
            var relativePath = Path.GetRelativePath(_projectRoot, file);
            
            // STEP 1: Read the source file with retry logic
            // This handles editor lock contention and temporary file access issues
            string sourceCode = null!;
            int retries = 0;
            const int maxRetries = 10;
            
            // Retry loop: attempt to read the file up to 10 times with 50ms delays
            // This gives editors time to release file locks after save operations
            while (retries < maxRetries)
            {
                try
                {
                    sourceCode = File.ReadAllText(file);
                    break; // Success, exit retry loop
                }
                catch (IOException) when (retries < maxRetries - 1)
                {
                    // File is locked or inaccessible, retry after a brief delay
                    retries++;
                    System.Threading.Thread.Sleep(50); // Wait 50ms before next attempt
                }
            }
            
            // Final check: if we exhausted retries without success, throw an error
            if (sourceCode == null)
                throw new IOException($"Failed to read file after {maxRetries} retries");

            // STEP 2: Transform the XML syntax into C# code
            // This is the core code generation step
            var transform = _processor.Preprocess(sourceCode, _config);

            // STEP 3: Determine the output path in the Generated/ directory
            // Preserve the original directory structure so generated files are organized the same way
            var generatedRoot = Path.Combine(_projectRoot, "Generated");
            var outputPath = Path.Combine(generatedRoot, relativePath);
            outputPath = Path.ChangeExtension(outputPath, ".g.cs"); // Change .xcs to .g.cs

            // Create output directory if it doesn't exist
            var outputDir = Path.GetDirectoryName(outputPath)!;
            Directory.CreateDirectory(outputDir);

            // STEP 4: Write the generated C# code with retry logic
            // Same strategy as read: retry on I/O errors from editor locks
            retries = 0;
            while (retries < maxRetries)
            {
                try
                {
                    File.WriteAllText(outputPath, transform.TransformedCode);
                    break; // Success, exit retry loop
                }
                catch (IOException) when (retries < maxRetries - 1)
                {
                    // File is locked or permission denied, retry after delay
                    retries++;
                    System.Threading.Thread.Sleep(50);
                }
            }

            // STEP 5: Calculate statistics and report results
            var elapsed = DateTime.UtcNow - fileStartTime;
            var successCount = transform.Transformations.Count(t => t.Success);
            var errorCount = transform.Transformations.Count(t => !t.Success);

            // Format the output line with consistent spacing and color coding
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"  ");
            Console.ResetColor();

            // Choose status indicator based on error count
            if (errorCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"⚠️  ");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"✓ ");
                Console.ResetColor();
            }

            // Display the file path
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"{relativePath}");
            Console.ResetColor();

            // Show transformation statistics if there were errors
            if (errorCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($" ({successCount} ok, {errorCount} error{(errorCount != 1 ? "s" : "")})");
                Console.ResetColor();
            }

            // Display timing information in dim text
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($" {elapsed.TotalMilliseconds:F0}ms");
            Console.ResetColor();

            // STEP 6: Report individual transformation errors if present
            // This allows developers to see which XML expressions failed
            if (errorCount > 0)
            {
                foreach (var txn in transform.Transformations.Where(t => !t.Success))
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"      Error: {txn.Original}");
                    Console.Write($"      ");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(txn.Error);
                    Console.ResetColor();
                }
            }
        }
        catch (Exception ex)
        {
            // Fatal error: report it but continue with other files
            var relativePath = Path.GetRelativePath(_projectRoot, file);

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"  ");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"✖ ");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(relativePath);
            Console.ResetColor();

            // Display error message (and inner exception if present for debugging)
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"      {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"      {ex.InnerException.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Parses command-line arguments into an ExtendedSharpConfig object.
    /// 
    /// This method interprets the command-line flags and populates the configuration
    /// with user-specified values. Supported flags:
    /// - --factory [name]        : Sets the factory class name
    /// - --create-element [name] : Sets the factory method name for creating elements
    /// 
    /// Example: program.exe --factory UiFactory --create-element CreateElement
    /// 
    /// All flags are optional; if not provided, default configuration values are used.
    /// Invalid flag usage (missing values) throws ArgumentException.
    /// </summary>
    static ExtendedSharpConfig ParseConfig(string[] args)
    {
        // Start with default configuration
        var config = new ExtendedSharpConfig();

        // Iterate through arguments looking for recognized flags
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--factory":
                    // Require a value for --factory and advance the index
                    config.Factory = RequireValue(args, ref i, "--factory");
                    break;

                case "--create-element":
                    // Require a value for --create-element and advance the index
                    config.CreateElementName = RequireValue(args, ref i, "--create-element");
                    break;
            }
        }

        return config;
    }

    /// <summary>
    /// Utility to safely extract the value following a command-line flag.
    /// 
    /// This method validates that:
    /// 1. There is a next argument after the flag
    /// 2. The next argument is not another flag (doesn't start with --)
    /// 
    /// If validation fails, throws ArgumentException with a clear message.
    /// If successful, increments the index pointer and returns the value.
    /// </summary>
    /// <param name="args">The complete command-line arguments array.</param>
    /// <param name="index">The current index (updated to point to the value).</param>
    /// <param name="flag">The flag name for error reporting.</param>
    /// <returns>The value following the flag.</returns>
    /// <exception cref="ArgumentException">Thrown if the flag has no value or the next arg is another flag.</exception>
    static string RequireValue(string[] args, ref int index, string flag)
    {
        // Check if there's a next argument and it's not another flag
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--"))
        {
            throw new ArgumentException($"Missing value for {flag}");
        }

        // Move to the value and return it
        index++;
        return args[index];
    }

    /// <summary>
    /// Locates the project root by searching for a .csproj file.
    /// 
    /// This method implements a search algorithm that:
    /// 1. Starts in the current working directory
    /// 2. Searches for exactly one .csproj file
    /// 3. If found, returns it (project root identified)
    /// 4. If not found, moves to the parent directory and repeats
    /// 5. Continues until a .csproj is found or the file system root is reached
    /// 
    /// Error handling:
    /// - If multiple .csproj files exist in a directory, throws an error (ambiguous)
    /// - If no .csproj is found anywhere, throws an error (not in a project)
    /// 
    /// This allows the tool to be run from any subdirectory of a project,
    /// automatically discovering the project root.
    /// </summary>
    /// <returns>FileInfo pointing to the located .csproj file.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no .csproj found or if multiple .csproj files exist in one directory.</exception>
    static FileInfo LocateProject()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());

        // Search up the directory tree until we find a project
        while (dir != null)
        {
            // Look for .csproj files in the current directory (not recursive)
            var projects = dir.GetFiles("*.csproj");

            // Found exactly one project: this is our root
            if (projects.Length == 1)
                return projects[0];

            // Found multiple projects: ambiguous which is the target
            // User should run from the specific project directory
            if (projects.Length > 1)
                throw new InvalidOperationException(
                    "Multiple .csproj files found. Run the command from the desired project directory."
                );

            // No projects in this directory, move up to parent
            dir = dir.Parent;
        }

        // Reached the file system root without finding any .csproj
        throw new InvalidOperationException(
            "No .csproj found. Run this command from within a project directory."
        );
    }
}