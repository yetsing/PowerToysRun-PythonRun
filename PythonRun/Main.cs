using Community.PowerToys.Run.Plugin.PythonRun.Properties;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using System.Windows;
using System.Windows.Controls;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.PythonRun
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISettingProvider, IReloadable, IDisposable, IDelayedExecutionPlugin
    {
        private const string Setting = nameof(Setting);
        // current value of the setting
        private bool _setting;
        private PluginInitContext _context;
        private string _iconPath;
        private bool _disposed;
        public string Name => Resources.plugin_name;
        public string Description => Resources.plugin_description;
        public static string PluginID => "de278e77e39f4d90bbd91f44022f4917";

        // TODO: add additional options (optional)
        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                PluginOptionType= PluginAdditionalOption.AdditionalOptionType.Checkbox,
                Key = Setting,
                DisplayLabel = Resources.plugin_setting,
            },
        };

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            _setting = settings?.AdditionalOptions?.FirstOrDefault(x => x.Key == Setting)?.Value ?? false;
        }

        // TODO: return context menus for each Result (optional)
        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return new List<ContextMenuResult>(0);
        }

        // TODO: return query results
        public List<Result> Query(Query query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var results = new List<Result>();

            // empty query
            if (string.IsNullOrEmpty(query.Search))
            {
                results.Add(new Result
                {
                    Title = Name,
                    SubTitle = Description,
                    QueryTextDisplay = string.Empty,
                    IcoPath = _iconPath,
                    Action = action =>
                    {
                        return true;
                    },
                });
                return results;
            }

            return results;
        }

        // TODO: return delayed query results (optional)
        public List<Result> Query(Query query, bool delayedExecution)
        {
            ArgumentNullException.ThrowIfNull(query);

            var results = new List<Result>();

            // empty query
            if (string.IsNullOrEmpty(query.Search))
            {
                return results;
            }

            // 检查括号是否闭合，如果没有闭合就不执行
            if (!RobustPythonExecutor.AreBracketsBalanced(query.Search))
            {
                results.Add(new Result
                {
                    Title = "等待输入完成...",
                    SubTitle = "代码括号或引号未闭合",
                    IcoPath = "Images/info.png",
                    Action = context => false
                });
                return results;
            }

            // 自动添加 print，模拟 REPL 行为
            string wrappedCode = RobustPythonExecutor.AutoPrint(query.Search);
            var executeResult = RobustPythonExecutor.RunPythonCodeWithTimeout(wrappedCode, 3000);

            // 超时
            if (executeResult.timedOut)
            {
                results.Add(new Result
                {
                    Title = "⏱️ 执行超时",
                    SubTitle = "Python 代码执行超过 3 秒",
                    IcoPath = "Images/error.png"
                });
                return results;
            }

            // 有错误
            if (executeResult.exitCode != 0 || !string.IsNullOrEmpty(executeResult.error))
            {
                var errorMessage = string.IsNullOrEmpty(executeResult.error)
                    ? $"退出代码: {executeResult.exitCode}"
                    : executeResult.error.Trim();

                results.Add(new Result
                {
                    Title = "❌ 执行错误",
                    SubTitle = errorMessage.Length > 200 ? errorMessage.Substring(0, 200) + "..." : errorMessage,
                    IcoPath = "Images/error.png",
                    Action = context =>
                    {
                        Clipboard.SetText(executeResult.error);
                        return true;
                    }
                });
                return results;
            }

            // 成功且有输出
            if (!string.IsNullOrEmpty(executeResult.output))
            {
                var output = executeResult.output.Trim();
                results.Add(new Result
                {
                    Title = output.Length > 100 ? output.Substring(0, 100) + "..." : output,
                    SubTitle = "✅ 按 Enter 复制到剪贴板",
                    IcoPath = "Images/success.png",
                    Action = context =>
                    {
                        Clipboard.SetText(output);
                        return true;
                    }
                });
            }
            else
            {
                // 成功但无输出
                results.Add(new Result
                {
                    Title = "✅ 执行成功",
                    SubTitle = "没有输出",
                    IcoPath = "Images/success.png"
                });
            }

            return results;
        }

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public string GetTranslatedPluginTitle()
        {
            return Resources.plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Resources.plugin_description;
        }

        private void OnThemeChanged(Theme oldTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                _iconPath = "Images/PythonRun.light.png";
            }
            else
            {
                _iconPath = "Images/PythonRun.dark.png";
            }
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void ReloadData()
        {
            if (_context is null)
            {
                return;
            }

            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                if (_context != null && _context.API != null)
                {
                    _context.API.ThemeChanged -= OnThemeChanged;
                }

                _disposed = true;
            }
        }
    }
}
