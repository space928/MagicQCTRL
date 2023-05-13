using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Rug.Osc;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Data;
using System.Windows.Media;

namespace MagicQCTRLDesktopApp
{
    internal class ViewModel : ObservableObject
    {
        #region Bindable propeties
        [Reactive] public string USBConnectionStatus => isUSBConnected ? "Connected" : "Disconnected";
        [Reactive] public string OSCConnectionStatus => isOSCConnected ? "Connected" : "Disconnected";
        [Reactive] public string ConnectButtonText => (isUSBConnected || isOSCConnected) ? "Reconnect" : "Connect";
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
        [Reactive] public string CurrentPageString => $"Page {CurrentPage + 1}";
        [Reactive] public ObservableCollection<string> ButtonNames { get; private set; } = new(Enumerable.Repeat("Button", BUTTON_COUNT));
        [Reactive] public ObservableCollection<Brush> ButtonColours { get; private set; } = new(Enumerable.Repeat(new SolidColorBrush(Colors.Black), BUTTON_COUNT));
        [Reactive] public ObservableCollection<float> ButtonSelection { get; private set; } = new(Enumerable.Repeat(1f, BUTTON_COUNT));
        [Reactive] public int SelectedButton => SelectedButtonLocal + CurrentPage * BUTTON_COUNT;
        [Reactive] public int SelectedButtonLocal { get; private set; }
        [Reactive] public ObservableCollection<ButtonEditorViewModel> ButtonEditors { get; private set; } = new(Enumerable.Range(0, BUTTON_COUNT * MAX_PAGES).Select(x => new ButtonEditorViewModel(x)));
        [Reactive] public ButtonEditorViewModel ButtonEditor => ButtonEditors[SelectedButton];
        [Reactive] public float BaseBrightness { get; set; } = 0.5f;
        [Reactive] public float PressedBrightness { get; set; } = 2.5f;
        #endregion

        public const int MAX_PAGES = 3;
        public const int BUTTON_COUNT = 12 + 8;
        public const int COLOUR_BUTTON_COUNT = 12;
        public const int MAX_NAME_LENGTH = 6;
        public static readonly string LAST_PROFILE = "last_profile.json";

        private bool isUSBConnected = false;
        private bool isOSCConnected = false;
        private MagicQCTRLProfile magicQCTRLProfile = new();
        private static LogWindow logWindow;
        private static bool started = false;
        private static USBDriver usbDriver = null;
        private static OSCDriver oscDriver = null;
        private static MagicQDriver magicQDriver = null;
        private static ObservableCollection<string> logList;
        private static object logListLock = new();
        private JsonSerializerOptions jsonSerializerOptions = new()
        {
            IncludeFields = true,
            AllowTrailingCommas = true,
            WriteIndented = true,
        };

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
            magicQDriver = new();

            // Bind commands
            ConnectCommand = new(ConnectExecute, CanConnect);
            OpenLogCommand = new(OpenLogExecute, () => logWindow == null);
            OpenProfileCommand = new(OpenProfileExecute);
            SaveProfileCommand = new(SaveProfileExecute);
            EditControlCommand = new(EditControlCommandExecute);
            PageIncrementCommand = new(PageIncrementCommandExecute);

            // Bind button properties
            foreach(var editor in ButtonEditors)
            {
                editor.PropertyChanged += (o, e) =>
                {
                    OnPropertyChanged(nameof(ButtonEditors));
                };

                // Synchronise the editor with the serialisable model
                editor.PropertyChanged += (o, e) =>
                {
                    var ed = (ButtonEditorViewModel)o;
                    int page = ed.Id / BUTTON_COUNT;
                    int id = ed.Id % BUTTON_COUNT;
                    switch (e.PropertyName)
                    {
                        case nameof(ButtonEditorViewModel.Name):
                            magicQCTRLProfile.pages[page].keys[id].name = ed.Name;
                            usbDriver.SendKeyNameMessage(page, id, magicQCTRLProfile);
                            break;
                        case nameof(ButtonEditorViewModel.Colour):
                            magicQCTRLProfile.pages[page].keys[id].keyColourOff = ed.Colour.ToColor();
                            magicQCTRLProfile.pages[page].keys[id].keyColourOn = ed.Colour.ToColor();
                            usbDriver.SendColourConfig(page, id, magicQCTRLProfile);
                            break;
                        case nameof(ButtonEditorViewModel.ActiveColour):
                            magicQCTRLProfile.pages[page].keys[id].keyColourOn = ed.ActiveColour.ToColor(); break;
                        case nameof(ButtonEditorViewModel.OnPressOSC):
                            magicQCTRLProfile.pages[page].keys[id].oscMessagePress = ed.OnPressOSC; break;
                        case nameof(ButtonEditorViewModel.OnRotateOSC):
                            magicQCTRLProfile.pages[page].keys[id].oscMessageRotate = ed.OnRotateOSC; break;
                        case nameof(ButtonEditorViewModel.SpecialFunction):
                            magicQCTRLProfile.pages[page].keys[id].specialFunction = ed.SpecialFunction.SpecialFunction; break;
                        case nameof(ButtonEditorViewModel.CustomKeyCode):
                            magicQCTRLProfile.pages[page].keys[id].customKeyCode = ed.CustomKeyCode; break;
                    }
                };
            }

