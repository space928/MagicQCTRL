using CommunityToolkit.Mvvm.ComponentModel;
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
using System.Windows.Data;
using System.Windows.Input;

namespace MagicQCTRLDesktopApp
{
    internal class ViewModel : ObservableObject
    {
        #region Bindable propeties
        [Reactive] public string USBConnectionStatus => isUSBConnected ? "Connected" : "Disconnected";
        [Reactive] public string OSCConnectionStatus => isOSCConnected ? "Connected" : "Disconnected";
        [Reactive] public int OSCRXPort { get; set; } = 9000;
        [Reactive] public int OSCTXPort { get; set; } = 8000;
        [Reactive] public RelayCommand ConnectCommand { get; private set; }
        [Reactive] public RelayCommand OpenProfileCommand { get; private set; }
        [Reactive] public RelayCommand SaveProfileCommand { get; private set; }
        [Reactive] public RelayCommand OpenLogCommand { get; private set; }
        [Reactive] public RelayCommand<string> EditControlCommand { get; private set; }
        [Reactive] public RelayCommand<string> PageIncrementCommand { get; private set; }
        [Reactive] public static ObservableCollection<string> LogList { get { return logList; }
            private set 
            {
                logList = value;
                BindingOperations.EnableCollectionSynchronization(logList, logListLock);
            } 
        }
        [Reactive] public int CurrentPage { get; set; }
        [Reactive] public string CurrentPageString => $"Page {CurrentPage+1}";
        [Reactive] public ObservableCollection<string> ButtonNames { get; private set; }
        #endregion

        private bool isUSBConnected = false;
        private bool isOSCConnected = false;
        private MagicQCTRLProfile magicQCTRLProfile = new();
        private static LogWindow logWindow;
        private static bool started = false;
        private static USBDriver usbDriver = null;
        private static OSCDriver oscDriver = null;
        private static ObservableCollection<string> logList;
        private static object logListLock = new();

        public ViewModel() 
        {
            // Only run initialisation code once.
            // Otherwise the log window resets everything when it's opened
            if (started)
            {
                return;
            }
            started = true;

            LogList = new();
            Log("Starting MagicQCTRL Desktop App...");
            Log("  Copyright Thomas Mathieson 2023");

            usbDriver = new();
            oscDriver = new();

            // Bind commands
            ConnectCommand = new(ConnectExecute, CanConnect);
            OpenLogCommand = new(OpenLogExecute, () => logWindow == null);
            OpenProfileCommand = new(OpenProfileExecute);
            SaveProfileCommand = new(SaveProfileExecute);
            EditControlCommand = new(EditControlCommandExecute);
            PageIncrementCommand = new(PageIncrementCommandExecute);

            ConnectExecute();
        }

        #region Commands
        public void ConnectExecute()
        {
            Log("Connecting...");
            if(usbDriver.USBConnect())
                isUSBConnected = true;

            if(oscDriver.OSCConnect(OSCRXPort, OSCTXPort))
                isOSCConnected = true;
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

        public void CloseLogExecute()
        {
            logWindow.Close();
            logWindow = null;
        }

        public void EditControlCommandExecute(string controlId)
        {

        }

        public void PageIncrementCommandExecute(string direction)
        {
            if (string.IsNullOrEmpty(direction))
                return;

            if (direction == "+" && CurrentPage < magicQCTRLProfile.pages.Length-1)
                CurrentPage++;

            if(direction == "-" && CurrentPage > 0)
                CurrentPage--;

            Log($"Switched to page {CurrentPage}");
        }
        #endregion

        public static void Log(object message, LogLevel level = LogLevel.Info, [CallerMemberName] string caller = "")
        {
            lock(logListLock)
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
