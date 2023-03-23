using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MagicQCTRLDesktopApp
{
    internal class ViewModel
    {
        #region Bindable propeties
        [Reactive] public string USBConnectionStatus => isUSBConnected ? "Connected" : "Disconnected";
        [Reactive] public string OSCConnectionStatus => isOSCConnected ? "Connected" : "Disconnected";
        [Reactive] public RelayCommand ConnectCommand { get; private set; }
        [Reactive] public RelayCommand OpenProfileCommand { get; private set; }
        [Reactive] public RelayCommand SaveProfileCommand { get; private set; }
        [Reactive] public RelayCommand OpenLogCommand { get; private set; }
        [Reactive] public static ObservableCollection<string> LogList { get; private set; } = new ObservableCollection<string>();
        #endregion

        private bool isUSBConnected = false;
        private bool isOSCConnected = false;
        private MagicQCTRLProfile magicQCTRLProfile = new();
        private static LogWindow logWindow;
        private static bool started = false;

        public ViewModel() 
        {
            // Only display startup message once
            if(!started)
                Log("Starting MagicQCTRL Desktop App...");
            started = true;

            // Bind commands
            ConnectCommand = new(ConnectExecute, CanConnect);
            OpenLogCommand = new(OpenLogExecute, () => logWindow == null);
            OpenProfileCommand = new(OpenProfileExecute);
            SaveProfileCommand = new(SaveProfileExecute);
        }

        #region Commands
        public void ConnectExecute()
        {
            Log("Connecting...");
        }

        public bool CanConnect()
        {
            return !(isUSBConnected && isOSCConnected);
        }

        public void SaveProfileExecute()
        {
            SaveFileDialog saveFileDialog = new()
            {
                AddExtension = true,
                DereferenceLinks = true,
                Filter = "MagicQCTRL Profiles (*.json)|*.json|All files (*.*)|*.*",
                OverwritePrompt = true,
                Title = "Save MagicQCTRL Profile"
            };
            if(saveFileDialog.ShowDialog() ?? false)
            {
                File.WriteAllText(saveFileDialog.FileName, JsonSerializer.Serialize(magicQCTRLProfile));
                Log($"Saved profile to {saveFileDialog.FileName}!");
            }
        }

        public void OpenProfileExecute()
        {
            OpenFileDialog openFileDialog = new()
            {
                Multiselect = false,
                Title = "Open MagicQCTRL Profile",
                CheckFileExists = true,
                Filter = "MagicQCTRL Profiles (*.json)|*.json|All files (*.*)|*.*"
            };
            if(openFileDialog.ShowDialog() ?? false)
            {
                magicQCTRLProfile = JsonSerializer.Deserialize<MagicQCTRLProfile>(File.ReadAllText(openFileDialog.FileName));
                Log($"Loaded profile from disk! {openFileDialog.FileName}");
            }
        }

        public void OpenLogExecute()
        {
            logWindow = new();
            //logWindow.Owner = ((Window)e.Source);
            Log("Opening log...");
            logWindow.Closed += (e, x) => { logWindow = null; };
            logWindow.Show();
        }
        #endregion

        public static void Log(object message, LogLevel level = LogLevel.Info, [CallerMemberName] string caller = "")
        {
            lock(LogList)
            {
                string msg = $"[{level}] [{DateTime.Now}] [{caller}] {message}";
                LogList.Add(msg);
                Console.WriteLine(msg);
                Debug.WriteLine(msg);
            }
        }

        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }
    }
}
