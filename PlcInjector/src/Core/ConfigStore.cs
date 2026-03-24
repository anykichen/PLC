using Newtonsoft.Json;
using PlcInjector.Models;

namespace PlcInjector.Core;

public static class ConfigStore
{
    private static readonly string ConfigDir  = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "config");
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "rules.json");

    public static List<Rule> Load()
    {
        try
        {
            if (!File.Exists(ConfigFile)) return DefaultRules();
            var json = File.ReadAllText(ConfigFile);
            return JsonConvert.DeserializeObject<List<Rule>>(json) ?? DefaultRules();
        }
        catch { return DefaultRules(); }
    }

    public static void Save(IEnumerable<Rule> rules)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonConvert.SerializeObject(rules, Formatting.Indented);
        File.WriteAllText(ConfigFile, json);
    }

    private static List<Rule> DefaultRules() => new()
    {
        new Rule
        {
            Name    = "示例：模拟器 → 屏幕坐标",
            Enabled = false,
            Plc     = new PlcConfig { Brand = "mock" },
            Address = "DM0000",
            Target  = new InjectionTarget
            {
                Type      = TargetType.Screen,
                ScreenX   = 800,
                ScreenY   = 400,
                ClickMode = ClickType.Single,
            },
            Condition = new ConditionConfig { Expression = "value > 0" }
        },
        new Rule
        {
            Name    = "示例：KV-8000 UpdateFlag → 浏览器",
            Enabled = false,
            Plc     = new PlcConfig { Brand = "keyence_kv", Ip = "192.168.1.10", Port = 502 },
            Address = "DM0202",
            Handshake = new HandshakeConfig
            {
                Enabled         = true,
                HeartbeatAddr   = "DM0200",
                UpdateFlagAddr  = "DM0201",
                UpdateFlagValue = 1,
                ResetValue      = 0,
                MaxSilenceSec   = 60,
            },
            Target = new InjectionTarget
            {
                Type        = TargetType.Browser,
                BrowserMode = "active_tab",
                CssSelector = "#weightInput",
                Method      = SendMethod.Fill,
                PressEnter  = false,
            },
            Condition = new ConditionConfig { Expression = "" }
        }
    };
}
