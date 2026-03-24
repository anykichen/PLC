using PlcInjector.Injection;
using PlcInjector.Models;
using PlcInjector.Plc;

namespace PlcInjector.Core;

public class LogEventArgs(LogEntry entry) : EventArgs
{
    public LogEntry Entry { get; } = entry;
}

public class RuleStatusEventArgs(string ruleId, string status, string msg = "") : EventArgs
{
    public string RuleId { get; } = ruleId;
    public string Status { get; } = status;
    public string Msg    { get; } = msg;
}

// ── 单条规则的轮询任务 ─────────────────────────────────────────────────────────
public class RuleRunner
{
    private readonly Rule              _rule;
    private CancellationTokenSource?   _cts;
    private Task?                      _task;

    public event EventHandler<LogEventArgs>?       OnLog;
    public event EventHandler<RuleStatusEventArgs>? OnStatus;

    public RuleRunner(Rule rule) { _rule = rule; }

    public bool IsRunning => _task is { IsCompleted: false };

    public void Start()
    {
        if (IsRunning) return;
        _cts  = new CancellationTokenSource();
        _task = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _task = null;
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        IPlcClient? plc = null;
        double? prevValue = null;
        DateTime lastInject = DateTime.MinValue;
        DateTime lastHeartbeat = DateTime.UtcNow;

        SetStatus("running");
        try
        {
            plc = PlcFactory.Create(_rule.Plc);
            await plc.ConnectAsync();

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(_rule.PollIntervalMs, ct);

                try
                {
                    var hs = _rule.Handshake;

                    // ── 心跳检查 ──────────────────────────────────────────────
                    if (hs.Enabled && !string.IsNullOrEmpty(hs.HeartbeatAddr))
                    {
                        var hb = await plc.ReadWordAsync(hs.HeartbeatAddr);
                        if (hb != 0)
                        {
                            lastHeartbeat = DateTime.UtcNow;
                            await plc.WriteWordAsync(hs.HeartbeatAddr, 0);
                        }
                        else if ((DateTime.UtcNow - lastHeartbeat).TotalSeconds > hs.MaxSilenceSec)
                            throw new TimeoutException($"心跳超时 {hs.MaxSilenceSec}s，PLC 可能离线");
                    }

                    // ── UpdateFlag ────────────────────────────────────────────
                    if (hs.Enabled && !string.IsNullOrEmpty(hs.UpdateFlagAddr))
                    {
                        var flag = await plc.ReadWordAsync(hs.UpdateFlagAddr);
                        if (flag != hs.UpdateFlagValue) continue;
                    }

                    // ── 读取数据 ──────────────────────────────────────────────
                    var raw = await plc.ReadWordAsync(_rule.Address);
                    var value = (double)raw;
                    _rule.LastValue = raw.ToString();

                    // ── 条件过滤 ──────────────────────────────────────────────
                    if (!Evaluator.EvalCondition(_rule.Condition.Expression, value))
                    {
                        if (hs.Enabled && !string.IsNullOrEmpty(hs.UpdateFlagAddr))
                            await plc.WriteWordAsync(hs.UpdateFlagAddr, hs.ResetValue);
                        continue;
                    }

                    // ── 去抖 ──────────────────────────────────────────────────
                    var now = DateTime.UtcNow;
                    var debounce = TimeSpan.FromMilliseconds(_rule.DebounceMs);
                    if (prevValue.HasValue && Math.Abs(value - prevValue.Value) < 1e-9
                        && (now - lastInject) < debounce)
                    {
                        if (hs.Enabled && !string.IsNullOrEmpty(hs.UpdateFlagAddr))
                            await plc.WriteWordAsync(hs.UpdateFlagAddr, hs.ResetValue);
                        continue;
                    }

                    // ── 注入 ──────────────────────────────────────────────────
                    var text = Evaluator.ApplyTransform(_rule.Condition.Transform, value);
                    await DispatchInjectAsync(_rule.Target, text);

                    prevValue   = value;
                    lastInject  = now;
                    _rule.LastInject = DateTime.Now.ToString("HH:mm:ss");
                    _rule.ErrorMsg   = "";

                    var entry = new LogEntry
                    {
                        RuleName = _rule.Name,
                        Value    = raw.ToString(),
                        Text     = text,
                        Ok       = true
                    };
                    OnLog?.Invoke(this, new LogEventArgs(entry));
                    SetStatus("running", "");

                    // ── ACK UpdateFlag ────────────────────────────────────────
                    if (hs.Enabled && !string.IsNullOrEmpty(hs.UpdateFlagAddr))
                        await plc.WriteWordAsync(hs.UpdateFlagAddr, hs.ResetValue);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _rule.ErrorMsg = ex.Message;
                    OnLog?.Invoke(this, new LogEventArgs(new LogEntry
                    {
                        RuleName = _rule.Name, Ok = false, Error = ex.Message
                    }));
                    SetStatus("error", ex.Message);
                    await Task.Delay(3000, ct);
                    SetStatus("running");

                    // 重连
                    try { plc.Dispose(); plc = PlcFactory.Create(_rule.Plc); await plc.ConnectAsync(); }
                    catch { }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetStatus("error", ex.Message);
        }
        finally
        {
            plc?.Dispose();
            SetStatus("idle");
        }
    }

    private static async Task DispatchInjectAsync(InjectionTarget t, string text)
    {
        switch (t.Type)
        {
            case TargetType.Screen:
                await Task.Run(() => ScreenInjector.Inject(t, text));
                break;
            case TargetType.Browser:
                await BrowserInjector.InjectAsync(t, text);
                break;
            case TargetType.Window:
                await Task.Run(() => WindowInjector.Inject(t, text));
                break;
            case TargetType.Clipboard:
                await Task.Run(() => ClipboardInjector.Inject(text));
                break;
        }
    }

    private void SetStatus(string s, string msg = "")
    {
        _rule.Status = s;
        OnStatus?.Invoke(this, new RuleStatusEventArgs(_rule.Id, s, msg));
    }
}

// ── 规则管理器 ─────────────────────────────────────────────────────────────────
public class RuleManager
{
    private readonly Dictionary<string, RuleRunner> _runners = new();
    public List<Rule> Rules { get; } = new();
    public List<LogEntry> Log { get; } = new();

