using System;
using Reloaded.Assembler;
using Process.NET;
using Process.NET.Assembly;
using Process.NET.Assembly.Assemblers;
using Process.NET.Patterns;
using static MagicQCTRLDesktopApp.ViewModel;
using Process.NET.Memory;
using Process.NET.Native.Types;
using System.Text;
using System.Linq;
using System.Runtime.InteropServices;
//using Reloaded.Hooks.Definitions.X86;
//using CallingConventions = Process.NET.Native.Types.CallingConventions;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

namespace MagicQCTRLDesktopApp;

internal partial class MagicQDriver : IDisposable, INotifyConnectionStatus
{
    public event Action<MagicQCTRLButtonLight, KeyLightState>? OnKeyLightChange;
    public event Action<bool>? OnConnectionStatusChanged;
    public bool IsConnected => initialized;

    private readonly string procName = "mqqt";

    private ProcessSharp? mqProcess;
    private AssemblyFactory? asmFactory;
    private bool initialized;
    private nint mqKeyFunctionAddr;
    private nint mqEncoderFunctionAddr;
    private nint mqOpenLayoutAddr;
    private nint mqSetExecItemStateAddr;
    private nint mqGetExecItemStateAddr;
    private nint mqLampOnOffAllAddr;
    private nint mqResetAllHeadsAddr;
    private nint mqSetKeyLightStateAddr;
    private nint mqGetKeyLightStateAddr;
    private nint mqKeyLightStateAddr;

    private volatile bool isDisposing;
    private Thread? mqPollingThread;
    private readonly KeyLightState[] mqKeyLightStates;
    private readonly KeyLightState[] mqPrevKeyLightStates;

    public MagicQDriver() 
    {
        mqKeyLightStates = new KeyLightState[256];
        mqPrevKeyLightStates = new KeyLightState[256];
    }