            this.WhenAnyValue(x => x.BaseBrightness).Throttle(TimeSpan.FromMilliseconds(2)).Subscribe(x=> { 
                magicQCTRLProfile.baseBrightness = x; 
                for(int page = 0; page < MAX_PAGES; page++)
                    for(int id = 0; id < COLOUR_BUTTON_COUNT; id++)
                        usbDriver.SendColourConfig(page, id, magicQCTRLProfile);
            });
            this.WhenAnyValue(x => x.PressedBrightness).Throttle(TimeSpan.FromMilliseconds(30)).Subscribe(x=> { 
                magicQCTRLProfile.pressedBrightness = x;
                for (int page = 0; page < MAX_PAGES; page++)
                    for (int id = 0; id < COLOUR_BUTTON_COUNT; id++)
                        usbDriver.SendColourConfig(page, id, magicQCTRLProfile);
            });
            this.WhenAnyValue(x => x.USBConnectionStatus).Where(x => isUSBConnected).Subscribe(x =>
            {
                for (int page = 0; page < MAX_PAGES; page++)
                {
                    for (int id = 0; id < COLOUR_BUTTON_COUNT; id++)
                        usbDriver.SendColourConfig(page, id, magicQCTRLProfile);
                    for (int id = 0; id < BUTTON_COUNT; id++)
                        usbDriver.SendKeyNameMessage(page, id, magicQCTRLProfile);
                }
            });

            // Auto connect
            ConnectExecute();

            // Auto load last profile
            OpenProfile(LAST_PROFILE);

            magicQDriver.Connect();

            usbDriver.OnMessageReceived += OnUSBMessageReceived;
        }

        public void OnExit()
        {
            Log("Shutting down...");
            oscDriver.Dispose();
            SaveProfile(LAST_PROFILE);
            Log("Goodbye!");
        }

        #region Commands
        public void ConnectExecute()
        {
            Log("Connecting...");
            if(usbDriver.USBConnect())
                isUSBConnected = true;

            if(oscDriver.OSCConnect(OSCRXPort, OSCTXPort))
                isOSCConnected = true;

            OnPropertyChanged(nameof(OSCConnectionStatus));
            OnPropertyChanged(nameof(USBConnectionStatus));

            usbDriver.OnClose += () =>
            {
                isUSBConnected = false;
                OnPropertyChanged(nameof(USBConnectionStatus));
            };
        }

        public bool CanConnect()
        {
            return true;
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
                SaveProfile(saveFileDialog.FileName);
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
                OpenProfile(openFileDialog.FileName);
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
            logWindow?.Close();
            logWindow = null;
        }

        public void EditControlCommandExecute(string controlId)
        {
            if(!int.TryParse(controlId, out int id))
            {
                Log("Invalid controlId! Something went very wrong!", LogLevel.Error);
                return;
            }

            Log($"Editing control {id}", LogLevel.Debug);
            for(int i = 0; i < ButtonNames.Count; i++)
            {
                if (i == id)
                    ButtonSelection[i] = 3;
                else
                    ButtonSelection[i] = 1;
            }

            SelectedButtonLocal = id;

            OnPropertyChanged(nameof(SelectedButton));
            OnPropertyChanged(nameof(ButtonEditor));
        }

