using PlcInjector.Models;

namespace PlcInjector.UI;

public class RuleEditDialog : Form
{
    private readonly Rule _rule;
    private bool _isNew;

    // Controls
    private TextBox txtName = null!;
    private NumericUpDown numPoll = null!, numDebounce = null!;
    // PLC
    private ComboBox cmbBrand = null!;
    private TextBox txtIp = null!, txtAddr = null!;
    private NumericUpDown numPort = null!, numUnit = null!, numDataLen = null!;
    // Handshake
    private CheckBox chkHs = null!;
    private TextBox txtHbAddr = null!, txtFlagAddr = null!;
    private NumericUpDown numFlagVal = null!, numResetVal = null!, numSilence = null!;
    // Target
    private ComboBox cmbTargetType = null!, cmbClickMode = null!, cmbMethod = null!;
    private NumericUpDown numSx = null!, numSy = null!, numDelay = null!;
    private CheckBox chkRestoreMouse = null!, chkClickBefore = null!, chkClearBefore = null!, chkPressEnter = null!;
    private TextBox txtCss = null!, txtXpath = null!, txtTabTitle = null!, txtBrowserUrl = null!;
    private TextBox txtProcName = null!, txtWinTitle = null!, txtAutoId = null!, txtClassName = null!;
    private ComboBox cmbBrowserMode = null!;
    // Condition
    private TextBox txtExpr = null!, txtTransform = null!;
    // Panels
    private Panel pnlScreen = null!, pnlBrowser = null!, pnlWindow = null!;

    public RuleEditDialog(Rule? rule = null)
    {
        _isNew = rule == null;
        _rule  = rule ?? new Rule();
        BuildUI();
        LoadValues();
    }

    private void BuildUI()
    {
        Text            = _isNew ? "新建规则" : $"编辑规则 — {_rule.Name}";
        Size            = new Size(600, 780);
        MinimumSize     = new Size(560, 700);
        StartPosition   = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        BackColor       = Color.FromArgb(20, 23, 32);
        ForeColor       = Color.FromArgb(212, 216, 240);
        Font            = new Font("Microsoft YaHei UI", 9f);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BackColor };
        Controls.Add(scroll);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(12),
            BackColor = BackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scroll.Controls.Add(layout);

        void AddSection(string title) =>
            layout.Controls.Add(MakeSection(title));

        Control Row(params Control[] ctrls)
        {
            var p = new FlowLayoutPanel
            {
                AutoSize = true, Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight,
                BackColor = BackColor, Margin = new Padding(0, 0, 0, 4)
            };
            p.Controls.AddRange(ctrls);
            return p;
        }

        // ── 基本信息 ──────────────────────────────────────────────────────────
        AddSection("基本信息");
        txtName    = MakeTextBox(350); numPoll    = MakeNum(1, 60000, 500);
        numDebounce = MakeNum(0, 5000, 200);
        layout.Controls.Add(Row(MakeLabel("规则名称"), txtName));
        layout.Controls.Add(Row(MakeLabel("轮询间隔(ms)"), numPoll,
                                MakeLabel("去抖延迟(ms)", 8), numDebounce));

        // ── PLC 连接 ──────────────────────────────────────────────────────────
        AddSection("📡 PLC 连接");
        cmbBrand  = MakeCombo(new[]{"mock — 模拟器", "keyence_kv — 基恩士 KV-8000"}, 200);
        txtIp     = MakeTextBox(140); numPort = MakeNum(1, 65535, 502);
        numUnit   = MakeNum(1, 247, 1); txtAddr = MakeTextBox(100);
        numDataLen = MakeNum(1, 100, 1);
        cmbBrand.SelectedIndexChanged += (_, _) => txtIp.Enabled = !cmbBrand.Text.StartsWith("mock");
        layout.Controls.Add(Row(MakeLabel("品牌"), cmbBrand));
        layout.Controls.Add(Row(MakeLabel("IP 地址"), txtIp,
                                MakeLabel("端口", 8), numPort, MakeLabel("Unit", 8), numUnit));
        layout.Controls.Add(Row(MakeLabel("寄存器地址"), txtAddr,
                                MakeLabel("读取字数", 8), numDataLen));

