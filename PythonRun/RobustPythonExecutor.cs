using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Community.PowerToys.Run.Plugin.PythonRun
{
    public class RobustPythonExecutor
    {
        public static (string output, string error, int exitCode, bool timedOut)
            RunPythonCodeWithTimeout(string pythonCode, int timeoutMs = 30000)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "py.exe",
                Arguments = $"-c \"{EscapeArgument(pythonCode)}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            try
            {
                using (Process process = new Process { StartInfo = startInfo })
                {
                    StringBuilder outputBuilder = new StringBuilder();
                    StringBuilder errorBuilder = new StringBuilder();

                    // 异步读取输出，避免缓冲区满导致死锁
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            outputBuilder.AppendLine(e.Data);
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            errorBuilder.AppendLine(e.Data);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    bool finished = process.WaitForExit(timeoutMs);

                    if (!finished)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch { }
                        return ("", "进程超时被终止", -1, true);
                    }

                    // 确保所有输出都被读取
                    process.WaitForExit();

                    return (outputBuilder.ToString(), errorBuilder.ToString(), process.ExitCode, false);
                }
            }
            catch (Exception ex)
            {
                return ("", $"执行异常: {ex.Message}", -1, false);
            }
        }

        /// <summary>
        /// 检查代码中的括号是否正确闭合
        /// </summary>
        /// <param name="code">要检查的代码</param>
        /// <returns>如果括号闭合返回 true，否则返回 false</returns>
        public static bool AreBracketsBalanced(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return true;
            }

            var stack = new Stack<char>();
            bool inSingleQuote = false;
            bool inDoubleQuote = false;
            bool inTripleSingleQuote = false;
            bool inTripleDoubleQuote = false;

            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];

                // 检查是否在三引号字符串中
                if (i + 2 < code.Length)
                {
                    string threeChars = code.Substring(i, 3);

                    if (threeChars == "\"\"\"" && !inSingleQuote && !inTripleSingleQuote)
                    {
                        inTripleDoubleQuote = !inTripleDoubleQuote;
                        i += 2; // 跳过接下来的两个引号
                        continue;
                    }

                    if (threeChars == "'''" && !inDoubleQuote && !inTripleDoubleQuote)
                    {
                        inTripleSingleQuote = !inTripleSingleQuote;
                        i += 2; // 跳过接下来的两个引号
                        continue;
                    }
                }

                // 如果在三引号字符串中，跳过所有字符
                if (inTripleDoubleQuote || inTripleSingleQuote)
                {
                    continue;
                }

                // 处理转义字符
                if (c == '\\' && i + 1 < code.Length)
                {
                    i++; // 跳过下一个字符
                    continue;
                }

                // 处理单引号字符串
                if (c == '\'' && !inDoubleQuote)
                {
                    inSingleQuote = !inSingleQuote;
                    continue;
                }

                // 处理双引号字符串
                if (c == '"' && !inSingleQuote)
                {
                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }

                // 如果在字符串中，跳过括号检查
                if (inSingleQuote || inDoubleQuote)
                {
                    continue;
                }

                // 检查左括号
                if (c == '(' || c == '[' || c == '{')
                {
                    stack.Push(c);
                }
                // 检查右括号
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (stack.Count == 0)
                    {
                        return false; // 右括号多于左括号
                    }

                    char left = stack.Pop();

                    // 检查括号是否匹配
                    if ((c == ')' && left != '(') ||
                        (c == ']' && left != '[') ||
                        (c == '}' && left != '{'))
                    {
                        return false; // 括号类型不匹配
                    }
                }
            }

            // 所有括号都应该被闭合，且不应该在未闭合的字符串中
            return stack.Count == 0 && !inSingleQuote && !inDoubleQuote &&
                   !inTripleSingleQuote && !inTripleDoubleQuote;
        }

        /// <summary>
        /// 为 Python 代码添加自动打印功能，模拟 REPL 行为
        /// </summary>
        public static string AutoPrint(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return code;
            }

            code = code.Trim();
            code = code.Replace("\"", "\\\"");

            return $@"
code = compile(""{code}"", ""<stdin>"", ""single"")
value = eval(code)
if value is not None:
    import sys
    sys.displayhook(value)
".Trim();
        }

        private static string EscapeArgument(string argument)
        {
            // 转义双引号和反斜杠
            return argument.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public static void Main()
        {
            // 示例 1: 简单执行
            Console.WriteLine("=== 示例 1: 简单执行 ===");
            var result1 = RunPythonCodeWithTimeout("print('Hello World'); print(2 + 2)");
            Console.WriteLine($"输出:\n{result1.output}");
            Console.WriteLine($"退出代码: {result1.exitCode}\n");

            // 示例 2: 执行有错误的代码
            Console.WriteLine("=== 示例 2: 错误处理 ===");
            var result2 = RunPythonCodeWithTimeout("print(1/0)");
            Console.WriteLine($"输出:\n{result2.output}");
            Console.WriteLine($"错误:\n{result2.error}");
            Console.WriteLine($"退出代码: {result2.exitCode}\n");

            // 示例 3: 超时处理
            Console.WriteLine("=== 示例 3: 超时处理 ===");
            var result3 = RunPythonCodeWithTimeout("import time; time.sleep(10)", 2000);
            if (result3.timedOut)
            {
                Console.WriteLine("执行超时！\n");
            }

            // 示例 4: 大量输出
            Console.WriteLine("=== 示例 4: 大量输出 ===");
            var result4 = RunPythonCodeWithTimeout("for i in range(10): print(f'Line {i}')");
            Console.WriteLine($"输出:\n{result4.output}");

            // 示例 5: 使用 Python 模块
            Console.WriteLine("=== 示例 5: 使用 Python 模块 ===");
            var result5 = RunPythonCodeWithTimeout("import json; print(json.dumps({'key': 'value'}))");
            Console.WriteLine($"输出:\n{result5.output}");
        }
    }
}
