using PlcInjector.Core;
using PlcInjector.Models;
using PlcInjector.UI;

namespace PlcInjector;

public class MainForm : Form
{
    private readonly RuleManager _mgr = new();

    // ── UI 控件 ───────────────────────────────────────────────────────────────
    private ListView        lvRules   = null!;
    private RichTextBox     rtbLog    = null!;
    private NotifyIcon      trayIcon  = null!;
    private StatusStrip     statusBar = null!;
    private ToolStripStatusLabel lblStatus = null!;
    private System.Windows.Forms.Timer uiTimer = null!;

    // ── 颜色常量 ──────────────────────────────────────────────────────────────
    static readonly Color BgMain   = Color.FromArgb(13, 15, 18);
    static readonly Color BgPanel  = Color.FromArgb(20, 23, 32);
    static readonly Color BgItem   = Color.FromArgb(28, 32, 48);
    static readonly Color ColText  = Color.FromArgb(212, 216, 240);
    static readonly Color ColMuted = Color.FromArgb(84, 92, 122);
    static readonly Color ColGreen = Color.FromArgb(39, 199, 122);
    static readonly Color ColRed   = Color.FromArgb(255, 77, 106);
    static readonly Color ColAmber = Color.FromArgb(245, 166, 35);
    static readonly Color ColBlue  = Color.FromArgb(61, 127, 255);

    public MainForm()
    {
        InitializeComponent();
        LoadRules();
        SetupTimer();
        _mgr.OnLog    += OnLog;
        _mgr.OnStatus += OnRuleStatus;
    }

    private void InitializeComponent()
    {
        Text            = "PLC Injector Pro  v1.3";
        Size            = new Size(1100, 700);
        MinimumSize     = new Size(900, 600);
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = BgMain;
        ForeColor       = ColText;
        Font            = new Font("Microsoft YaHei UI", 9f);
        Icon            = SystemIcons.Application;

        BuildToolbar();
        BuildSplitLayout();
        BuildStatusBar();
        BuildTrayIcon();
        FormClosing += (_, e) => { e.Cancel = true; Hide(); trayIcon.Visible = true; };
    }

    // ── 工具栏 ────────────────────────────────────────────────────────────────
    private void BuildToolbar()
    {
        var tb = new ToolStrip { BackColor = BgPanel, ForeColor = ColText,
                                  GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(4) };
        Controls.Add(tb);

        ToolStripButton Btn(string text, Color bg, EventHandler click)
        {
            var b = new ToolStripButton(text) { BackColor = bg, ForeColor = Color.White,
                DisplayStyle = ToolStripItemDisplayStyle.Text, Margin = new Padding(2),
                Font = new Font("Microsoft YaHei UI", 9f) };
            b.Click += click;
            return b;
        }

        tb.Items.Add(Btn("＋ 新建规则",  ColorUtils.Darker(ColBlue,  0.2f), (_, _) => NewRule()));
        tb.Items.Add(Btn("✎ 编辑规则",  ColorUtils.Darker(BgPanel,  -0.1f),(_, _) => EditRule()));
        tb.Items.Add(Btn("✕ 删除规则",  ColorUtils.Darker(ColRed,   0.3f), (_, _) => DeleteRule()));
        tb.Items.Add(new ToolStripSeparator());
        tb.Items.Add(Btn("▶ 全部启动",  ColorUtils.Darker(ColGreen, 0.2f), (_, _) => { _mgr.StartAll(); RefreshList(); }));
        tb.Items.Add(Btn("■ 全部停止",  ColorUtils.Darker(ColRed,   0.2f), (_, _) => { _mgr.StopAll();  RefreshList(); }));
        tb.Items.Add(new ToolStripSeparator());
        tb.Items.Add(Btn("📡 Ping PLC",  ColorUtils.Darker(ColAmber, 0.2f), async (_, _) => await PingSelected()));
        tb.Items.Add(Btn("⚡ 测试注入",  ColorUtils.Darker(ColAmber, 0.1f), async (_, _) => await TestSelected()));
        tb.Items.Add(new ToolStripSeparator());
        tb.Items.Add(Btn("🗑 清空日志",  ColorUtils.Darker(BgPanel,  0f),   (_, _) => { rtbLog.Clear(); _mgr.Log.Clear(); }));
    }

