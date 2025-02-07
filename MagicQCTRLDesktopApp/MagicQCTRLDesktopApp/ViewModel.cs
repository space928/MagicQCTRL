using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Rug.Osc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Text;

namespace MagicQCTRLDesktopApp;

internal class ViewModel : ObservableObject
{
    #region Bindable propeties
    [Reactive] public string USBConnectionStatus => isUSBConnected ? "Connected" : "Disconnected";
    [Reactive] public string OSCConnectionStatus => isOSCConnected ? "Connected" : "Disconnected";
    [Reactive] public string MQConnectionStatus => isMQConnected ? "Connected" : "Disconnected";
    [Reactive] public string ConnectButtonText => (isUSBConnected || isOSCConnected || isMQConnected) ? "Reconnect" : "Connect";
    [Reactive] public int OSCRXPort { get; set; } = 9000;
    [Reactive] public int OSCTXPort { get; set; } = 8000;
    [Reactive] public ObservableCollection<string> NICs => nics;
    [Reactive] public int SelectedNIC { get; set; }
    [Reactive] public RelayCommand ConnectCommand { get; private set; }
    [Reactive] public RelayCommand OpenProfileCommand { get; private set; }
    [Reactive] public RelayCommand SaveProfileCommand { get; private set; }
    [Reactive] public RelayCommand OpenLogCommand { get; private set; }
    [Reactive] public RelayCommand<string> EditControlCommand { get; private set; }
    [Reactive] public RelayCommand<string> PageIncrementCommand { get; private set; }
    [Reactive] public RelayCommand TestButtonsCommand { get; private set; }
    [Reactive]
    public static ObservableCollection<string> LogList
    {
        get { return logList; }
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
    public const int KEY_COUNT = 12;
    public const int ENCODER_COUNT = 8;
    public const int BUTTON_COUNT = KEY_COUNT + ENCODER_COUNT;
    public const int COLOUR_BUTTON_COUNT = KEY_COUNT;
    public const int MAX_NAME_LENGTH = 6;
    public static readonly string LAST_PROFILE = "last_profile.json";

    private readonly ObservableCollection<string> nics = [];
    private readonly List<IPAddress> nicAddresses = [];
    private readonly HashSet<(int page, int id)> reactiveButtons = [];
    private readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        IncludeFields = true,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };
    private bool isUSBConnected = false;
    private bool isOSCConnected = false;
    private bool isMQConnected = false;
    private MagicQCTRLProfile magicQCTRLProfile = new();

    private static LogWindow? logWindow;
    private static readonly USBDriver usbDriver = new();
    private static readonly OSCDriver oscDriver = new();
    private static readonly MagicQDriver magicQDriver = new();
    private static ObservableCollection<string> logList = [];
    private static readonly object logListLock = new();