        public void PageIncrementCommandExecute(string direction)
        {
            if (string.IsNullOrEmpty(direction))
                return;

            if (direction == "+" && CurrentPage < magicQCTRLProfile.pages.Length-1)
                CurrentPage++;

            if(direction == "-" && CurrentPage > 0)
                CurrentPage--;

            OnPropertyChanged(nameof(SelectedButton));
            OnPropertyChanged(nameof(ButtonEditors));
            OnPropertyChanged(nameof(ButtonEditor));

            Log($"Switched to page {CurrentPage}");
        }
        #endregion

        public void OpenProfile(string path)
        {
            try
            {
                magicQCTRLProfile = JsonSerializer.Deserialize<MagicQCTRLProfile>(File.ReadAllText(path), jsonSerializerOptions);
                ButtonEditors.FromMagicQProfile(magicQCTRLProfile);
                BaseBrightness = magicQCTRLProfile.baseBrightness;
                PressedBrightness = magicQCTRLProfile.pressedBrightness;

                Log($"Loaded profile from disk! {path}");
            }
            catch (Exception e)
            {
                Log($"Couldn't load profile from disk. Trying to load {path} \n  failed with: {e}", LogLevel.Warning);
                // Save the default profile instead
                ButtonEditors.ToMagicQProfile(ref magicQCTRLProfile);
            }
        }

        public void SaveProfile(string path)
        {
            try
            {
                File.WriteAllText(path, JsonSerializer.Serialize(magicQCTRLProfile, jsonSerializerOptions));
                Log($"Saved profile to {path}!");
            }
            catch (Exception e)
            {
                Log($"Couldn't save profile to disk. Trying to save {path} \n  failed with: {e}", LogLevel.Warning);
            }
        }

        private void OnUSBMessageReceived()
        {
            if (usbDriver.RXMessages.TryDequeue(out var msg))
            {
                // Currently the setup page counts as page 4, so skip it for now
                if (msg.page > MAX_PAGES)
                    return;

                string oscMsg = null;
                int oscParam = 0;
                MagicQCTRLKey key;
                MagicQCTRLSpecialFunction specialFunction = MagicQCTRLSpecialFunction.None;
                int customKeyCode = -1;
                switch (msg.msgType)
                {
                    case MagicQCTRLMessageType.Key:
                        if (msg.value == 1)
                        {
                            key = magicQCTRLProfile.pages[msg.page].keys[msg.keyCode];
                            specialFunction = key.specialFunction;
                            if (key.specialFunction == MagicQCTRLSpecialFunction.None)
                                oscMsg = key.oscMessagePress;
                            //oscParam = msg.value;
                            customKeyCode = key.customKeyCode;
                        }
                        break;
                    case MagicQCTRLMessageType.Button:
                        if (msg.value == 1)
                        {
                            key = magicQCTRLProfile.pages[msg.page].keys[msg.keyCode + COLOUR_BUTTON_COUNT];
                            specialFunction = key.specialFunction;
                            if (key.specialFunction == MagicQCTRLSpecialFunction.None)
                                oscMsg = key.oscMessagePress;
                            //oscParam = msg.value;
                            customKeyCode = key.customKeyCode;
                        }
                        break;
                    case MagicQCTRLMessageType.Encoder:
                        key = magicQCTRLProfile.pages[msg.page].keys[msg.keyCode + COLOUR_BUTTON_COUNT];
                        if (key.specialFunction == MagicQCTRLSpecialFunction.None)
                            oscMsg = key.oscMessageRotate;
                        oscParam = msg.delta;
                        break;
                    default:
                        break;
                }

                if (!string.IsNullOrEmpty(oscMsg) && oscMsg != "/")
                {
                    if (oscParam != 0)
                        oscMsg = $"{oscMsg} {oscParam}";
                    try
                    {
                        (string address, var args) = OSCMessageParser.ParseOSCMessage(oscMsg);
                        oscDriver.SendMessage(new OscMessage(address, args));
                    } catch(Exception e) 
                    {
                        Log($"Failed to parse OSC message: {oscMsg}\nError: {e.Message}", LogLevel.Warning);
                    }
                }

                magicQDriver.ExecuteCommand(specialFunction);
                if(customKeyCode != -1)
                    magicQDriver.PressMQKey(customKeyCode);
            }
        }

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
