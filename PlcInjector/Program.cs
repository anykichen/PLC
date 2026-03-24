using PlcInjector;

[STAThread]
static void Main()
{
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

    // 全局异常捕获
    Application.ThreadException += (_, e) =>
        MessageBox.Show($"未处理异常:\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
            "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
    AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        MessageBox.Show($"严重错误:\n{e.ExceptionObject}", "严重错误",
            MessageBoxButtons.OK, MessageBoxIcon.Error);

    Application.Run(new MainForm());
}
