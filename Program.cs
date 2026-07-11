namespace hoerhilfe;

static class Program
{
    private const string SingleInstanceMutexName = @"Local\Hoerfix.SingleInstance";
    private const string ShowWindowEventName = @"Local\Hoerfix.ShowWindow";

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        using var singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: SingleInstanceMutexName,
            createdNew: out var isFirstInstance);

        if (!isFirstInstance)
        {
            SignalRunningInstance();
            return;
        }

        using var showWindowEvent = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: ShowWindowEventName);

        var form = new Form1();
        var showWindowRegistration = ThreadPool.RegisterWaitForSingleObject(
            showWindowEvent,
            (_, _) => RestoreRunningInstance(form),
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);

        try
        {
            Application.Run(form);
        }
        finally
        {
            showWindowRegistration.Unregister(null);
        }
    }

    private static void SignalRunningInstance()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                using var showWindowEvent = EventWaitHandle.OpenExisting(ShowWindowEventName);
                showWindowEvent.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static void RestoreRunningInstance(Form1 form)
    {
        if (form.IsDisposed)
        {
            return;
        }

        try
        {
            form.BeginInvoke(form.RestoreMainWindow);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
