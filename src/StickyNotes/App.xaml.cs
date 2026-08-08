using System.IO;
using System.Threading;
using System.Windows;
using StickyNotes.Services;

namespace StickyNotes;

public partial class App : Application
{
    private const string MutexName = "Local\\Fable.StickyNotes.SingleInstance";
    private const string ActivationEventName = "Local\\Fable.StickyNotes.Activate";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _activationEvent;
    private CancellationTokenSource? _activationCancellation;
    private AppController? _controller;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(true, MutexName, out var isFirstInstance);
        _ownsMutex = isFirstInstance;
        if (!isFirstInstance)
        {
            SignalExistingInstance();
            Shutdown();
            return;
        }

        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        _activationCancellation = new CancellationTokenSource();
        ListenForActivation(_activationCancellation.Token);

        var storageDirectory = Environment.GetEnvironmentVariable("STICKYNOTES_DATA_DIRECTORY");
        try
        {
            _controller = new AppController(new NoteStore(storageDirectory));
            _controller.Start();
        }
        catch (InvalidDataException exception)
        {
            MessageBox.Show(
                $"{exception.Message}\n\n为防止数据丢失，软件不会用空白内容覆盖现有文件。",
                "无法读取便签数据",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationCancellation?.Cancel();
        _activationEvent?.Set();
        _activationEvent?.Dispose();
        _activationCancellation?.Dispose();
        if (_ownsMutex)
        {
            _instanceMutex?.ReleaseMutex();
        }
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var activationEvent = EventWaitHandle.OpenExisting(ActivationEventName);
            activationEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }

    private void ListenForActivation(CancellationToken cancellationToken)
    {
        _ = Task.Run(() =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                _activationEvent?.WaitOne();
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                Dispatcher.BeginInvoke(() => _controller?.ShowMainWindow());
            }
        }, cancellationToken);
    }
}
