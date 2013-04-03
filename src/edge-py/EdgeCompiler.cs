using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IronPython.Hosting;
using IronPython.Runtime;
using Microsoft.Scripting.Hosting;

public class EdgeCompiler
{
    static readonly Regex whitespacePrefixRegex = new Regex(@"^(\s*)[^#\r\n]");

    public Func<object, Task<object>> CompileFunc(IDictionary<string, object> parameters)
    {
        string source = this.NormalizeSource((string)parameters["source"]);

        bool sync = false;
        object tmp;
        if (parameters.TryGetValue("sync", out tmp))
        {
            sync = (bool)tmp;
        }

        // Compile to a Python lambda expression
        ScriptEngine engine = Python.CreateEngine();
        ScriptSource script = engine.CreateScriptSourceFromString(source, "path-to-py");
        PythonFunction pythonFunc = script.Execute() as PythonFunction;
        if (pythonFunc == null)
        {
            throw new InvalidOperationException("The Python code must evaluate to a Python lambda expression that takes one parameter, e.g. `lambda x: x + 1`.");
        }
        
        ObjectOperations operations = engine.CreateOperations();

        // create a Func<object,Task<object>> delegate around the method invocation using reflection

        if (sync)
        {
            return (input) => 
            {
                object ret = operations.Invoke(pythonFunc, new object[] { input });
                return Task.FromResult<object>(ret);
            };
        }
        else
        {
            return (input) =>
            {
                var task = new Task<object>(() =>
                {
                    object ret = operations.Invoke(pythonFunc, new object[] { input });
                    return ret;
                });

                task.Start();

                return task;
            };
        }
    }

    string NormalizeSource(string source)
    {
        if (source.EndsWith(".py", StringComparison.InvariantCultureIgnoreCase))
        {
            // Read source from file
            source = File.ReadAllText(source);
        }
        else
        {
            // Remove the whitespace prefix found on the first line contaning source from all source lines.
            // This is to allow indenting Python code embedded in node.js sources.
            string[] lines = source.Split('\n');
            string prefix = null;
            foreach (string line in lines)
            {
                Match prefixMatch = whitespacePrefixRegex.Match(line);
                if (prefixMatch.Success)
                {
                    prefix = prefixMatch.Groups[1].Value;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(prefix))
            {
                Regex replacement = new Regex("^" + prefix, RegexOptions.Multiline);
                source = replacement.Replace(source, string.Empty);
            }
        }

        return source;
    }
}
