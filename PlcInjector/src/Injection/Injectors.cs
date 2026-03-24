using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PlcInjector.Models;

namespace PlcInjector.Injection;

// ── Win32 API ──────────────────────────────────────────────────────────────────
internal static class Win32
{
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, nint extra);
    [DllImport("user32.dll")] public static extern nint FindWindow(string? cls, string? title);
    [DllImport("user32.dll")] public static extern nint FindWindowEx(nint parent, nint after, string? cls, string? title);
    [DllImport("user32.dll")] public static extern int  SendMessage(nint hwnd, uint msg, nint wp, string lp);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc fn, nint lp);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern int GetWindowText(nint hwnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] ins, int size);

    public delegate bool EnumWindowsProc(nint hwnd, nint lp);

    public const uint MOUSEEVENTF_LEFTDOWN   = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP     = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN  = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP    = 0x0010;
    public const uint WM_SETTEXT             = 0x000C;
    public const uint WM_KEYDOWN             = 0x0100;
    public const uint WM_KEYUP               = 0x0101;
    public const int  VK_RETURN              = 0x0D;
    public const int  VK_A                   = 0x41;
    public const int  VK_CONTROL             = 0x11;

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)] public struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }
    [StructLayout(LayoutKind.Explicit)] public struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }
    [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public nint dwExtraInfo;
    }
    public const uint INPUT_KEYBOARD   = 1;
    public const uint KEYEVENTF_KEYUP  = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;
}

