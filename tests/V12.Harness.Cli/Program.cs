// V12.Harness.Cli -- CLI-Anything Protocol Runner (Build 980 Proof-of-Work)
// Direct-reflection discovery: avoids xunit.runner.utility AssemblyRunner host
// which cannot load net48 assemblies from the dotnet CLI runtime context.
// Discovers all [Fact] methods in V12.Tests, executes synchronously, emits JSON.
// Exit code: 0 = all pass, 1 = any failure.
// ASCII-only output.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

// Import V12.Tests types directly (same process -- no cross-assembly loading needed)
using V12.Tests;

namespace V12.Harness.Cli
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            bool jsonMode = args.Length == 0
                || Array.Exists(args, a => a.Equals("--json", StringComparison.OrdinalIgnoreCase));

            // Discover all test classes and [Fact] methods in V12.Tests assembly
            var testsAssembly = typeof(T_RC_02_CancelAllSuppression).Assembly;
            var results       = new List<TestResult>();
            var globalSw      = Stopwatch.StartNew();
            int exitCode      = 0;

            var testClasses = testsAssembly.GetExportedTypes()
                .Where(t => t.GetCustomAttributes(false)
                    .Any(a => a.GetType().Name == "CollectionAttribute")
                    || t.GetMethods().Any(m => m.GetCustomAttributes(false)
                        .Any(a => a.GetType().Name == "FactAttribute")))
                .ToArray();

            // Also include types directly rather than depending on attribute scanning
            var directTypes = new Type[]
            {
                typeof(T_RC_02_CancelAllSuppression),
                typeof(T_PL_01_StopLimitPricing)
            };

            // Merge (keep unique types)
            var allTypes = directTypes.Concat(testClasses)
                .GroupBy(t => t.FullName)
                .Select(g => g.First())
                .ToArray();

            foreach (var testClass in allTypes)
            {
                var factMethods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttributes(false)
                        .Any(a => a.GetType().Name == "FactAttribute"))
                    .ToArray();

                foreach (var method in factMethods)
                {
                    var sw      = Stopwatch.StartNew();
                    string name = testClass.Name + "." + method.Name;
                    try
                    {
                        object instance = Activator.CreateInstance(testClass);
                        method.Invoke(instance, null);
                        sw.Stop();
                        results.Add(new TestResult
                        {
                            Name     = name,
                            Result   = "PASS",
                            Duration = (int)sw.ElapsedMilliseconds
                        });
                    }
                    catch (TargetInvocationException tex)
                    {
                        sw.Stop();
                        string msg = tex.InnerException != null
                            ? tex.InnerException.Message
                            : tex.Message;
                        results.Add(new TestResult
                        {
                            Name     = name,
                            Result   = "FAIL",
                            Duration = (int)sw.ElapsedMilliseconds,
                            Message  = msg
                        });
                        exitCode = 1;
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        results.Add(new TestResult
                        {
                            Name     = name,
                            Result   = "FAIL",
                            Duration = (int)sw.ElapsedMilliseconds,
                            Message  = ex.Message
                        });
                        exitCode = 1;
                    }
                }
            }

            globalSw.Stop();

            int passed  = results.Count(r => r.Result == "PASS");
            int failed  = results.Count(r => r.Result == "FAIL");
            int skipped = results.Count(r => r.Result == "SKIP");

            if (jsonMode)
            {
                Console.OutputEncoding = Encoding.ASCII;
                Console.WriteLine(BuildJson(passed, failed, skipped, globalSw.ElapsedMilliseconds, results));
            }
            else
            {
                foreach (var t in results)
                    Console.WriteLine("[{0}] {1} ({2}ms){3}",
                        t.Result.PadRight(4), t.Name, t.Duration,
                        t.Message != null ? " -- " + t.Message : "");

                Console.WriteLine();
                Console.WriteLine("Passed={0} Failed={1} Skipped={2} Total_ms={3}",
                    passed, failed, skipped, globalSw.ElapsedMilliseconds);
            }

            return exitCode;
        }

        private static string BuildJson(int passed, int failed, int skipped,
            long totalMs, List<TestResult> results)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"build\": \"980\",\n");
            sb.Append("  \"suites\": [\"T-RC-02\", \"T-PL-01\"],\n");
            sb.AppendFormat("  \"passed\": {0},\n", passed);
            sb.AppendFormat("  \"failed\": {0},\n", failed);
            sb.AppendFormat("  \"skipped\": {0},\n", skipped);
            sb.AppendFormat("  \"total_ms\": {0},\n", totalMs);
            sb.Append("  \"tests\": [\n");

            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                sb.Append("    {");
                sb.AppendFormat(" \"name\": \"{0}\",", EscapeJson(r.Name));
                sb.AppendFormat(" \"result\": \"{0}\",", r.Result);
                sb.AppendFormat(" \"duration_ms\": {0}", r.Duration);
                if (r.Message != null)
                    sb.AppendFormat(", \"message\": \"{0}\"", EscapeJson(r.Message));
                sb.Append(" }");
                if (i < results.Count - 1) sb.Append(",");
                sb.Append("\n");
            }

            sb.Append("  ]\n");
            sb.Append("}");
            return sb.ToString();
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private class TestResult
        {
            public string Name;
            public string Result;
            public int    Duration;
            public string Message;
        }
    }
}