        // ── 握手协议 ──────────────────────────────────────────────────────────
        AddSection("🤝 UpdateFlag 握手协议");
        chkHs = MakeCheck("启用握手（PLC 置 Flag=1 → PC 读写 → PC 清零）");
        txtHbAddr   = MakeTextBox(90); txtFlagAddr = MakeTextBox(90);
        numFlagVal  = MakeNum(0, 9999, 1); numResetVal = MakeNum(0, 9999, 0);
        numSilence  = MakeNum(5, 3600, 60);
        layout.Controls.Add(chkHs);
        layout.Controls.Add(Row(MakeLabel("心跳地址(可选)"), txtHbAddr,
                                MakeLabel("超时(s)", 8), numSilence));
        layout.Controls.Add(Row(MakeLabel("UpdateFlag 地址"), txtFlagAddr,
                                MakeLabel("触发值", 8), numFlagVal,
                                MakeLabel("→ ACK值", 4), numResetVal));

        // ── 注入目标 ──────────────────────────────────────────────────────────
        AddSection("🎯 注入目标");
        cmbTargetType = MakeCombo(new[]{"Screen — 屏幕坐标点击", "Browser — 浏览器(Playwright)",
                                        "Window — Win32窗口", "Clipboard — 剪贴板"}, 220);
        cmbMethod = MakeCombo(new[]{"fill","type","js_set","clipboard","uia","win32"}, 100);
        chkClickBefore = MakeCheck("注入前点击");
        chkClearBefore = MakeCheck("注入前全选清空");
        chkPressEnter  = MakeCheck("注入后按 Enter");
        numDelay = MakeNum(0, 5000, 200);
        layout.Controls.Add(Row(MakeLabel("目标类型"), cmbTargetType,
                                MakeLabel("发送方式", 8), cmbMethod));
        layout.Controls.Add(Row(chkClickBefore, chkClearBefore, chkPressEnter,
                                MakeLabel("点击延迟(ms)", 8), numDelay));

