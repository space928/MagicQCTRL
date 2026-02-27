using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace MagicQCTRLDesktopApp;

internal class Throttle : IDisposable
{
    private readonly ObservableObject target;
    private readonly string prop;
    private readonly TimeSpan timeout;
    private readonly Action onChanged;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer timer;

    public Throttle(ObservableObject target, string prop, TimeSpan timeout, Action onChanged)
    {
        this.target = target;
        this.prop = prop;
        this.timeout = timeout;
        this.onChanged = onChanged;
        dispatcher = Dispatcher.CurrentDispatcher ?? throw new Exception("Must be called from a thread with an active dispatcher");

        timer = new(DispatcherPriority.Normal, dispatcher);
        timer.Interval = timeout;
        timer.Tick += Timer_Tick;

        target.PropertyChanged += Target_PropertyChanged;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        timer.Stop();
        onChanged?.Invoke();
    }

    public void Dispose()
    {
        target.PropertyChanged -= Target_PropertyChanged;
    }

    private void Target_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != prop)
            return;

        if (!timer.IsEnabled)
            timer.Start();
    }
}
