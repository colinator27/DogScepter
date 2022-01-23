using DogScepterLib.Project.GML.Decompiler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DogScepterLib.Project.GML.Analysis
{
    public class NodeSearcher
    {
        public class ConditionalSearch
        {
            public string Name { get; set; } // Display name
            public bool Enabled { get; set; } = true; // Whether enabled
            public ASTNode.StatementKind Kind { get; set; } // Type of node to base conditions on
            public string Value { get; set; } // Required string evaluation of node (or null if N/A)
            public Condition Condition { get; set; } // Condition to evaluate for the search to be successful

            public Dictionary<string, SearchResult> CodeEntryToResult = new();
            public List<SearchResult> Results = new();

            // Print used for debugging purposes
            public void Print()
            {
                Console.WriteLine($"Query \"{Name}\"");
                Console.WriteLine("===");
                foreach (var res in Results)
                    Console.WriteLine($"-> {res.CodeEntryName} (x{res.Occurrences})");
            }
        }

        public class SearchResult
        {
            public string CodeEntryName { get; set; }
            public int Occurrences { get; set; } = 1;

            public SearchResult(string codeEntryName)
            {
                CodeEntryName = codeEntryName;
            }
        }

        public ProjectFile Project;
        public Dictionary<ASTNode.StatementKind, List<ConditionalSearch>> Queries = new();
        public Dictionary<string, ConditionalSearch> QueriesByName = new();

        public NodeSearcher(ProjectFile pf)
        {
            Project = pf;
        }

        public void Search(ConditionContext ctx, ASTNode node)
        {
            if (Queries.TryGetValue(node.Kind, out List<ConditionalSearch> queries))
            {
                foreach (var query in queries)
                {
                    if (!query.Enabled)
                        continue;

                    // Check for string evaluation, if applicable
                    if (query.Value != null && node.ToString() != query.Value)
                        continue;

                    // Now evaluate actual condition
                    if (query.Condition.Evaluate(ctx, node))
                    {
                        if (query.CodeEntryToResult.TryGetValue(ctx.DecompileContext.CodeName, out SearchResult sr))
                            sr.Occurrences++;
                        else
                        {
                            sr = new SearchResult(ctx.DecompileContext.CodeName);
                            query.CodeEntryToResult[ctx.DecompileContext.CodeName] = sr;
                            query.Results.Add(sr);
                        }
                    }
                }
            }

            ctx.Parents.Push(node);
            if (node.Kind == ASTNode.StatementKind.Variable)
                Search(ctx, (node as ASTVariable).Left);
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                    Search(ctx, child);
            }
            ctx.Parents.Pop();
        }

        public struct SearchJson
        {
            public List<ConditionalSearch> Queries { get; set; }
        }

        public bool AddFromFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                AddFromFile(File.ReadAllBytes(filePath));
                return true;
            }
            return false;
        }

        public void AddFromFile(byte[] data)
        {
            var options = new JsonSerializerOptions(ProjectFile.JsonOptions);
            options.Converters.Add(new ConditionConverter());
            var json = JsonSerializer.Deserialize<SearchJson>(data, options);
            if (json.Queries != null)
            {
                foreach (var query in json.Queries)
                {
                    // Find existing list to add to, or create a new one
                    List<ConditionalSearch> conditions;
                    if (Queries.TryGetValue(query.Kind, out conditions))
                        conditions.Add(query);
                    else
                    {
                        conditions = new();
                        conditions.Add(query);
                        Queries[query.Kind] = conditions;
                    }
                    QueriesByName[query.Name] = query;
                }
            }
        }
    }
}