        // ── 屏幕坐标面板 ──────────────────────────────────────────────────────
        pnlScreen = new Panel { AutoSize = true, Dock = DockStyle.Top, BackColor = BackColor, Margin = new Padding(0) };
        numSx = MakeNum(-9999, 9999, 0); numSy = MakeNum(-9999, 9999, 0);
        cmbClickMode  = MakeCombo(new[]{"Single — 单击","Double — 双击","Right — 右击"}, 130);
        chkRestoreMouse = MakeCheck("注入后恢复鼠标");
        var btnCap = MakeButton("📍 3秒后捕获坐标", 130, Color.FromArgb(40, 80, 160));
        btnCap.Click += async (_, _) =>
        {
            btnCap.Text = "3..."; btnCap.Enabled = false;
            for (int i = 2; i >= 0; i--)
            {
                await Task.Delay(1000);
                btnCap.Text = i > 0 ? $"{i}..." : "捕获中";
            }
            var pos = Cursor.Position;
            numSx.Value = pos.X; numSy.Value = pos.Y;
            btnCap.Text = "📍 3秒后捕获坐标"; btnCap.Enabled = true;
            MessageBox.Show($"已捕获坐标: ({pos.X}, {pos.Y})", "坐标捕获",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        var rowSc1 = Row(MakeLabel("屏幕 X"), numSx, MakeLabel("Y", 6), numSy, MakeLabel("点击方式",8), cmbClickMode);
        var rowSc2 = Row(chkRestoreMouse, btnCap);
        pnlScreen.Controls.Add(rowSc2); pnlScreen.Controls.Add(rowSc1);
        layout.Controls.Add(pnlScreen);

        // ── 浏览器面板 ────────────────────────────────────────────────────────
        pnlBrowser = new Panel { AutoSize = true, Dock = DockStyle.Top, BackColor = BackColor, Margin = new Padding(0), Visible = false };
        cmbBrowserMode = MakeCombo(new[]{"active_tab — 自动连接已打开标签页","cdp — CDP调试端口9222","new_window — 启动新窗口"}, 240);
        txtTabTitle  = MakeTextBox(150); txtBrowserUrl = MakeTextBox(200);
        txtCss       = MakeTextBox(300); txtXpath     = MakeTextBox(300);
        pnlBrowser.Controls.Add(Row(MakeLabel("XPath(备选)"), txtXpath));
        pnlBrowser.Controls.Add(Row(MakeLabel("CSS 选择器"), txtCss));
        pnlBrowser.Controls.Add(Row(MakeLabel("页面 URL"), txtBrowserUrl));
        pnlBrowser.Controls.Add(Row(MakeLabel("连接方式"), cmbBrowserMode,
                                    MakeLabel("标签标题关键字",8), txtTabTitle));
        layout.Controls.Add(pnlBrowser);

        // ── 窗口面板 ──────────────────────────────────────────────────────────
        pnlWindow = new Panel { AutoSize = true, Dock = DockStyle.Top, BackColor = BackColor, Margin = new Padding(0), Visible = false };
        txtProcName = MakeTextBox(130); txtWinTitle = MakeTextBox(130);
        txtAutoId   = MakeTextBox(130); txtClassName = MakeTextBox(100);
        pnlWindow.Controls.Add(Row(MakeLabel("AutomationId"), txtAutoId, MakeLabel("ClassName",8), txtClassName));
        pnlWindow.Controls.Add(Row(MakeLabel("进程名"), txtProcName, MakeLabel("窗口标题关键字",8), txtWinTitle));
        layout.Controls.Add(pnlWindow);

        cmbTargetType.SelectedIndexChanged += (_, _) => UpdateTargetPanels();

        // ── 条件 & 变换 ───────────────────────────────────────────────────────
        AddSection("⚡ 条件 & 变换");
        txtExpr      = MakeTextBox(380);
        txtTransform = MakeTextBox(380);
        layout.Controls.Add(Row(MakeLabel("触发条件(留空=全部通过)"), txtExpr));
        layout.Controls.Add(Row(MakeLabel("示例: value > 0  |  value != 0"),
                                MakeLabel("", 0)));
        layout.Controls.Add(Row(MakeLabel("值变换(留空=原始值)"), txtTransform));
        layout.Controls.Add(Row(MakeLabel("示例: {value:F2}  |  Math.Round(value,2)"),
                                MakeLabel("", 0)));

        // ── 按钮栏 ────────────────────────────────────────────────────────────
        var btnOk  = MakeButton("✓ 保存", 90, Color.FromArgb(30, 120, 200));
        var btnCan = MakeButton("取消",   70, Color.FromArgb(60, 60, 80));
        btnOk.Click  += (_, _) => { SaveValues(); DialogResult = DialogResult.OK; Close(); };
        btnCan.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.FromArgb(20, 23, 32), Padding = new Padding(8)
        };
        btnRow.Controls.Add(btnCan); btnRow.Controls.Add(btnOk);
        Controls.Add(btnRow);
    }

    private void UpdateTargetPanels()
    {
        var sel = cmbTargetType.Text;
        pnlScreen.Visible  = sel.StartsWith("Screen");
        pnlBrowser.Visible = sel.StartsWith("Browser");
        pnlWindow.Visible  = sel.StartsWith("Window");
    }

