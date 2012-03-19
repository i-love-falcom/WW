using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WW;

namespace WW
{
    /// <summary>
    /// Startup.xaml の相互作用ロジック
    /// </summary>
    public partial class Startup : Window
    {
        // NIC一覧
        public List<NetworkInterface> nicList = new List<NetworkInterface>();

        // バックエンド
        ServerMainJob mainJob = null;

        public Startup()
        {
            InitializeComponent();

            mainJob = new ServerMainJob();
            mainJob.InitLog(SourceLevels.All);
            mainJob.InitServer();
        }

        private void StartupWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            // NIC一覧を取得
            NetworkInterface[] nic = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface var in nic)
            {
                if (var.OperationalStatus == OperationalStatus.Up &&
                    var.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    var.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                )
                {
                    ComboBoxItem item = new ComboBoxItem();
                    item.Content = var.Description;
                    AdapterComboBox.Items.Add(item);
                    nicList.Add(var);
                }
            }
            if (AdapterComboBox.Items.Count > 0)
            {
                AdapterComboBox.SelectedIndex = 0;
            }

            // 初期ポート番号を設定
            PortTextBox.Text = "25000";

            // ボタン状態を初期化
            AdapterComboBox.IsEnabled = true;
            PortTextBox.IsEnabled = true;
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private void StartupWindow_OnClosed(object sender, EventArgs e)
        {
            if (mainJob != null)
            {
                mainJob.StopListening();
                mainJob.CloseServer();
                mainJob.CloseLog();
            }
        }

        private void StartButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (mainJob.StartListening(nicList[AdapterComboBox.SelectedIndex], int.Parse(PortTextBox.Text)))
            {
                AdapterComboBox.IsEnabled = false;
                PortTextBox.IsEnabled = false;
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
            }
        }

        private void StopButton_OnClick(object sender, RoutedEventArgs e)
        {
            AdapterComboBox.IsEnabled = true;
            PortTextBox.IsEnabled = true;
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            
            // 接続を閉じる
            mainJob.StopListening();
        }
    }
}