    public event EventHandler<LogEventArgs>?        OnLog;
    public event EventHandler<RuleStatusEventArgs>?  OnStatus;

    public void AddRule(Rule rule)
    {
        Rules.Add(rule);
        var runner = new RuleRunner(rule);
        runner.OnLog    += (_, e) => { AddLog(e.Entry); OnLog?.Invoke(this, e); };
        runner.OnStatus += (_, e) => OnStatus?.Invoke(this, e);
        _runners[rule.Id] = runner;
    }

    public void RemoveRule(string id)
    {
        StopRule(id);
        _runners.Remove(id);
        Rules.RemoveAll(r => r.Id == id);
    }

    public void StartRule(string id)  { if (_runners.TryGetValue(id, out var r)) r.Start(); }
    public void StopRule(string id)   { if (_runners.TryGetValue(id, out var r)) r.Stop(); }
    public bool IsRunning(string id)  => _runners.TryGetValue(id, out var r) && r.IsRunning;

    public void StartAll() { foreach (var (id, r) in _runners) if (Rules.First(x => x.Id == id).Enabled) r.Start(); }
    public void StopAll()  { foreach (var r in _runners.Values) r.Stop(); }

    private void AddLog(LogEntry e) { Log.Insert(0, e); if (Log.Count > 500) Log.RemoveAt(500); }

    // ── 测试单次注入 ───────────────────────────────────────────────────────────
    public async Task<(bool ok, string val, string err)> TestOnceAsync(string id)
    {
        var rule = Rules.FirstOrDefault(r => r.Id == id);
        if (rule == null) return (false, "", "规则不存在");
        try
        {
            using var plc = PlcFactory.Create(rule.Plc);
            await plc.ConnectAsync();
            var raw   = await plc.ReadWordAsync(rule.Address);
            var text  = Evaluator.ApplyTransform(rule.Condition.Transform, raw);
            await DispatchInjectAsync(rule.Target, text);
            return (true, raw.ToString(), "");
        }
        catch (Exception ex) { return (false, "", ex.Message); }
    }

    public async Task<(bool ok, string val, string err)> PingPlcAsync(string id)
    {
        var rule = Rules.FirstOrDefault(r => r.Id == id);
        if (rule == null) return (false, "", "规则不存在");
        try
        {
            using var plc = PlcFactory.Create(rule.Plc);
            await plc.ConnectAsync();
            var raw = await plc.ReadWordAsync(rule.Address);
            return (true, raw.ToString(), "");
        }
        catch (Exception ex) { return (false, "", ex.Message); }
    }

    private static async Task DispatchInjectAsync(InjectionTarget t, string text)
    {
        switch (t.Type)
        {
            case TargetType.Screen:    await Task.Run(() => ScreenInjector.Inject(t, text)); break;
            case TargetType.Browser:   await BrowserInjector.InjectAsync(t, text); break;
            case TargetType.Window:    await Task.Run(() => WindowInjector.Inject(t, text)); break;
            case TargetType.Clipboard: await Task.Run(() => ClipboardInjector.Inject(text)); break;
        }
    }
}