    private void LoadValues()
    {
        txtName.Text     = _rule.Name;
        numPoll.Value    = _rule.PollIntervalMs;
        numDebounce.Value = _rule.DebounceMs;
        // PLC
        cmbBrand.Text    = _rule.Plc.Brand == "mock" ? "mock — 模拟器" : "keyence_kv — 基恩士 KV-8000";
        txtIp.Text       = _rule.Plc.Ip;
        numPort.Value    = _rule.Plc.Port;
        numUnit.Value    = _rule.Plc.Unit;
        txtAddr.Text     = _rule.Address;
        numDataLen.Value = _rule.DataLength;
        txtIp.Enabled   = _rule.Plc.Brand != "mock";
        // Handshake
        chkHs.Checked    = _rule.Handshake.Enabled;
        txtHbAddr.Text   = _rule.Handshake.HeartbeatAddr;
        txtFlagAddr.Text = _rule.Handshake.UpdateFlagAddr;
        numFlagVal.Value = _rule.Handshake.UpdateFlagValue;
        numResetVal.Value = _rule.Handshake.ResetValue;
        numSilence.Value = _rule.Handshake.MaxSilenceSec;
        // Target type
        cmbTargetType.Text = _rule.Target.Type switch
        {
            TargetType.Screen    => "Screen — 屏幕坐标点击",
            TargetType.Browser   => "Browser — 浏览器(Playwright)",
            TargetType.Window    => "Window — Win32窗口",
            TargetType.Clipboard => "Clipboard — 剪贴板",
            _ => "Screen — 屏幕坐标点击"
        };
        numSx.Value      = _rule.Target.ScreenX;
        numSy.Value      = _rule.Target.ScreenY;
        cmbClickMode.Text = _rule.Target.ClickMode switch
        { ClickType.Double=>"Double — 双击", ClickType.Right=>"Right — 右击", _=>"Single — 单击" };
        chkRestoreMouse.Checked = _rule.Target.RestoreMouse;
        cmbBrowserMode.Text = _rule.Target.BrowserMode switch
        { "cdp"=>"cdp — CDP调试端口9222","new_window"=>"new_window — 启动新窗口",_=>"active_tab — 自动连接已打开标签页"};
        txtTabTitle.Text   = _rule.Target.TabTitle;
        txtBrowserUrl.Text = _rule.Target.BrowserUrl;
        txtCss.Text        = _rule.Target.CssSelector;
        txtXpath.Text      = _rule.Target.XPath;
        txtProcName.Text   = _rule.Target.ProcessName;
        txtWinTitle.Text   = _rule.Target.WindowTitle;
        txtAutoId.Text     = _rule.Target.AutomationId;
        txtClassName.Text  = _rule.Target.ClassName;
        cmbMethod.Text     = _rule.Target.Method.ToString().ToLower();
        chkClickBefore.Checked  = _rule.Target.ClickBefore;
        chkClearBefore.Checked  = _rule.Target.ClearBefore;
        chkPressEnter.Checked   = _rule.Target.PressEnter;
        numDelay.Value = _rule.Target.DelayMs;
        // Condition
        txtExpr.Text      = _rule.Condition.Expression;
        txtTransform.Text = _rule.Condition.Transform;
        UpdateTargetPanels();
    }

