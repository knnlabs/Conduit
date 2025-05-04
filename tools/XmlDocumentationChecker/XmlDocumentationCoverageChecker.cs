using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace ConduitLLM.Tools
{
    /// <summary>
    /// Utility to check XML documentation coverage across the Conduit solution.
    /// </summary>
    public class XmlDocumentationCoverageChecker
    {
        private readonly string _solutionDirectory;
        private readonly string[] _projectsToScan;
        private readonly string[] _excludedDirectories = new[] { "bin", "obj", "TestHelpers" };
        private readonly string[] _excludedFiles = new[] { "Program.cs", "Usings.cs", "AssemblyInfo.cs" };
        
        private record TypeDocCoverage(string FullName, int MemberCount, int DocumentedCount, double CoveragePercentage);
        
        /// <summary>
        /// Initializes a new instance of the XmlDocumentationCoverageChecker.
        /// </summary>
        /// <param name="solutionDirectory">The root directory of the solution to scan.</param>
        /// <param name="projectsToScan">Optional array of project names to scan. If null, all projects are scanned.</param>
        public XmlDocumentationCoverageChecker(string solutionDirectory, string[] projectsToScan = null)
        {
            _solutionDirectory = solutionDirectory;
            _projectsToScan = projectsToScan ?? new[]
            {
                "ConduitLLM.Core",
                "ConduitLLM.Configuration", 
                "ConduitLLM.Providers",
                "ConduitLLM.WebUI",
                "ConduitLLM.Http"
            };
        }
        
        /// <summary>
        /// Run the coverage check and generate a report.
        /// </summary>
        public void GenerateReport()
        {
            Console.WriteLine($"Analyzing XML documentation coverage for Conduit solution...\n");
            
            var results = new List<(string ProjectName, List<string> UndocumentedTypes, List<(string TypeName, List<string> UndocumentedMembers)> PartiallyDocumentedTypes)>();
            
            foreach (var projectName in _projectsToScan)
            {
                Console.WriteLine($"Scanning project: {projectName}");
                var projectDir = Path.Combine(_solutionDirectory, projectName);
                
                if (!Directory.Exists(projectDir))
                {
                    Console.WriteLine($"  Project directory not found: {projectDir}");
                    continue;
                }
                
                var csFiles = GetCSharpFiles(projectDir);
                Console.WriteLine($"  Found {csFiles.Count} C# files");
                
                var undocumentedTypes = new List<string>();
                var partiallyDocumentedTypes = new List<(string TypeName, List<string> UndocumentedMembers)>();
                
                foreach (var file in csFiles)
                {
                    var content = File.ReadAllText(file);
                    var fileName = Path.GetFileName(file);
                    
                    // Skip files that don't define public types or interfaces
                    if (!content.Contains("public class") && 
                        !content.Contains("public interface") && 
                        !content.Contains("public record") &&
                        !content.Contains("public enum") &&
                        !content.Contains("public struct"))
                    {
                        continue;
                    }
                    
                    // Check for class-level documentation
                    var hasTypeDocumentation = content.Contains("/// <summary>");
                    var typeName = ExtractTypeName(content, fileName);
                    
                    if (!hasTypeDocumentation)
                    {
                        undocumentedTypes.Add(typeName);
                    }
                    else
                    {
                        // Check for method-level documentation
                        var undocumentedMembers = FindUndocumentedMethods(content, typeName);
                        if (undocumentedMembers.Any())
                        {
                            partiallyDocumentedTypes.Add((typeName, undocumentedMembers));
                        }
                    }
                }
                
                results.Add((projectName, undocumentedTypes, partiallyDocumentedTypes));
            }
            
            // Generate the report
            PrintReport(results);
        }
        
        private List<string> GetCSharpFiles(string directory)
        {
            var files = new List<string>();
            
            foreach (var dir in Directory.GetDirectories(directory))
            {
                var dirName = Path.GetFileName(dir);
                if (_excludedDirectories.Contains(dirName))
                    continue;
                
                files.AddRange(GetCSharpFiles(dir));
            }
            
            files.AddRange(
                Directory.GetFiles(directory, "*.cs")
                    .Where(f => !_excludedFiles.Contains(Path.GetFileName(f)))
            );
            
            return files;
        }
        
        private string ExtractTypeName(string content, string fileName)
        {
            // Simple extraction - this is a basic implementation
            // In a real implementation, you would use Roslyn for proper parsing
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            
            // Try to extract namespace
            var namespaceMatch = System.Text.RegularExpressions.Regex.Match(content, @"namespace\s+([^\s{;]+)");
            var namespaceName = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "Unknown";
            
            return $"{namespaceName}.{nameWithoutExtension}";
        }
        
        private List<string> FindUndocumentedMethods(string content, string typeName)
        {
            var undocumentedMethods = new List<string>();
            
            // This is a simple implementation - in reality, you'd want to use Roslyn
            // to properly parse the code and find all public methods
            var lines = content.Split('\n');
            
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                
                // Check if line declares a public method, property, or event
                if ((line.Contains("public ") || line.Contains("protected ")) && 
                    !line.Contains("class ") && 
                    !line.Contains("interface ") && 
                    !line.Contains("record ") &&
                    !line.Contains("enum ") &&
                    !line.Contains("struct ") &&
                    !line.Contains("//") &&
                    !line.Contains("/*") &&
                    !line.Contains("{") &&
                    !line.Contains("}"))
                {
                    // Check if previous lines contain XML docs
                    bool hasDocumentation = false;
                    for (int j = i - 1; j >= 0 && j >= i - 10; j--)
                    {
                        if (lines[j].Trim().Contains("/// <summary>"))
                        {
                            hasDocumentation = true;
                            break;
                        }
                        
                        // If we hit another method or class declaration, stop looking
                        if (lines[j].Trim().Contains("public ") || lines[j].Trim().Contains("private ") || 
                            lines[j].Trim().Contains("protected ") || lines[j].Trim().Contains("internal "))
                        {
                            break;
                        }
                    }
                    
                    if (!hasDocumentation)
                    {
                        // Extract method name - this is very simplistic
                        var parts = line.Split(new[] { ' ', '(', '<' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3)
                        {
                            var methodName = parts[2];
                            undocumentedMethods.Add(methodName);
                        }
                    }
                }
            }
            
            return undocumentedMethods;
        }
        
        private void PrintReport(List<(string ProjectName, List<string> UndocumentedTypes, List<(string TypeName, List<string> UndocumentedMembers)> PartiallyDocumentedTypes)> results)
        {
            Console.WriteLine("\n===============================================");
            Console.WriteLine("XML DOCUMENTATION COVERAGE REPORT");
            Console.WriteLine("===============================================\n");
            
            int totalUndocumentedTypes = 0;
            int totalPartiallyDocumentedTypes = 0;
            int totalTypesScanned = 0;
            
            foreach (var (projectName, undocumentedTypes, partiallyDocumentedTypes) in results)
            {
                var projectTotalTypes = undocumentedTypes.Count + partiallyDocumentedTypes.Count;
                if (projectTotalTypes == 0)
                    continue;
                
                totalTypesScanned += projectTotalTypes;
                totalUndocumentedTypes += undocumentedTypes.Count;
                totalPartiallyDocumentedTypes += partiallyDocumentedTypes.Count;
                
                Console.WriteLine($"PROJECT: {projectName}");
                Console.WriteLine($"  Total Types: {projectTotalTypes}");
                Console.WriteLine($"  Undocumented Types: {undocumentedTypes.Count}");
                Console.WriteLine($"  Partially Documented Types: {partiallyDocumentedTypes.Count}");
                
                if (undocumentedTypes.Any())
                {
                    Console.WriteLine("\n  UNDOCUMENTED TYPES:");
                    foreach (var type in undocumentedTypes.OrderBy(t => t))
                    {
                        Console.WriteLine($"    - {type}");
                    }
                }
                
                if (partiallyDocumentedTypes.Any())
                {
                    Console.WriteLine("\n  PARTIALLY DOCUMENTED TYPES:");
                    foreach (var (typeName, undocumentedMembers) in partiallyDocumentedTypes.OrderBy(t => t.TypeName))
                    {
                        Console.WriteLine($"    - {typeName}");
                        Console.WriteLine($"      Undocumented Members: {string.Join(", ", undocumentedMembers.Take(5))}");
                        if (undocumentedMembers.Count > 5)
                        {
                            Console.WriteLine($"      ... and {undocumentedMembers.Count - 5} more");
                        }
                    }
                }
                
                Console.WriteLine("\n-----------------------------------------------\n");
            }
            
            // Final summary
            Console.WriteLine("SUMMARY:");
            Console.WriteLine($"  Total Types Scanned: {totalTypesScanned}");
            Console.WriteLine($"  Fully Documented Types: {totalTypesScanned - totalUndocumentedTypes - totalPartiallyDocumentedTypes}");
            Console.WriteLine($"  Partially Documented Types: {totalPartiallyDocumentedTypes}");
            Console.WriteLine($"  Completely Undocumented Types: {totalUndocumentedTypes}");
            
            var coveragePercentage = totalTypesScanned > 0 
                ? (double)(totalTypesScanned - totalUndocumentedTypes) / totalTypesScanned * 100 
                : 0;
                
            Console.WriteLine($"  Overall Documentation Coverage: {coveragePercentage:F1}%");
            
            // Recommendations
            Console.WriteLine("\nRECOMMENDATIONS:");
            
            if (totalUndocumentedTypes > 0)
            {
                Console.WriteLine("  1. Focus first on adding class-level documentation to completely undocumented types");
            }
            
            if (totalPartiallyDocumentedTypes > 0)
            {
                Console.WriteLine("  2. Complete documentation for partially documented types, focusing on public APIs first");
            }
            
            Console.WriteLine("  3. Add or improve documentation in the following order:");
            Console.WriteLine("     - Public interfaces and abstract classes");
            Console.WriteLine("     - Provider implementation classes");
            Console.WriteLine("     - Core model classes");
            Console.WriteLine("     - Controller classes");
            Console.WriteLine("     - Service classes");
            
            Console.WriteLine("\n===============================================");
        }
        
        /// <summary>
        /// Entry point for the XML documentation checker tool.
        /// </summary>
        public static void Main(string[] args)
        {
            string solutionDir = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
            
            var checker = new XmlDocumentationCoverageChecker(solutionDir);
            checker.GenerateReport();
        }
    }
}