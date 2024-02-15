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

namespace MagicQCTRLDesktopApp
{
    internal class MagicQDriver : IDisposable
    {
        private readonly string procName = "mqqt";

        private ProcessSharp mqProcess;
        private nint mqKeyFunctionAddr;
        private nint mqEncoderFunctionAddr;
        private nint mqOpenLayoutAddr;
        private nint mqSetExecItemStateAddr;
        private nint mqGetExecItemStateAddr;
        private nint mqLampOnOffAllAddr;
        private nint mqResetAllHeadsAddr;
        private AssemblyFactory asmFactory;

        public MagicQDriver() 
        {

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
                    FindHookAddresses();
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

            Log("Connected to MagicQ instance!");
            return true;
        }

        public void Dispose()
        {
            mqProcess?.Dispose();
            asmFactory?.Dispose();
        }

        private void FindHookAddresses()
        {
            var matcher = new Process.NET.Patterns.PatternScanner(mqProcess.ModuleFactory.MainModule);

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
        }

        /// <summary>
        /// Executes a MagicQ special function.
        /// </summary>
        /// <param name="function">the function to execute</param>
        public void ExecuteCommand(MagicQCTRLSpecialFunction function, int param = 0)
        {
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
            asmFactory?.Execute<int>(mqKeyFunctionAddr, CallingConventions.Cdecl, keyId);
        }

        /// <summary>
        /// Sends an encoder change message for given MagicQ encoder.
        /// </summary>
        /// <param name="encoder">the encoder to turn</param>
        /// <param name="delta">how much to rotate the encoder by</param>
        public void TurnEncoder(MagicQCTRLEncoderType encoder, int delta)
        {
            asmFactory?.Execute(mqEncoderFunctionAddr, CallingConventions.Cdecl, (ushort)encoder, delta);
        }

        /// <summary>
        /// Opens the chosen layout in MagicQ
        /// </summary>
        /// <param name="layoutId">the layout number to open</param>
        public void OpenLayout(int layoutId)
        {
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
            //var s = GetExecItemState(execPage, execItem, out ExecuteItemState execItemState, out string name);
            //Log($"Exec item state for {execPage}/{execItem} = {s}; \n\tstate = {execItemState}; \n\tname = {name}");

            asmFactory?.Execute(mqSetExecItemStateAddr, CallingConventions.Cdecl, execPage, execItem, (int)state);
        }

        public int GetExecItemState(ushort execPage, uint execItem, out ExecuteItemState execItemState, out string name, int mode = 0x1b)
        {
            using var resBuff = mqProcess.MemoryFactory.Allocate("GetExecItemStateRes", 0x23);
            using var nameBuff = mqProcess.MemoryFactory.Allocate("GetExecItemStateName", 0x10);

            var ret = asmFactory?.Execute<int>(mqGetExecItemStateAddr, CallingConventions.Cdecl, mode, execPage, execItem, resBuff.BaseAddress, nameBuff.BaseAddress) ?? 0;

            execItemState = resBuff.Read<ExecuteItemState>(0);
            name = Encoding.UTF8.GetString(nameBuff.Read(0, nameBuff.Size));

            return ret;
        }

        public void LampOnOffAll(bool state)
        {
            asmFactory?.Execute(mqLampOnOffAllAddr, CallingConventions.Cdecl, state?1:0);
        }

        public void ResetAllHeads()
        {
            asmFactory?.Execute(mqResetAllHeadsAddr, CallingConventions.Cdecl);
        }
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
}