    private void SaveValues()
    {
        _rule.Name           = txtName.Text;
        _rule.PollIntervalMs = (int)numPoll.Value;
        _rule.DebounceMs     = (int)numDebounce.Value;
        _rule.Plc.Brand      = cmbBrand.Text.StartsWith("mock") ? "mock" : "keyence_kv";
        _rule.Plc.Ip         = txtIp.Text;
        _rule.Plc.Port       = (int)numPort.Value;
        _rule.Plc.Unit       = (int)numUnit.Value;
        _rule.Address        = txtAddr.Text;
        _rule.DataLength     = (int)numDataLen.Value;
        _rule.Handshake.Enabled         = chkHs.Checked;
        _rule.Handshake.HeartbeatAddr   = txtHbAddr.Text;
        _rule.Handshake.UpdateFlagAddr  = txtFlagAddr.Text;
        _rule.Handshake.UpdateFlagValue = (int)numFlagVal.Value;
        _rule.Handshake.ResetValue      = (int)numResetVal.Value;
        _rule.Handshake.MaxSilenceSec   = (int)numSilence.Value;
        _rule.Target.Type    = cmbTargetType.Text[..cmbTargetType.Text.IndexOf(' ')] switch
        { "Screen"=>"Screen","Browser"=>"Browser","Window"=>"Window","Clipboard"=>"Clipboard",_=>"Screen"} switch
        { "Browser"=>TargetType.Browser,"Window"=>TargetType.Window,"Clipboard"=>TargetType.Clipboard,_=>TargetType.Screen};
        _rule.Target.ScreenX  = (int)numSx.Value;
        _rule.Target.ScreenY  = (int)numSy.Value;
        _rule.Target.ClickMode = cmbClickMode.Text.StartsWith("Double") ? ClickType.Double :
                                  cmbClickMode.Text.StartsWith("Right")  ? ClickType.Right  : ClickType.Single;
        _rule.Target.RestoreMouse  = chkRestoreMouse.Checked;
        _rule.Target.BrowserMode   = cmbBrowserMode.Text.Split(' ')[0];
        _rule.Target.TabTitle      = txtTabTitle.Text;
        _rule.Target.BrowserUrl    = txtBrowserUrl.Text;
        _rule.Target.CssSelector   = txtCss.Text;
        _rule.Target.XPath         = txtXpath.Text;
        _rule.Target.ProcessName   = txtProcName.Text;
        _rule.Target.WindowTitle   = txtWinTitle.Text;
        _rule.Target.AutomationId  = txtAutoId.Text;
        _rule.Target.ClassName     = txtClassName.Text;
        _rule.Target.Method        = Enum.TryParse<SendMethod>(cmbMethod.Text, true, out var m) ? m : SendMethod.Fill;
        _rule.Target.ClickBefore   = chkClickBefore.Checked;
        _rule.Target.ClearBefore   = chkClearBefore.Checked;
        _rule.Target.PressEnter    = chkPressEnter.Checked;
        _rule.Target.DelayMs       = (int)numDelay.Value;
        _rule.Condition.Expression = txtExpr.Text;
        _rule.Condition.Transform  = txtTransform.Text;
    }

    public Rule GetRule() => _rule;

    // ── 控件工厂 ──────────────────────────────────────────────────────────────
    private static readonly Color Dark2 = Color.FromArgb(28, 32, 48);
    private static readonly Color Dark3 = Color.FromArgb(36, 40, 64);
    private static readonly Color TextC = Color.FromArgb(212, 216, 240);
    private static readonly Color Muted = Color.FromArgb(84, 92, 122);

    private Label MakeSection(string title)
    {
        var l = new Label
        {
            Text = title, Dock = DockStyle.Top, AutoSize = false, Height = 26,
            BackColor = Dark3, ForeColor = Color.FromArgb(92, 154, 255),
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0), Margin = new Padding(0, 8, 0, 4)
        };
        return l;
    }

    private Label MakeLabel(string text, int leftPad = 0) => new()
    {
        Text = text, AutoSize = true, ForeColor = Muted,
        Margin = new Padding(leftPad, 6, 4, 0)
    };

    private TextBox MakeTextBox(int width) => new()
    {
        Width = width, BackColor = Dark2, ForeColor = TextC,
        BorderStyle = BorderStyle.FixedSingle, Font = new Font("Consolas", 9f),
        Margin = new Padding(0, 2, 4, 2)
    };

    private NumericUpDown MakeNum(int min, int max, int val) => new()
    {
        Minimum = min, Maximum = max, Value = val, Width = 80,
        BackColor = Dark2, ForeColor = TextC, Margin = new Padding(0, 2, 4, 2)
    };

    private ComboBox MakeCombo(string[] items, int width) 
    {
        var c = new ComboBox
        {
            Width = width, DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Dark2, ForeColor = TextC, FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 2, 4, 2)
        };
        c.Items.AddRange(items);
        if (c.Items.Count > 0) c.SelectedIndex = 0;
        return c;
    }

    private CheckBox MakeCheck(string text) => new()
    {
        Text = text, AutoSize = true, ForeColor = TextC,
        Margin = new Padding(0, 4, 12, 4)
    };

    private Button MakeButton(string text, int width, Color bg) => new()
    {
        Text = text, Width = width, Height = 30, BackColor = bg,
        ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", 9f),
        Margin = new Padding(4, 4, 4, 4), Cursor = Cursors.Hand
    };
}
