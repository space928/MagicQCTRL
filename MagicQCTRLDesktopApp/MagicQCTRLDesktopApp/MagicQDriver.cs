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
        //private MagicQNativeMethods.PressMQKey mqKeyFunction;
        private nint mqKeyFunctionAddr;
        private AssemblyFactory asmFactory;

        public MagicQDriver() 
        {

        }

        public bool Connect()
        {
            try
            {
                // TODO: Cleanup this mess...
                if (System.Diagnostics.Process.GetProcessesByName(procName).Length > 0)
                {
                    var proc = System.Diagnostics.Process.GetProcessesByName(procName)[0];
                    mqProcess = new ProcessSharp(proc, Process.NET.Memory.MemoryType.Remote);

                    asmFactory = new AssemblyFactory(mqProcess, new ReloadedAssembler());
                    var matcher = new Process.NET.Patterns.PatternScanner(mqProcess.ModuleFactory.MainModule);
                    var match = matcher.Find(new DwordPattern(MagicQNativeMethods.PressMQKeySignature));
                    mqKeyFunctionAddr = 0x0078e530;//match.BaseAddress;
                    //new RemoteFunction(mqProcess, offset.ReadAddress, "MQKey").GetDelegate<MagicQNativeMethods.PressMQKey>();
                } else
                {
                    throw new Exception("Couldn't find MagicQ process!");
                }
            } catch (Exception e) 
            {
                Log($"Error while attaching to MagicQ: {e}", LogLevel.Error);
            }

            return true;
        }

        public void Dispose()
        {

        }

        public void ExecuteCommand(MagicQCTRLSpecialFunction function)
        {
            if(function != MagicQCTRLSpecialFunction.None)
                asmFactory?.Execute<int>(mqKeyFunctionAddr, Process.NET.Native.Types.CallingConventions.Cdecl, (int)function);
        }

        public void PressMQKey(int keyId)
        {
            asmFactory?.Execute<int>(mqKeyFunctionAddr, Process.NET.Native.Types.CallingConventions.Cdecl, keyId);
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
    }
}
