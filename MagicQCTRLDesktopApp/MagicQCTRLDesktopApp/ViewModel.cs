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
using System.Windows.Interop;
using System.Windows.Media;

namespace MagicQCTRLDesktopApp
{
    internal class ViewModel : ObservableObject
    {
        #region Bindable propeties
        [Reactive] public string USBConnectionStatus => isUSBConnected ? "Connected" : "Disconnected";
        [Reactive] public string OSCConnectionStatus => isOSCConnected ? "Connected" : "Disconnected";
        [Reactive] public string MQConnectionStatus => isMQConnected ? "Connected" : "Disconnected";
        [Reactive] public string ConnectButtonText => (isUSBConnected || isOSCConnected || isMQConnected) ? "Reconnect" : "MagicQConnect";
        [Reactive] public int OSCRXPort { get; set; } = 9000;
        [Reactive] public int OSCTXPort { get; set; } = 8000;
        [Reactive] public RelayCommand ConnectCommand { get; private set; }
        [Reactive] public RelayCommand OpenProfileCommand { get; private set; }
        [Reactive] public RelayCommand SaveProfileCommand { get; private set; }
        [Reactive] public RelayCommand OpenLogCommand { get; private set; }
        [Reactive] public RelayCommand<string> EditControlCommand { get; private set; }
        [Reactive] public RelayCommand<string> PageIncrementCommand { get; private set; }
        [Reactive] public RelayCommand TestButtonsCommand { get; private set; }
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
        [Reactive] public float TestButtonsEnabled { get; set; } = 1;

        [Reactive] public RelayCommand DebugPlusCommand { get; private set; }
        [Reactive] public RelayCommand DebugMinusCommand { get; private set; }
        #endregion

        public const int MAX_PAGES = 3;
        public const int BUTTON_COUNT = 12 + 8;
        public const int COLOUR_BUTTON_COUNT = 12;
        public const int MAX_NAME_LENGTH = 6;
        public static readonly string LAST_PROFILE = "last_profile.json";

        private bool isUSBConnected = false;
        private bool isOSCConnected = false;
        private bool isMQConnected = false;
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
            DebugPlusCommand = new(() => magicQDriver.TurnEncoder(MagicQCTRLEncoderType.X, 1));
            DebugMinusCommand = new(() => magicQDriver.TurnEncoder(MagicQCTRLEncoderType.X, -1));
            TestButtonsCommand = new(TestButtonsCommandExecute);

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
                        case nameof(ButtonEditorViewModel.EncoderFunction):
                            magicQCTRLProfile.pages[page].keys[id].encoderFunction = ed.EncoderFunction; break;
                        case nameof(ButtonEditorViewModel.CustomKeyCode):
                            magicQCTRLProfile.pages[page].keys[id].customKeyCode = ed.CustomKeyCode; break;
                        case nameof(ButtonEditorViewModel.ExecuteItemPage):
                            magicQCTRLProfile.pages[page].keys[id].executeItemPage = ed.ExecuteItemPage; break;
                        case nameof(ButtonEditorViewModel.ExecuteItemIndex):
                            magicQCTRLProfile.pages[page].keys[id].executeItemIndex = ed.ExecuteItemIndex; break;
                        case nameof(ButtonEditorViewModel.ExecuteItemCommand):
                            magicQCTRLProfile.pages[page].keys[id].executeItemAction = ed.ExecuteItemCommand; break;
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

            usbDriver.OnMessageReceived += OnUSBMessageReceived;
        }

        public void OnExit()
        {
            Log("Shutting down...");
            oscDriver.Dispose();
            magicQDriver.Dispose();
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

            if(magicQDriver.MagicQConnect())
                isMQConnected = true;

            OnPropertyChanged(nameof(OSCConnectionStatus));
            OnPropertyChanged(nameof(USBConnectionStatus));
            OnPropertyChanged(nameof(MQConnectionStatus));

            usbDriver.OnClose += () =>
            {
                isUSBConnected = false;
                OnPropertyChanged(nameof(USBConnectionStatus));
                Log("USB device disconnected!", LogLevel.Warning);
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

            if (TestButtonsEnabled > 1)
            {
                var key = magicQCTRLProfile.pages[CurrentPage].keys[id];
                ExecuteKeyAction(key, 0);
            }
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

        public void TestButtonsCommandExecute()
        {
            if (TestButtonsEnabled > 1)
                TestButtonsEnabled = 1;
            else
                TestButtonsEnabled = 3;
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

                MagicQCTRLKey key = default;
                switch (msg.msgType)
                {
                    case MagicQCTRLMessageType.Key:
                        if (msg.value == 1)
                            key = magicQCTRLProfile.pages[msg.page].keys[msg.keyCode];
                        break;
                    case MagicQCTRLMessageType.Button:
                        if (msg.value == 1)
                            key = magicQCTRLProfile.pages[msg.page].keys[msg.keyCode + COLOUR_BUTTON_COUNT];
                        break;
                    case MagicQCTRLMessageType.Encoder:
                        key = magicQCTRLProfile.pages[msg.page].keys[msg.keyCode + COLOUR_BUTTON_COUNT];
                        break;
                    default:
                        break;
                }

                ExecuteKeyAction(key, (sbyte)-msg.delta);
            }
        }

        public static void ExecuteKeyAction(MagicQCTRLKey key, sbyte encoderDelta = 0)
        {
            if (encoderDelta == 0)
            {
                // Key press action
                if (!string.IsNullOrEmpty(key.oscMessagePress) && key.oscMessagePress != "/")
                {
                    try
                    {
                        (string address, var args) = OSCMessageParser.ParseOSCMessage(key.oscMessagePress);
                        oscDriver.SendMessage(new OscMessage(address, args));
                    }
                    catch (Exception e)
                    {
                        Log($"Failed to parse OSC message: {key.oscMessagePress}\nError: {e.Message}", LogLevel.Warning);
                    }
                }

                magicQDriver.ExecuteCommand(key.specialFunction, key.customKeyCode);
                if (key.customKeyCode != -1 && key.specialFunction != MagicQCTRLSpecialFunction.SOpenLayout)
                    magicQDriver.PressMQKey(key.customKeyCode);

                if(key.executeItemPage > 0 && key.executeItemIndex >= 0)
                {
                    magicQDriver.SetExecItemState((ushort)key.executeItemPage, (uint)key.executeItemIndex, key.executeItemAction);
                }
            }
            else
            {
                // Encoder action
                if (key.encoderFunction != MagicQCTRLEncoderType.None)
                {
                    magicQDriver.TurnEncoder(key.encoderFunction, encoderDelta);

                    if (!string.IsNullOrEmpty(key.oscMessageRotate) && key.oscMessageRotate != "/")
                    {
                        try
                        {
                            (string address, var args) = OSCMessageParser.ParseOSCMessage($"{key.oscMessageRotate} {encoderDelta}");
                            oscDriver.SendMessage(new OscMessage(address, args));
                        }
                        catch (Exception e)
                        {
                            Log($"Failed to parse OSC message: {key.oscMessageRotate} {encoderDelta}\nError: {e.Message}", LogLevel.Warning);
                        }
                    }
                }
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
