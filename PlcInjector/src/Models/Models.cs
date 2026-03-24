using Newtonsoft.Json;

namespace PlcInjector.Models;

// ── PLC 配置 ───────────────────────────────────────────────────────────────────
public class PlcConfig
{
    public string Brand { get; set; } = "keyence_kv"; // keyence_kv | mock
    public string Ip    { get; set; } = "192.168.1.10";
    public int    Port  { get; set; } = 502;
    public int    Unit  { get; set; } = 1;            // Modbus slave id
}

// ── UpdateFlag 握手 ────────────────────────────────────────────────────────────
public class HandshakeConfig
{
    public bool   Enabled          { get; set; } = false;
    public string HeartbeatAddr    { get; set; } = "";   // e.g. DM0200
    public string UpdateFlagAddr   { get; set; } = "";   // e.g. DM0201
    public int    UpdateFlagValue  { get; set; } = 1;    // 触发值
    public int    ResetValue       { get; set; } = 0;    // ACK 写入值
    public int    MaxSilenceSec    { get; set; } = 60;
}

// ── 注入目标 ───────────────────────────────────────────────────────────────────
public enum TargetType { Screen, Browser, Window, Clipboard }
public enum ClickType  { Single, Double, Right }
public enum SendMethod { Fill, Type, JsSet, Clipboard, Uia, Win32 }

public class InjectionTarget
{
    public TargetType  Type         { get; set; } = TargetType.Screen;

    // Screen
    public int         ScreenX      { get; set; } = 0;
    public int         ScreenY      { get; set; } = 0;
    public ClickType   ClickMode    { get; set; } = ClickType.Single;
    public bool        RestoreMouse { get; set; } = false;

    // Browser (Playwright)
    public string      BrowserMode  { get; set; } = "active_tab"; // active_tab|cdp|new_window
    public string      BrowserUrl   { get; set; } = "";
    public string      CssSelector  { get; set; } = "";
    public string      XPath        { get; set; } = "";
    public string      TabTitle     { get; set; } = "";

    // Window (UIAutomation / Win32)
    public string      ProcessName  { get; set; } = "";
    public string      WindowTitle  { get; set; } = "";
    public string      AutomationId { get; set; } = "";
    public string      ClassName    { get; set; } = "";

    // Shared
    public SendMethod  Method       { get; set; } = SendMethod.Fill;
    public bool        PressEnter   { get; set; } = false;
    public bool        ClickBefore  { get; set; } = true;
    public bool        ClearBefore  { get; set; } = true;
    public int         DelayMs      { get; set; } = 200;
}

// ── 条件 & 变换 ────────────────────────────────────────────────────────────────
public class ConditionConfig
{
    public string Expression { get; set; } = "";  // e.g. "value > 0"
    public string Transform  { get; set; } = "";  // e.g. "Math.Round(value/10,2).ToString()"
}

// ── 规则 ───────────────────────────────────────────────────────────────────────
public class Rule
{
    public string          Id             { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string          Name           { get; set; } = "新规则";
    public bool            Enabled        { get; set; } = true;
    public PlcConfig       Plc            { get; set; } = new();
    public string          Address        { get; set; } = "DM0000";
    public int             DataLength     { get; set; } = 1;
    public int             PollIntervalMs { get; set; } = 500;
    public int             DebounceMs     { get; set; } = 200;
    public HandshakeConfig Handshake      { get; set; } = new();
    public InjectionTarget Target         { get; set; } = new();
    public ConditionConfig Condition      { get; set; } = new();

    // ── 运行时（不持久化）──────────────────────────────────────────────────────
    [JsonIgnore] public string  Status      { get; set; } = "idle"; // idle|running|error
    [JsonIgnore] public string  LastValue   { get; set; } = "—";
    [JsonIgnore] public string  LastInject  { get; set; } = "—";
    [JsonIgnore] public string  ErrorMsg    { get; set; } = "";
}

// ── 日志条目 ───────────────────────────────────────────────────────────────────
public class LogEntry
{
    public DateTime Ts       { get; set; } = DateTime.Now;
    public string   RuleName { get; set; } = "";
    public string   Value    { get; set; } = "";
    public string   Text     { get; set; } = "";
    public bool     Ok       { get; set; } = true;
    public string   Error    { get; set; } = "";
}