    public bool MagicQConnect()
    {
        try
        {
            Log("Searching for MagicQ process...");
            Dispose();
            var procs = System.Diagnostics.Process.GetProcessesByName(procName);
            if (procs.Length > 0)
            {
                mqProcess = new ProcessSharp(procs[0], Process.NET.Memory.MemoryType.Remote);

                if (procs.Length > 1)
                    Log($"More than one MagicQ process found! Connecting to pid: {mqProcess.Native.Id}", LogLevel.Warning);

                asmFactory = new AssemblyFactory(mqProcess, new ReloadedAssembler());
                Task.Run(FindHookAddresses).ContinueWith(_ =>
                {
                    mqPollingThread = new(PollMagicQ);
                    mqPollingThread.Start();

                    Log("Connected to MagicQ instance!");
                    initialized = true;
                    OnConnectionStatusChanged?.Invoke(initialized);
                });
            }
            else
            {
                throw new Exception("Couldn't find MagicQ process!");
            }
        } catch (Exception e) 
        {
            Log($"Error while attaching to MagicQ: {e}", LogLevel.Error);
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        OnConnectionStatusChanged?.Invoke(false);
        initialized = false;
        isDisposing = true;
        mqPollingThread?.Join();
        mqProcess?.Dispose();
        asmFactory?.Dispose();
        isDisposing = false;
    }

    private void FindHookAddresses()
    {
        if (mqProcess == null)
            return;

        var matcher = new PatternScanner(mqProcess.ModuleFactory.MainModule);

        var match = matcher.Find(new DwordPattern(MagicQNativeMethods.PressMQKeySignature));
        if(match.Found)
            mqKeyFunctionAddr = match.BaseAddress;
        else
            mqKeyFunctionAddr = 0x0078e530; // Correct for 1.9.3.8

        match = matcher.Find(new DwordPattern(MagicQNativeMethods.MQEncoderSignature));
        if(match.Found)
            mqEncoderFunctionAddr = match.BaseAddress;
        else
            mqEncoderFunctionAddr = 0x007f9b70; // Correct for 1.9.3.8

        match = matcher.Find(new DwordPattern(MagicQNativeMethods.OpenLayoutSignature));
        if (match.Found)
            mqOpenLayoutAddr = match.BaseAddress;
        else
            mqOpenLayoutAddr = 0x006879f0; // Correct for 1.9.3.8

        match = matcher.Find(new DwordPattern(MagicQNativeMethods.SetExecItemStateSignature));
        if (match.Found)
            mqSetExecItemStateAddr = match.BaseAddress;
        else
            mqSetExecItemStateAddr = 0x0058a960; // Correct for 1.9.3.8

        match = matcher.Find(new DwordPattern(MagicQNativeMethods.GetExecuteItemStateSignature));
        if (match.Found)
            mqGetExecItemStateAddr = match.BaseAddress;
        else
            mqGetExecItemStateAddr = 0x0062dbc0; // Correct for 1.9.3.8

        match = matcher.Find(new DwordPattern(MagicQNativeMethods.LampOnOffAllSignature));
        if (match.Found)
            mqLampOnOffAllAddr = match.BaseAddress;
        else
            mqLampOnOffAllAddr = 0x0052bfd0; // Correct for 1.9.3.8

        match = matcher.Find(new DwordPattern(MagicQNativeMethods.ResetAllHeadsSignature));
        if (match.Found)
            mqResetAllHeadsAddr = match.BaseAddress;
        else
            mqResetAllHeadsAddr = 0x0052c000; // Correct for 1.9.3.8

        match = matcher.Find(new DwordPattern(MagicQNativeMethods.SetKeyLightStateSignature));
        if (match.Found)
            mqSetKeyLightStateAddr = match.BaseAddress;
        else
            mqSetKeyLightStateAddr = 0x005eaaa0; // Correct for 1.9.3.8

        match = matcher.Find(new DwordPattern(MagicQNativeMethods.GetKeyLightStatesSignature));
        if (match.Found)
            mqGetKeyLightStateAddr = match.BaseAddress;
        else
            mqGetKeyLightStateAddr = 0x005e8990; // Correct for 1.9.3.8

        mqKeyLightStateAddr = mqProcess.Memory.Read<nint>(mqGetKeyLightStateAddr + 35);
    }

    private void HookCallbacks()
    {
        //Reloaded.Hooks.ReloadedHooks.Instance.CreateHook()
    }

    private void PollMagicQ()
    {
        //using var buttonLightStatesPool = mqProcess.MemoryFactory.Allocate("buttonLightStatesPool", 0xff, MemoryProtectionFlags.ReadWrite);

        while (!isDisposing && mqProcess != null)
        {
            if (ReadProcessMemory(mqProcess.Handle, mqKeyLightStateAddr, Unsafe.As<byte[]>(mqKeyLightStates), mqKeyLightStates.Length, out int _))
            {
                for (int i = 0; i < (int)MagicQCTRLButtonLight.__MaxVal; i++)
                {
                    var state = mqKeyLightStates[i];
                    if (state != mqPrevKeyLightStates[i])
                    {
                        mqPrevKeyLightStates[i] = state;
                        OnKeyLightChange?.Invoke((MagicQCTRLButtonLight)i, state);
                    }
                }
            }

            Thread.Sleep(50);
        }
    }

    /// <summary>
    /// Executes a MagicQ special function.
    /// </summary>
    /// <param name="function">the function to execute</param>
    public void ExecuteCommand(MagicQCTRLSpecialFunction function, int param = 0)
    {
        if (!initialized)
            return;

        switch(function)
        {
            case MagicQCTRLSpecialFunction.None: break;
            case MagicQCTRLSpecialFunction.SLampOnAll:
                LampOnOffAll(true);
                break;
            case MagicQCTRLSpecialFunction.SLampOffAll:
                LampOnOffAll(false);
                break;
            case MagicQCTRLSpecialFunction.SResetAll:
                ResetAllHeads();
                break;
            case MagicQCTRLSpecialFunction.SOpenLayout:
                OpenLayout(param);
                break;
            case MagicQCTRLSpecialFunction.SOpenLayout + 1:
            case MagicQCTRLSpecialFunction.SOpenLayout + 2:
            case MagicQCTRLSpecialFunction.SOpenLayout + 3:
            case MagicQCTRLSpecialFunction.SOpenLayout + 4:
            case MagicQCTRLSpecialFunction.SOpenLayout + 5:
            case MagicQCTRLSpecialFunction.SOpenLayout + 6:
            case MagicQCTRLSpecialFunction.SOpenLayout + 7:
            case MagicQCTRLSpecialFunction.SOpenLayout + 8:
            case MagicQCTRLSpecialFunction.SOpenLayout + 9:
            case MagicQCTRLSpecialFunction.SOpenLayout + 10:
                OpenLayout(function - MagicQCTRLSpecialFunction.SOpenLayout);
                break;
            case MagicQCTRLSpecialFunction.SSetKeyLight:
                asmFactory?.Execute<int>(mqSetKeyLightStateAddr, CallingConventions.Cdecl, param, true);
                break;
            default:
                PressMQKey((int)function);
                break;
        }
    }

    /// <summary>
    /// Presses the given MagicQ key.
    /// </summary>
    /// <param name="keyId">the MagicQ keyID to press</param>
    public void PressMQKey(int keyId)
    {
        if (!initialized)
            return;

        asmFactory?.Execute<int>(mqKeyFunctionAddr, CallingConventions.Cdecl, keyId);
    }

    /// <summary>
    /// Sends an encoder change message for given MagicQ encoder.
    /// </summary>
    /// <param name="encoder">the encoder to turn</param>
    /// <param name="delta">how much to rotate the encoder by</param>
    public void TurnEncoder(MagicQCTRLEncoderType encoder, int delta)
    {
        if (!initialized)
            return;

        asmFactory?.Execute(mqEncoderFunctionAddr, CallingConventions.Cdecl, (ushort)encoder, delta);
    }

    /// <summary>
    /// Opens the chosen layout in MagicQ
    /// </summary>
    /// <param name="layoutId">the layout number to open</param>
    public void OpenLayout(int layoutId)
    {
        if (!initialized)
            return;

        asmFactory?.Execute(mqOpenLayoutAddr, CallingConventions.Cdecl, (ushort)layoutId);
    }

    /// <summary>
    /// Sets the state of a given execute item
    /// </summary>
    /// <param name="execPage">the page of the execute item</param>
    /// <param name="execItem">the index of the execute item</param>
    /// <param name="state">the state to set the execute item to</param>
    public void SetExecItemState(ushort execPage, uint execItem, ExecuteItemCommand state)
    {
        if (!initialized)
            return;

        //var s = GetExecItemState(execPage, execItem, out ExecuteItemState execItemState, out string name);
        //Log($"Exec item state for {execPage}/{execItem} = {s}; \n\tstate = {execItemState}; \n\tname = {name}");

        asmFactory?.Execute(mqSetExecItemStateAddr, CallingConventions.Cdecl, execPage, execItem, (int)state);
    }

    public int GetExecItemState(ushort execPage, uint execItem, out ExecuteItemState execItemState, out string name, int mode = 0x1b)
    {
        if (!initialized || mqProcess == null)
        {
            name = string.Empty;
            execItemState = default;
            return 0;
        }

        using var resBuff = mqProcess.MemoryFactory.Allocate("GetExecItemStateRes", 0x23);
        using var nameBuff = mqProcess.MemoryFactory.Allocate("GetExecItemStateName", 0x10);

        var ret = asmFactory?.Execute<int>(mqGetExecItemStateAddr, CallingConventions.Cdecl, mode, execPage, execItem, resBuff.BaseAddress, nameBuff.BaseAddress) ?? 0;

        execItemState = resBuff.Read<ExecuteItemState>(0);
        name = Encoding.UTF8.GetString(nameBuff.Read(0, nameBuff.Size));

        return ret;
    }

    public void LampOnOffAll(bool state)
    {
        if (!initialized)
            return;

        asmFactory?.Execute(mqLampOnOffAllAddr, CallingConventions.Cdecl, state?1:0);
    }

    public void ResetAllHeads()
    {
        if (!initialized)
            return;

        asmFactory?.Execute(mqResetAllHeadsAddr, CallingConventions.Cdecl);
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReadProcessMemory(SafeMemoryHandle hProcess, nint lpBaseAddress, [Out] byte[] buffer, int size, out int lpNumberOfBytesRead);
}

public class ReloadedAssembler : IAssembler
{
    private readonly Assembler assembler = new();

    public byte[] Assemble(string asm)
    {
        return Assemble(asm, IntPtr.Zero);
    }

    public byte[] Assemble(string asm, IntPtr baseAddress)
    {
        asm = $"use32\norg 0x{baseAddress.ToInt64():X8}\n" + asm;
        return assembler.Assemble(asm);
    }
}

public class RemoteFunction : IDisposable
{
    private nint targetFunctionAddr;
    private IAllocatedMemory functionWrapperAddr;
    private AssemblyFactory asmFactory;

    public RemoteFunction(nint targetFunctionAddr, AssemblyFactory asmFactory)
    {
        this.targetFunctionAddr = targetFunctionAddr;
        this.asmFactory = asmFactory;
        Inject();
    }

    public void Execute(params object[] args)
    {
        if(functionWrapperAddr.IsValid)
            asmFactory.Execute(functionWrapperAddr.BaseAddress, args);
    }

    [MemberNotNull(nameof(functionWrapperAddr))]
    private void Inject()
    {
        string asm = $"""

            """;
        functionWrapperAddr = asmFactory.Inject(asm);
    }

    public void Dispose()
    {
        asmFactory.Dispose();
    }
}

public static class MagicQNativeMethods
{
    /// <summary>
    /// Delegate for the remote method which handles key presses in MagicQ
    /// </summary>
    /// <param name="keyID">the id code of the key to press</param>
    /// <returns>always true</returns>
    public delegate bool PressMQKey(int keyID);
    public static readonly string PressMQKeySignature = "55 89 e5 53 83 ec 14 8b 5d 08 c7 44 24 04 00 00 00 00 89 d8 0d 00 00 80 00 89 04 24";

    /// <summary>
    /// Delegate for the remote method which handles encoder turns in MagicQ
    /// </summary>
    /// <param name="encoderID"></param>
    /// <param name="delta"></param>
    public delegate void HandleMQEncoder(ushort encoderID, int delta);
    public static readonly string MQEncoderSignature = "55 89 e5 81 ec a8 00 00 00 8d 45 f8 8b 4d 08";

    public delegate void OpenLayout(ushort layout);
    public static readonly string OpenLayoutSignature = "55 89 e5 8b 45 08 66 3d 90 00 77 09";

    public delegate int SetExecItemState(ushort execPage, uint execItem, ExecuteItemCommand state);
    public static readonly string SetExecItemStateSignature = "55 89 e5 56 53 83 ec 10 8b 5d 10";

    public delegate int GetExecuteItemState(int param_1, ushort execPage, uint execItem, nint out_execItemState, nint out_Extra);
    public static readonly string GetExecuteItemStateSignature = "55 89 e5 53 83 ec 14 8b 45 08 8b 55 0c 8d 1c c5";

    public delegate void LampOnOffAll(bool state);
    public static readonly string LampOnOffAllSignature = "55 89 e5 66 83 7d 08 00 75 16";

    public delegate void ResetAllHeads();
    public static readonly string ResetAllHeadsSignature = "55 89 e5 83 ec 18 c7 04 24 d4 9c 30 01";

    //[Function(Reloaded.Hooks.Definitions.X86.CallingConventions.Cdecl)]
    public delegate int SetKeyLightState(uint keyLightID, byte state);
    public static readonly string SetKeyLightStateSignature = "55 b9 00 00 00 00 89 e5 56 53 83 ec 20";

    public delegate void GetKeyLightStates(int dst, uint length);
    public static readonly string GetKeyLightStatesSignature = "55 b8 e3 00 00 00 89 e5 53 8b 4d 0c 8b 5d 08 66 81 f9 e3 00 0f 47 c8 0f b7 c9 85 c9";
}

public enum ExecuteItemCommand : int
{
    Toggle, // In some cases acts as a toggle
    Unk1, // In some cases, releases
    Activate,
    Release,
    Unk4, // In some cases acts as a toggle
    Unk5,
    Unk6, // In some cases releases

    // Special cases
    //Toggle = -1
}

public enum KeyLightState : byte
{
    Off,
    Red
}

[StructLayout(LayoutKind.Explicit, Size = 0x23)]
public struct ExecuteItemState
{
    [FieldOffset(0)] public short faderVal;
    [FieldOffset(3)] public byte x;
    [FieldOffset(4)] public byte y;
    [FieldOffset(7)] public byte z;
    [FieldOffset(8)] public byte activeState;
    [FieldOffset(12)] public byte regionPos;
    [FieldOffset(19)] public byte widthHeight;

    public override string ToString()
    {
        return $"ExecuteItemState {{faderVal={faderVal}; x={x}; y={y}; z={z}; activeState={activeState}; regionPos={regionPos}; width={(widthHeight&0xf)+1}; height={(widthHeight>>0x10)+1}}}";
    }
}