    // ── 主分割布局 ────────────────────────────────────────────────────────────
    private void BuildSplitLayout()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
            BackColor = BgMain, BorderStyle = BorderStyle.None,
            SplitterDistance = 380, Panel1MinSize = 200, Panel2MinSize = 100
        };
        Controls.Add(split);
        split.BringToFront();

        // ── 规则列表 ──────────────────────────────────────────────────────────
        lvRules = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
            BackColor = BgPanel, ForeColor = ColText, GridLines = false,
            BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9f),
            OwnerDraw = true
        };
        lvRules.Columns.Add("状态",   50);
        lvRules.Columns.Add("名称",  180);
        lvRules.Columns.Add("PLC",   120);
        lvRules.Columns.Add("地址",   80);
        lvRules.Columns.Add("目标",   80);
        lvRules.Columns.Add("当前值",  80);
        lvRules.Columns.Add("最后注入",100);
        lvRules.Columns.Add("错误",   200);
        lvRules.DrawColumnHeader += (s, e) =>
        {
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(25, 28, 40)), e.Bounds);
            e.Graphics.DrawString(e.Header.Text, Font, new SolidBrush(ColMuted), e.Bounds.X + 4, e.Bounds.Y + 4);
        };
        lvRules.DrawItem += (_, e) => { };
        lvRules.DrawSubItem += (_, e) =>
        {
            var rule = _mgr.Rules.ElementAtOrDefault(e.ItemIndex);
            if (rule == null) return;
            var bg = e.ItemIndex % 2 == 0 ? BgItem : BgPanel;
            if (e.Item.Selected) bg = Color.FromArgb(35, 60, 100);
            e.Graphics.FillRectangle(new SolidBrush(bg), e.Bounds);
            Color fg = e.ColumnIndex == 0 ? rule.Status switch
            {
                "running" => ColGreen, "error" => ColRed, _ => ColMuted
            } : ColText;
            var text = e.ColumnIndex == 0 ? rule.Status switch
            {
                "running"=>"● 运行中", "error"=>"● 错误", "idle"=>"○ 停止", _=>"○ 停止"
            } : e.SubItem?.Text ?? "";
            if (e.ColumnIndex == 7 && !string.IsNullOrEmpty(rule.ErrorMsg)) fg = ColRed;
            e.Graphics.DrawString(text, Font, new SolidBrush(fg), e.Bounds.X + 3, e.Bounds.Y + 3);
        };
        lvRules.DoubleClick += (_, _) => EditRule();
        split.Panel1.Controls.Add(lvRules);
        split.Panel1.Controls.Add(new Label
        {
            Text = "规则列表  (双击编辑)", Dock = DockStyle.Top, Height = 22,
            BackColor = Color.FromArgb(16, 19, 28), ForeColor = ColMuted,
            Font = new Font("Microsoft YaHei UI", 8f), TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0)
        });

        // ── 日志面板 ──────────────────────────────────────────────────────────
        rtbLog = new RichTextBox
        {
            Dock = DockStyle.Fill, BackColor = BgPanel, ForeColor = ColText,
            BorderStyle = BorderStyle.None, ReadOnly = true, WordWrap = false,
            Font = new Font("Consolas", 8.5f), ScrollBars = RichTextBoxScrollBars.Both
        };
        split.Panel2.Controls.Add(rtbLog);
        split.Panel2.Controls.Add(new Label
        {
            Text = "实时日志", Dock = DockStyle.Top, Height = 22,
            BackColor = Color.FromArgb(16, 19, 28), ForeColor = ColMuted,
            Font = new Font("Microsoft YaHei UI", 8f), TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0)
        });
    }

    // ── 状态栏 ────────────────────────────────────────────────────────────────
    private void BuildStatusBar()
    {
        statusBar = new StatusStrip { BackColor = Color.FromArgb(16, 19, 28), ForeColor = ColMuted };
        lblStatus = new ToolStripStatusLabel("就绪") { ForeColor = ColMuted };
        statusBar.Items.Add(lblStatus);
        statusBar.Items.Add(new ToolStripStatusLabel { Spring = true });
        statusBar.Items.Add(new ToolStripStatusLabel("PLC Injector Pro v1.3") { ForeColor = ColMuted });
        Controls.Add(statusBar);
    }

    // ── 系统托盘 ──────────────────────────────────────────────────────────────
    private void BuildTrayIcon()
    {
        trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application, Visible = false,
            Text = "PLC Injector Pro"
        };
        var ctx = new ContextMenuStrip();
        ctx.Items.Add("显示主窗口",  null, (_, _) => ShowMain());
        ctx.Items.Add("全部启动",    null, (_, _) => { _mgr.StartAll(); RefreshList(); });
        ctx.Items.Add("全部停止",    null, (_, _) => { _mgr.StopAll();  RefreshList(); });
        ctx.Items.Add(new ToolStripSeparator());
        ctx.Items.Add("退出",        null, (_, _) => ExitApp());
        trayIcon.ContextMenuStrip = ctx;
        trayIcon.DoubleClick      += (_, _) => ShowMain();
        Application.ApplicationExit += (_, _) => trayIcon.Visible = false;
    }

    private void ShowMain() { Show(); WindowState = FormWindowState.Normal; Activate(); trayIcon.Visible = false; }
    private void ExitApp()  { _mgr.StopAll(); trayIcon.Visible = false; Application.Exit(); }

    // ── 定时刷新 ──────────────────────────────────────────────────────────────
    private void SetupTimer()
    {
        uiTimer = new System.Windows.Forms.Timer { Interval = 800 };
        uiTimer.Tick += (_, _) => RefreshList();
        uiTimer.Start();
    }

    // ── 规则列表刷新 ──────────────────────────────────────────────────────────
    private void RefreshList()
    {
        if (InvokeRequired) { Invoke(RefreshList); return; }
        lvRules.BeginUpdate();
        while (lvRules.Items.Count > _mgr.Rules.Count) lvRules.Items.RemoveAt(lvRules.Items.Count - 1);
        for (int i = 0; i < _mgr.Rules.Count; i++)
        {
            var r = _mgr.Rules[i];
            var sub = new[] { "", r.Name, $"{r.Plc.Brand}  {r.Plc.Ip}", r.Address,
                              r.Target.Type.ToString(), r.LastValue, r.LastInject, r.ErrorMsg };
            if (i < lvRules.Items.Count)
            {
                var item = lvRules.Items[i];
                for (int j = 0; j < sub.Length; j++)
                    if (j < item.SubItems.Count) item.SubItems[j].Text = sub[j];
                    else item.SubItems.Add(sub[j]);
            }
            else
            {
                var item = new ListViewItem(sub);
                lvRules.Items.Add(item);
            }
        }
        lvRules.EndUpdate();
        var running = _mgr.Rules.Count(r => r.Status == "running");
        lblStatus.Text = $"规则总数: {_mgr.Rules.Count}   运行中: {running}   日志: {_mgr.Log.Count}";
    }

    // ── 日志输出 ──────────────────────────────────────────────────────────────
    private void OnLog(object? sender, LogEventArgs e)
    {
        if (InvokeRequired) { Invoke(() => OnLog(sender, e)); return; }
        var entry = e.Entry;
        var color = entry.Ok ? ColGreen : ColRed;
        var ts    = entry.Ts.ToString("HH:mm:ss.fff");
        var msg   = entry.Ok
            ? $"[{ts}] ✓ {entry.RuleName,-20} {entry.Value,-10} → \"{entry.Text}\"\n"
            : $"[{ts}] ✗ {entry.RuleName,-20} {entry.Error}\n";

        rtbLog.SelectionStart  = rtbLog.TextLength;
        rtbLog.SelectionLength = 0;
        rtbLog.SelectionColor  = color;
        rtbLog.AppendText(msg);
        rtbLog.SelectionColor  = rtbLog.ForeColor;
        if (rtbLog.Lines.Length > 1000) { rtbLog.Clear(); }
        rtbLog.ScrollToCaret();
    }

    private void OnRuleStatus(object? sender, RuleStatusEventArgs e)
    {
        if (InvokeRequired) { Invoke(() => OnRuleStatus(sender, e)); return; }
        RefreshList();
        if (e.Status == "error")
            AppendLog($"[错误] {e.RuleId}: {e.Msg}\n", ColRed);
    }

    private void AppendLog(string msg, Color color)
    {
        rtbLog.SelectionStart = rtbLog.TextLength;
        rtbLog.SelectionColor = color;
        rtbLog.AppendText(msg);
        rtbLog.SelectionColor = rtbLog.ForeColor;
        rtbLog.ScrollToCaret();
    }

    // ── 规则操作 ──────────────────────────────────────────────────────────────
    private void LoadRules()
    {
        var rules = ConfigStore.Load();
        foreach (var r in rules) _mgr.AddRule(r);
        RefreshList();
        AppendLog($"[{DateTime.Now:HH:mm:ss}] 已加载 {rules.Count} 条规则\n", ColMuted);
    }

    private void SaveRules() => ConfigStore.Save(_mgr.Rules);

    private void NewRule()
    {
        using var dlg = new RuleEditDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _mgr.AddRule(dlg.GetRule());
        SaveRules(); RefreshList();
    }

    private void EditRule()
    {
        if (lvRules.SelectedIndices.Count == 0) return;
        var rule = _mgr.Rules[lvRules.SelectedIndices[0]];
        var wasRunning = _mgr.IsRunning(rule.Id);
        if (wasRunning) _mgr.StopRule(rule.Id);
        using var dlg = new RuleEditDialog(rule);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            if (wasRunning && rule.Enabled) _mgr.StartRule(rule.Id);
            SaveRules(); RefreshList();
        }
        else if (wasRunning) _mgr.StartRule(rule.Id);
    }

    private void DeleteRule()
    {
        if (lvRules.SelectedIndices.Count == 0) return;
        var rule = _mgr.Rules[lvRules.SelectedIndices[0]];
        if (MessageBox.Show($"确认删除规则「{rule.Name}」？", "删除确认",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _mgr.RemoveRule(rule.Id);
        SaveRules(); RefreshList();
    }

    private async Task PingSelected()
    {
        if (lvRules.SelectedIndices.Count == 0) { MessageBox.Show("请先选择一条规则"); return; }
        var rule = _mgr.Rules[lvRules.SelectedIndices[0]];
        lblStatus.Text = $"正在 Ping {rule.Plc.Ip}...";
        var (ok, val, err) = await _mgr.PingPlcAsync(rule.Id);
        if (ok) MessageBox.Show($"PLC 连接正常\n地址 {rule.Address} = {val}", "Ping 成功",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        else    MessageBox.Show($"PLC 连接失败\n{err}", "Ping 失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task TestSelected()
    {
        if (lvRules.SelectedIndices.Count == 0) { MessageBox.Show("请先选择一条规则"); return; }
        var rule = _mgr.Rules[lvRules.SelectedIndices[0]];
        lblStatus.Text = "测试注入中...";
        var (ok, val, err) = await _mgr.TestOnceAsync(rule.Id);
        if (ok) AppendLog($"[{DateTime.Now:HH:mm:ss}] ⚡ 测试成功  {rule.Name}: {val}\n", ColAmber);
        else    AppendLog($"[{DateTime.Now:HH:mm:ss}] ✗ 测试失败  {rule.Name}: {err}\n", ColRed);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _mgr.StopAll();
        SaveRules();
        base.OnFormClosed(e);
    }
}

// ── 颜色工具 ──────────────────────────────────────────────────────────────────
internal static class ColorUtils
{
    public static Color Darker(Color c, float amount) =>
        Color.FromArgb(c.A, Clamp(c.R - (int)(amount * 255)),
                           Clamp(c.G - (int)(amount * 255)),
                           Clamp(c.B - (int)(amount * 255)));
    static int Clamp(int v) => Math.Max(0, Math.Min(255, v));
}