// ── 条件 & 变换求值 ────────────────────────────────────────────────────────────
public static class Evaluator
{
    public static bool EvalCondition(string expr, double value)
    {
        if (string.IsNullOrWhiteSpace(expr)) return true;
        // 简单表达式解析：支持 > < >= <= == != and or
        try
        {
            expr = expr.Replace("value", value.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
            return EvalBoolExpr(expr);
        }
        catch { return true; }
    }

    private static bool EvalBoolExpr(string expr)
    {
        expr = expr.Trim();
        // or
        int oi = FindOuterOp(expr, " or ");
        if (oi >= 0) return EvalBoolExpr(expr[..oi]) || EvalBoolExpr(expr[(oi + 4)..]);
        // and
        int ai = FindOuterOp(expr, " and ");
        if (ai >= 0) return EvalBoolExpr(expr[..ai]) && EvalBoolExpr(expr[(ai + 5)..]);
        // comparison
        foreach (var op in new[] { ">=", "<=", "!=", "==", ">", "<" })
        {
            int idx = expr.IndexOf(op, StringComparison.Ordinal);
            if (idx < 0) continue;
            if (!double.TryParse(expr[..idx].Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var l)) continue;
            if (!double.TryParse(expr[(idx + op.Length)..].Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var r)) continue;
            return op switch { ">=" => l >= r, "<=" => l <= r, "!=" => l != r,
                               "==" => l == r, ">"  => l > r,  "<"  => l < r, _ => true };
        }
        return true;
    }

    private static int FindOuterOp(string expr, string op)
    {
        int depth = 0;
        for (int i = 0; i < expr.Length - op.Length + 1; i++)
        {
            if (expr[i] == '(') depth++;
            else if (expr[i] == ')') depth--;
            else if (depth == 0 && expr[i..].StartsWith(op, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    public static string ApplyTransform(string expr, double value)
    {
        if (string.IsNullOrWhiteSpace(expr)) return value.ToString("G");
        // 支持简单格式化: {value:F2} 或 value.ToString("F2")
        try
        {
            if (expr.Contains("{value:") && expr.Contains("}"))
            {
                var fmt = expr[expr.IndexOf(':') + 1..expr.IndexOf('}')];
                return value.ToString(fmt);
            }
            if (expr.StartsWith("Math.Round(value,"))
            {
                var digits = int.Parse(expr.Split(',')[1].TrimEnd(')', ' '));
                return Math.Round(value, digits).ToString();
            }
            if (expr.Contains("/ 10") || expr.Contains("/10"))
                return (value / 10.0).ToString("G");
            return value.ToString("G");
        }
        catch { return value.ToString("G"); }
    }
}

// ── 屏幕坐标注入 ───────────────────────────────────────────────────────────────
public static class ScreenInjector
{
    public static void Inject(InjectionTarget t, string text)
    {
        if (t.ScreenX == 0 && t.ScreenY == 0)
            throw new InvalidOperationException("屏幕坐标未设置 (X=0, Y=0)");

        // 记录原始位置
        Win32.GetCursorPos(out var orig);

        try
        {
            // 移动到目标
            Win32.SetCursorPos(t.ScreenX, t.ScreenY);
            Thread.Sleep(80);

            // 点击
            switch (t.ClickMode)
            {
                case ClickType.Double:
                    Win32.mouse_event(Win32.MOUSEEVENTF_LEFTDOWN, t.ScreenX, t.ScreenY, 0, 0);
                    Win32.mouse_event(Win32.MOUSEEVENTF_LEFTUP,   t.ScreenX, t.ScreenY, 0, 0);
                    Thread.Sleep(80);
                    Win32.mouse_event(Win32.MOUSEEVENTF_LEFTDOWN, t.ScreenX, t.ScreenY, 0, 0);
                    Win32.mouse_event(Win32.MOUSEEVENTF_LEFTUP,   t.ScreenX, t.ScreenY, 0, 0);
                    break;
                case ClickType.Right:
                    Win32.mouse_event(Win32.MOUSEEVENTF_RIGHTDOWN, t.ScreenX, t.ScreenY, 0, 0);
                    Win32.mouse_event(Win32.MOUSEEVENTF_RIGHTUP,   t.ScreenX, t.ScreenY, 0, 0);
                    break;
                default:
                    Win32.mouse_event(Win32.MOUSEEVENTF_LEFTDOWN, t.ScreenX, t.ScreenY, 0, 0);
                    Win32.mouse_event(Win32.MOUSEEVENTF_LEFTUP,   t.ScreenX, t.ScreenY, 0, 0);
                    break;
            }

            Thread.Sleep(t.DelayMs);

            // Ctrl+A 全选清空
            if (t.ClearBefore)
            {
                SendKeyCombo(Win32.VK_CONTROL, Win32.VK_A);
                Thread.Sleep(50);
            }

            // 输入文字（Unicode SendInput，支持中文）
            TypeText(text);

            if (t.PressEnter) SendKey(Win32.VK_RETURN);
        }
        finally
        {
            if (t.RestoreMouse)
                Win32.SetCursorPos(orig.X, orig.Y);
        }
    }

    private static void TypeText(string text)
    {
        var inputs = new List<Win32.INPUT>();
        foreach (char c in text)
        {
            inputs.Add(new Win32.INPUT
            {
                type = Win32.INPUT_KEYBOARD,
                u = new Win32.INPUTUNION { ki = new Win32.KEYBDINPUT
                    { wVk = 0, wScan = c, dwFlags = Win32.KEYEVENTF_UNICODE } }
            });
            inputs.Add(new Win32.INPUT
            {
                type = Win32.INPUT_KEYBOARD,
                u = new Win32.INPUTUNION { ki = new Win32.KEYBDINPUT
                    { wVk = 0, wScan = c, dwFlags = Win32.KEYEVENTF_UNICODE | Win32.KEYEVENTF_KEYUP } }
            });
        }
        Win32.SendInput((uint)inputs.Count, inputs.ToArray(),
            Marshal.SizeOf(typeof(Win32.INPUT)));
    }

    private static void SendKey(int vk)
    {
        var ins = new Win32.INPUT[]
        {
            new() { type = Win32.INPUT_KEYBOARD,
                u = new() { ki = new() { wVk = (ushort)vk } } },
            new() { type = Win32.INPUT_KEYBOARD,
                u = new() { ki = new() { wVk = (ushort)vk, dwFlags = Win32.KEYEVENTF_KEYUP } } },
        };
        Win32.SendInput(2, ins, Marshal.SizeOf<Win32.INPUT>());
    }

    private static void SendKeyCombo(int modifier, int key)
    {
        var ins = new Win32.INPUT[]
        {
            new() { type=Win32.INPUT_KEYBOARD, u=new(){ki=new(){wVk=(ushort)modifier}} },
            new() { type=Win32.INPUT_KEYBOARD, u=new(){ki=new(){wVk=(ushort)key}} },
            new() { type=Win32.INPUT_KEYBOARD, u=new(){ki=new(){wVk=(ushort)key,      dwFlags=Win32.KEYEVENTF_KEYUP}} },
            new() { type=Win32.INPUT_KEYBOARD, u=new(){ki=new(){wVk=(ushort)modifier, dwFlags=Win32.KEYEVENTF_KEYUP}} },
        };
        Win32.SendInput(4, ins, Marshal.SizeOf<Win32.INPUT>());
    }
}

// ── 浏览器注入 (Playwright) ───────────────────────────────────────────────────
public static class BrowserInjector
{
    public static async Task InjectAsync(InjectionTarget t, string text)
    {
        // Playwright 需要在 STA 线程以外运行
        var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Microsoft.Playwright.IBrowserContext? ctx = null;
        Microsoft.Playwright.IBrowser? browser   = null;

        try
        {
            // 尝试 CDP 连接已打开的浏览器
            Microsoft.Playwright.IPage? page = null;
            foreach (var port in new[] { 9222, 9223, 9224 })
            {
                try
                {
                    browser = await playwright.Chromium.ConnectOverCDPAsync(
                        $"http://localhost:{port}", new() { Timeout = 800 });
                    var pages = browser.Contexts.SelectMany(c => c.Pages).ToList();
                    if (pages.Count == 0) { await browser.DisposeAsync(); browser = null; continue; }

                    page = pages.LastOrDefault(p =>
                        (!string.IsNullOrEmpty(t.TabTitle) && p.Title.Contains(t.TabTitle, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(t.BrowserUrl) && p.Url.Contains(t.BrowserUrl))) ?? pages.Last();
                    break;
                }
                catch { browser = null; }
            }

            if (page == null)
            {
                // 启动新窗口
                browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
                ctx  = await browser.NewContextAsync();
                page = await ctx.NewPageAsync();
                if (!string.IsNullOrEmpty(t.BrowserUrl))
                    await page.GotoAsync(t.BrowserUrl);
            }

            // 定位元素
            Microsoft.Playwright.ILocator? loc = null;
            if (!string.IsNullOrEmpty(t.CssSelector))
                loc = page.Locator(t.CssSelector).First;
            else if (!string.IsNullOrEmpty(t.XPath))
                loc = page.Locator($"xpath={t.XPath}").First;
            else
                throw new InvalidOperationException("未配置 CSS 选择器或 XPath");

            await loc.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 5000 });

            if (t.ClickBefore)
            {
                await loc.ClickAsync();
                await page.WaitForTimeoutAsync(t.DelayMs);
            }

            switch (t.Method)
            {
                case SendMethod.Fill:
                    await loc.FillAsync(text);
                    break;
                case SendMethod.Type:
                    if (t.ClearBefore) await loc.TripleClickAsync();
                    await loc.TypeAsync(text, new() { Delay = 30 });
                    break;
                case SendMethod.JsSet:
                    await page.EvaluateAsync(@"([el, val]) => {
                        const s = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set;
                        s.call(el, val);
                        el.dispatchEvent(new Event('input',  { bubbles: true }));
                        el.dispatchEvent(new Event('change', { bubbles: true }));
                    }", new object[] { (await loc.ElementHandleAsync())!, text });
                    break;
                default:
                    await loc.FillAsync(text);
                    break;
            }

            if (t.PressEnter) await loc.PressAsync("Enter");
        }
        finally
        {
            if (ctx    != null) await ctx.DisposeAsync();
            if (browser != null) await browser.DisposeAsync();
            playwright.Dispose();
        }
    }
}

// ── Win32 窗口注入 ─────────────────────────────────────────────────────────────
public static class WindowInjector
{
    public static void Inject(InjectionTarget t, string text)
    {
        var hwnd = FindTargetWindow(t);
        if (hwnd == nint.Zero)
            throw new InvalidOperationException($"找不到窗口: {t.WindowTitle ?? t.ProcessName}");

        var ctrl = hwnd;
        if (!string.IsNullOrEmpty(t.ClassName))
        {
            var child = Win32.FindWindowEx(hwnd, nint.Zero, t.ClassName, null);
            if (child != nint.Zero) ctrl = child;
        }

        if (t.ClickBefore)
        {
            Win32.SetForegroundWindow(ctrl);
            Thread.Sleep(t.DelayMs);
        }

        Win32.SendMessage(ctrl, Win32.WM_SETTEXT, nint.Zero, text);
        if (t.PressEnter)
            Win32.SendMessage(ctrl, Win32.WM_KEYDOWN, Win32.VK_RETURN, "");
    }

    private static nint FindTargetWindow(InjectionTarget t)
    {
        nint found = nint.Zero;
        Win32.EnumWindows((hwnd, _) =>
        {
            if (!Win32.IsWindowVisible(hwnd)) return true;
            var sb = new StringBuilder(256);
            Win32.GetWindowText(hwnd, sb, 256);
            var title = sb.ToString();

            if (!string.IsNullOrEmpty(t.WindowTitle) &&
                title.Contains(t.WindowTitle, StringComparison.OrdinalIgnoreCase))
            { found = hwnd; return false; }

            if (!string.IsNullOrEmpty(t.ProcessName))
            {
                Win32.GetWindowThreadProcessId(hwnd, out var pid);
                try
                {
                    var proc = Process.GetProcessById((int)pid);
                    if (proc.ProcessName.Contains(t.ProcessName, StringComparison.OrdinalIgnoreCase))
                    { found = hwnd; return false; }
                }
                catch { }
            }
            return true;
        }, nint.Zero);
        return found;
    }
}

// ── 剪贴板注入 ─────────────────────────────────────────────────────────────────
public static class ClipboardInjector
{
    public static void Inject(string text)
    {
        // 必须在 STA 线程设置剪贴板
        var tcs = new TaskCompletionSource();
        var t = new Thread(() =>
        {
            try { Clipboard.SetText(text); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        tcs.Task.GetAwaiter().GetResult();
        SendKeys.SendWait("^v");
    }
}
