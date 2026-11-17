using System.Windows;
using System.Windows.Media;
using LANPUT.Core;

namespace LANPUT.Windows;

public partial class MainWindow
{
    bool checkingConnection;

    async Task<ConnectionReadiness> CheckConnectionAsync(NetworkChoice choice)
    {
        checkingConnection = true;
        SetButtons(receivingRequested);
        bool ownReceiver = receiver is not null && boundName == choice.Name && boundAddress == choice.Address.ToString();
        ConnectionCheckSummary.Text = "正在检查网卡与接收服务…";
        try
        {
            var result = await Task.Run(() => ConnectionPreflight.Scan(choice, ownReceiver), lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            // Do not attach an old result to a different selection or service state.
            if (Equals(NetworkBox.SelectedItem, choice) && ownReceiver == (receiver is not null
                && boundName == choice.Name && boundAddress == choice.Address.ToString()))
            {
                var issue = result.Checks.FirstOrDefault(check => check.Level == ConnectionCheckLevel.Block)
                    ?? result.Checks.FirstOrDefault(check => check.Level == ConnectionCheckLevel.Warning);
                ConnectionCheckSummary.Text = issue is null
                    ? "网卡与接收端口检查完成；手机能否发现设备仍需实测。详情见连接诊断。"
                    : issue.Message + "。详情见连接诊断。";
                ConnectionCheckSummary.Foreground = (Brush)FindResource(result.CanStart ? "TextMuted" : "StateError");
                ConnectionCheckSummary.ToolTip = result.Summary;
            }
            foreach (var check in result.Checks)
                Log($"连接检查 · {check.Level} · {check.Name} · {check.Message}");
            return result;
        }
        finally
        {
            checkingConnection = false;
            if (!closed) SetButtons(receivingRequested);
        }
    }

    async void ConnectionCheck_Click(object sender, RoutedEventArgs e)
    {
        if (busy || checkingConnection || closing) return;
        if (NetworkBox.SelectedItem is not NetworkChoice choice)
        {
            ConnectionCheckSummary.Text = "未选择可用网卡，请连接局域网后刷新。";
            ShowMotionToast("请先选择手机所在局域网的网卡", "Info");
            return;
        }
        try
        {
            var result = await CheckConnectionAsync(choice);
            ShowMotionToast(result.CanStart ? "连接检查完成，详情见接收设置与诊断" : "连接条件需处理，详情见接收设置", result.CanStart ? "Info" : "Error");
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            ConnectionCheckSummary.Text = "检查未完成，请刷新网卡后重试；可在诊断中查看错误类型。";
            Log("连接检查未完成 · " + error.GetType().Name);
        }
    }

    async Task<ReceiverHost> CreateReceiverHostAsync(NetworkChoice network, string? receiverName = null)
    {
        var host = new ReceiverHost(network, receiverName ?? settings.Name, settings.Id, 41000, playback, Log);
        try { await host.StartAsync(); return host; }
        catch
        {
            // TCP can start successfully before SSDP fails. Release both on partial startup.
            try { await host.DisposeAsync(); }
            catch (Exception error) { Log("未完成接收服务的清理异常 · " + error.GetType().Name); }
            throw;
        }
    }

    async Task TryAutomaticReceiveAsync()
    {
        if (!preferences.AutoReceive || closing || receivingRequested || receiver is not null) return;
        try
        {
            var choices = NetworkChoice.Enumerate();
            bool hasSavedNetwork = !string.IsNullOrWhiteSpace(settings.NetworkName)
                && !string.IsNullOrWhiteSpace(settings.Address);
            var network = hasSavedNetwork
                ? ReceiverRecovery.SelectForStartup(choices, settings.NetworkName, settings.Address)
                : choices.Count == 1 ? choices[0] : null;
            if (!hasSavedNetwork && network is null)
            {
                NetworkBox.ItemsSource = choices; NetworkBox.SelectedItem = null;
                ConnectionCheckSummary.Text = choices.Count == 0
                    ? "未找到可用网卡，请连接局域网后刷新并开始接收。"
                    : "发现多个网卡，请选择手机所在局域网的网卡并开始接收，下次将自动使用。";
                FooterHint.Text = ConnectionCheckSummary.Text;
                Log(ConnectionCheckSummary.Text);
                Settings_Click(this, new RoutedEventArgs());
                return;
            }
            if (network is null)
            {
                NetworkBox.ItemsSource = choices; NetworkBox.SelectedItem = null;
                ConnectionCheckSummary.Text = "等待已保存网卡恢复；如需改用其他网卡，请先停止接收。";
                receivingRequested = true;
                boundName = settings.NetworkName; boundAddress = settings.Address;
                nextNetworkCheck = DateTime.UtcNow.AddSeconds(2);
                ServiceStatus.Text = "等待已保存网卡恢复";
                FooterHint.Text = "自动接收等待原网卡；可在接收设置中停止后另选网卡";
                SetButtons(true);
                Log("自动接收等待已保存网卡，不切换到其他网卡。");
                return;
            }
            NetworkBox.ItemsSource = choices; NetworkBox.SelectedItem = network;
            await StartReceiverAsync(automatic: true);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            Log("自动接收暂未完成 · " + error.GetType().Name);
            FooterHint.Text = "自动接收未完成，请在接收设置中检查网卡";
        }
    }

    void AutoReceive_Click(object sender, RoutedEventArgs e)
    {
        preferences = preferences with { AutoReceive = AutoReceiveBox.IsChecked == true };
        SaveDesktopPreferences();
        ShowMotionToast(preferences.AutoReceive ? "已开启下次启动自动接收" : "已关闭启动自动接收", "Info");
    }
}
