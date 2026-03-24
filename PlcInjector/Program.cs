using PlcInjector;

var app = new System.Windows.Forms.ApplicationContext();
System.Windows.Forms.Application.EnableVisualStyles();
System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);

// 全局异常捕获
System.Windows.Forms.Application.ThreadException += (_, e) =>
    System.Windows.Forms.MessageBox.Show($"未处理异常:\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
        "错误", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    System.Windows.Forms.MessageBox.Show($"严重错误:\n{e.ExceptionObject}", "严重错误",
        System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);

var mainForm = new MainForm();
mainForm.FormClosed += (_, __) => app.ExitThread();
System.Windows.Forms.Application.Run(app, mainForm);