    public ViewModel()
    {
        LogList = logList;

        Log("Starting MagicQCTRL Desktop App...");
        Log($"  Version: {Assembly.GetExecutingAssembly().GetName().Version}");
        Log("  Copyright Thomas Mathieson 2024");

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

        // Subscribe to events
        magicQDriver.OnConnectionStatusChanged += state =>
        {
            isMQConnected = state;
            OnPropertyChanged(nameof(MQConnectionStatus));
            OnPropertyChanged(nameof(ConnectButtonText));
        };
        oscDriver.OnConnectionStatusChanged += state =>
        {
            isOSCConnected = state;
            OnPropertyChanged(nameof(OSCConnectionStatus));
            OnPropertyChanged(nameof(ConnectButtonText));
        };
        usbDriver.OnConnectionStatusChanged += state =>
        {
            isUSBConnected = state;
            OnPropertyChanged(nameof(USBConnectionStatus));
            OnPropertyChanged(nameof(ConnectButtonText));
        };
        usbDriver.OnMessageReceived += OnUSBMessageReceived;
        magicQDriver.OnKeyLightChange += MagicQDriver_OnKeyLightChange;

        // Bind button properties
        foreach (var editor in ButtonEditors)
        {
            editor.PropertyChanged += (o, e) =>
            {
                OnPropertyChanged(nameof(ButtonEditors));
            };

            // Synchronise the editor with the serialisable model
            editor.PropertyChanged += (o, e) =>
            {
                if (o is not ButtonEditorViewModel ed)
                    return;

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
                        magicQCTRLProfile.pages[page].keys[id].specialFunction = ed.SpecialFunction.SpecialFunction;
                        if (GetButtonLight(ed.SpecialFunction.SpecialFunction) != MagicQCTRLButtonLight.None)
                            reactiveButtons.Add((page, id));
                        else
                            reactiveButtons.Remove((page, id));
                        break;
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

        this.WhenAnyValue(x => x.BaseBrightness).Throttle(TimeSpan.FromMilliseconds(2)).Subscribe(x =>
        {
            magicQCTRLProfile.baseBrightness = x;
            for (int page = 0; page < MAX_PAGES; page++)
                for (int id = 0; id < COLOUR_BUTTON_COUNT; id++)
                    usbDriver.SendColourConfig(page, id, magicQCTRLProfile);
        });
        this.WhenAnyValue(x => x.PressedBrightness).Throttle(TimeSpan.FromMilliseconds(30)).Subscribe(x =>
        {
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

        QueryNICs();

        // Auto connect
        ConnectExecute();

        // Auto load last profile
        OpenProfile(LAST_PROFILE);
    }

    public void OnExit()
    {
        Log("Shutting down...");
        SaveProfile(LAST_PROFILE);
        oscDriver.Dispose();
        magicQDriver.Dispose();
        usbDriver.Dispose();
        Log("Goodbye!");
    }

    #region Commands
    public void ConnectExecute()
    {
        Log("Connecting...");

        usbDriver.USBConnect();
        oscDriver.OSCConnect(nicAddresses.Count == 0 ? IPAddress.Any : nicAddresses[SelectedNIC], OSCRXPort, OSCTXPort);
        magicQDriver.MagicQConnect();
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
        if (saveFileDialog.ShowDialog() ?? false)
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
        if (openFileDialog.ShowDialog() ?? false)
        {
            OpenProfile(openFileDialog.FileName);
        }
    }

    public void OpenLogExecute()
    {
        logWindow = new();
        logWindow.DataContext = this;
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

    public void EditControlCommandExecute(string? controlId)
    {
        if (!int.TryParse(controlId, out int id))
        {
            Log("Invalid controlId! Something went very wrong!", LogLevel.Error);
            return;
        }

        Log($"Editing control {id}", LogLevel.Debug);
        for (int i = 0; i < ButtonNames.Count; i++)
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

    public void PageIncrementCommandExecute(string? direction)
    {
        if (string.IsNullOrEmpty(direction))
            return;

        if (direction == "+" && CurrentPage < magicQCTRLProfile.pages.Length - 1)
            CurrentPage++;

        if (direction == "-" && CurrentPage > 0)
            CurrentPage--;

        OnPropertyChanged(nameof(SelectedButton));
        OnPropertyChanged(nameof(ButtonEditors));
        OnPropertyChanged(nameof(ButtonEditor));

        Log($"Switched to page {CurrentPage}", LogLevel.Debug);
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
            sbyte delta = 0;
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
                    delta = (sbyte)-msg.delta;
                    break;
                default:
                    break;
            }

            ExecuteKeyAction(key, delta);
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
            if (key.customKeyCode != -1 && key.specialFunction <= MagicQCTRLSpecialFunction.SLampOnAll)
                magicQDriver.PressMQKey(key.customKeyCode);

            if (key.executeItemPage > 0 && key.executeItemIndex >= 0)
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

    private void QueryNICs()
    {
        nics.Clear();
        nicAddresses.Clear();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            var ipProps = nic.GetIPProperties();
            var nicAddr = ipProps.UnicastAddresses.Select(x => x.Address);
            if (nicAddr.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork) is IPAddress naddr
                && ipProps.GatewayAddresses.Count > 0)
            {
                nicAddresses.AddRange(nicAddr);
                foreach (var addr in nicAddr)
                    nics.Add($"{nic.Name}: {addr}");
                //Log($"\t{nic.Name}: {string.Join(", ", nicAddr)}");
            }
        }
    }

    private void MagicQDriver_OnKeyLightChange(MagicQCTRLButtonLight button, KeyLightState state)
    {
        // Not very efficient, but in practice arrays should be small, calls infrequent
        foreach (var (page, id) in reactiveButtons)
        {
            var b = magicQCTRLProfile.pages[page].keys[id];
            if (GetButtonLight(b.specialFunction) == button)
            {
                try
                {
                    usbDriver.SendColourConfig(page, id, magicQCTRLProfile, state != KeyLightState.Off);
                }
                catch { }
            }
        }
    }

    private static MagicQCTRLButtonLight GetButtonLight(MagicQCTRLSpecialFunction key)
    {
        return key switch
        {
            MagicQCTRLSpecialFunction.Go => MagicQCTRLButtonLight.Go,
            MagicQCTRLSpecialFunction.Pause => MagicQCTRLButtonLight.Pause,
            MagicQCTRLSpecialFunction.JumpBack => MagicQCTRLButtonLight.JumpPrevCue,
            MagicQCTRLSpecialFunction.JumpForward => MagicQCTRLButtonLight.JumpNextCue,

            //MagicQCTRLButtonLight.SelPB1,
            //MagicQCTRLButtonLight.SelPB2,
            //MagicQCTRLButtonLight.SelPB3,
            //MagicQCTRLButtonLight.SelPB4,
            //MagicQCTRLButtonLight.SelPB5,
            //MagicQCTRLButtonLight.SelPB6,
            //MagicQCTRLButtonLight.SelPB7,
            //MagicQCTRLButtonLight.SelPB8,
            //MagicQCTRLButtonLight.SelPB9,
            //MagicQCTRLButtonLight.SelPB10,
            //MagicQCTRLButtonLight.FlashPB1,
            //MagicQCTRLButtonLight.FlashPB2,
            //MagicQCTRLButtonLight.FlashPB3,
            //MagicQCTRLButtonLight.FlashPB4,
            //MagicQCTRLButtonLight.FlashPB5,
            //MagicQCTRLButtonLight.FlashPB6,
            //MagicQCTRLButtonLight.FlashPB7,
            //MagicQCTRLButtonLight.FlashPB8,
            //MagicQCTRLButtonLight.FlashPB9,
            //MagicQCTRLButtonLight.FlashPB10,
            MagicQCTRLSpecialFunction.GoPB1 => MagicQCTRLButtonLight.GoPB1,
            MagicQCTRLSpecialFunction.GoPB2 => MagicQCTRLButtonLight.GoPB2,
            MagicQCTRLSpecialFunction.GoPB3 => MagicQCTRLButtonLight.GoPB3,
            MagicQCTRLSpecialFunction.GoPB4 => MagicQCTRLButtonLight.GoPB4,
            MagicQCTRLSpecialFunction.GoPB5 => MagicQCTRLButtonLight.GoPB5,
            MagicQCTRLSpecialFunction.GoPB6 => MagicQCTRLButtonLight.GoPB6,
            MagicQCTRLSpecialFunction.GoPB7 => MagicQCTRLButtonLight.GoPB7,
            MagicQCTRLSpecialFunction.GoPB8 => MagicQCTRLButtonLight.GoPB8,
            MagicQCTRLSpecialFunction.GoPB9 => MagicQCTRLButtonLight.GoPB9,
            MagicQCTRLSpecialFunction.GoPB10 => MagicQCTRLButtonLight.GoPB10,
            MagicQCTRLSpecialFunction.PausePB1 => MagicQCTRLButtonLight.PausePB1,
            MagicQCTRLSpecialFunction.PausePB2 => MagicQCTRLButtonLight.PausePB2,
            MagicQCTRLSpecialFunction.PausePB3 => MagicQCTRLButtonLight.PausePB3,
            MagicQCTRLSpecialFunction.PausePB4 => MagicQCTRLButtonLight.PausePB4,
            MagicQCTRLSpecialFunction.PausePB5 => MagicQCTRLButtonLight.PausePB5,
            MagicQCTRLSpecialFunction.PausePB6 => MagicQCTRLButtonLight.PausePB6,
            MagicQCTRLSpecialFunction.PausePB7 => MagicQCTRLButtonLight.PausePB7,
            MagicQCTRLSpecialFunction.PausePB8 => MagicQCTRLButtonLight.PausePB8,
            MagicQCTRLSpecialFunction.PausePB9 => MagicQCTRLButtonLight.PausePB9,
            MagicQCTRLSpecialFunction.PausePB10 => MagicQCTRLButtonLight.PausePB10,
            //MagicQCTRLButtonLight.FlashGrandMaster,
            //MagicQCTRLButtonLight.FlashSubMaster,
            //MagicQCTRLButtonLight.FlashGo,
            //MagicQCTRLSpecialFunction.Release => MagicQCTRLButtonLight.Rel,
            //MagicQCTRLSpecialFunction.Sel => MagicQCTRLButtonLight.Sel,
            MagicQCTRLSpecialFunction.Clear => MagicQCTRLButtonLight.Clear,
            //MagicQCTRLSpecialFunction.Shift => MagicQCTRLButtonLight.Shift,
            MagicQCTRLSpecialFunction.Blind => MagicQCTRLButtonLight.Blind,
            //MagicQCTRLButtonLight.Backspace,
            MagicQCTRLSpecialFunction.Undo => MagicQCTRLButtonLight.Undo,
            MagicQCTRLSpecialFunction.Set => MagicQCTRLButtonLight.Set,

            MagicQCTRLSpecialFunction.Remove => MagicQCTRLButtonLight.Remove,
            MagicQCTRLSpecialFunction.Move => MagicQCTRLButtonLight.Move,
            MagicQCTRLSpecialFunction.Copy => MagicQCTRLButtonLight.Copy,

            MagicQCTRLSpecialFunction.Include => MagicQCTRLButtonLight.Include,
            MagicQCTRLSpecialFunction.Update => MagicQCTRLButtonLight.Update,
            MagicQCTRLSpecialFunction.Record => MagicQCTRLButtonLight.Record,

            MagicQCTRLSpecialFunction.Fan => MagicQCTRLButtonLight.Fan,
            MagicQCTRLSpecialFunction.Highlight => MagicQCTRLButtonLight.Highlight,
            MagicQCTRLSpecialFunction.Single => MagicQCTRLButtonLight.Single,
            MagicQCTRLSpecialFunction.OddEven => MagicQCTRLButtonLight.OddEven,

            _ => MagicQCTRLButtonLight.None
        };
    }

    internal static string FormatEnumString(string name)
    {
        // Rules:
        // Captialise first letter: setup -> Setup
        // Remove prepended underscores: _1 -> 1
        // Split numbers: Start12Now -> Start 12 Now
        // Split Pascal case, keeping acronyms grouped: LaunchNow -> Launch Now
        //                                              LaunchFXNow -> Launch FX Now
        //                                              MoveABit -> Move A Bit
        StringBuilder sb = new(name.Length + 5);
        bool lastWasDigit = true;
        bool lastWasUpper = true;
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (sb.Length == 0)
            {
                if (c == '_')
                    continue;
                sb.Append(char.ToUpper(c));
                lastWasDigit = char.IsDigit(c);
            }
            else
            {
                if (char.IsDigit(c))
                {
                    if (!lastWasDigit)
                        sb.Append(' ');
                    lastWasDigit = true;
                }
                else
                {
                    lastWasDigit = false;
                }

                if (char.IsUpper(c))
                {
                    bool nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
                    if (!lastWasUpper || nextIsLower)
                        sb.Append(' '); ;
                    lastWasUpper = true;
                }
                else
                {
                    lastWasUpper = false;
                }

                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    public static void Log(object message, LogLevel level = LogLevel.Info, [CallerMemberName] string caller = "")
    {
        lock (logListLock)
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

internal interface INotifyConnectionStatus
{
    public event Action<bool> OnConnectionStatusChanged;
    public bool IsConnected { get; }
}
