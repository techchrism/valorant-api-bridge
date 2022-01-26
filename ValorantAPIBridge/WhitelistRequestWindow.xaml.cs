using System;
using System.Media;
using System.Windows;
using ValorantAPIBridge.whitelist;

namespace ValorantAPIBridge;

public partial class WhitelistRequestWindow : Window
{
    private readonly Action<bool> _callback;
    private bool _triggered = false;
    public WhitelistRequest Request { get; }

    public WhitelistRequestWindow(Action<bool> callback, WhitelistRequest request)
    {
        _callback = callback;
        Request = request;
        InitializeComponent();
        SystemSounds.Beep.Play();
        this.DataContext = this;
    }

    private void OnButtonClick(bool allowed)
    {
        if (_triggered) return;
        _triggered = true;
        _callback?.Invoke(allowed);
        this.Close();
    }
    
    private void AcceptButton_OnClick(object sender, RoutedEventArgs e)
    {
        OnButtonClick(true);
    }

    private void RejectButton_OnClick(object sender, RoutedEventArgs e)
    {
        OnButtonClick(false);
    }

    private void WhitelistRequestWindow_OnClosed(object? sender, EventArgs e)
    {
        OnButtonClick(false);
    }
}