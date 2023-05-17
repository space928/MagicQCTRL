using System;
using Reloaded.Assembler;
using Process.NET;
using Process.NET.Assembly;
using Process.NET.Assembly.Assemblers;
using Process.NET.Patterns;
using static MagicQCTRLDesktopApp.ViewModel;

namespace MagicQCTRLDesktopApp
{
    internal class MagicQDriver : IDisposable
    {
        private readonly string procName = "mqqt";

        private ProcessSharp mqProcess;
        private nint mqKeyFunctionAddr;
        private nint mqEncoderFunctionAddr;
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
        }

        /// <summary>
        /// Executes a MagicQ special function.
        /// </summary>
        /// <param name="function">the function to execute</param>
        public void ExecuteCommand(MagicQCTRLSpecialFunction function)
        {
            if(function != MagicQCTRLSpecialFunction.None)
                asmFactory?.Execute<int>(mqKeyFunctionAddr, Process.NET.Native.Types.CallingConventions.Cdecl, (int)function);
        }

        /// <summary>
        /// Presses the given MagicQ key.
        /// </summary>
        /// <param name="keyId">the MagicQ keyID to press</param>
        public void PressMQKey(int keyId)
        {
            asmFactory?.Execute<int>(mqKeyFunctionAddr, Process.NET.Native.Types.CallingConventions.Cdecl, keyId);
        }

        /// <summary>
        /// Sends an encoder change message for given MagicQ encoder.
        /// </summary>
        /// <param name="encoder">the encoder to turn</param>
        /// <param name="delta">how much to rotate the encoder by</param>
        public void TurnEncoder(MagicQCTRLEncoderType encoder, int delta)
        {
            asmFactory?.Execute(mqEncoderFunctionAddr, Process.NET.Native.Types.CallingConventions.Cdecl, (ushort)encoder, delta);
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
    }
}
