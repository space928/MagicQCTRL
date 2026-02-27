using Process.NET.Modules;
using Process.NET.Patterns;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MagicQCTRLDesktopApp;

public class FastPatternScanner : IPatternScanner
{
    private readonly IProcessModule _module;

    public byte[] Data { get; }

    public FastPatternScanner(IProcessModule module)
    {
        _module = module;
        Data = module.Read(0, _module.Size);
    }

    public PatternScanResult Find(IMemoryPattern pattern)
    {
        if (pattern.PatternType != MemoryPatternType.Function)
        {
            return FindDataPattern(pattern);
        }

        return FindFunctionPattern(pattern);
    }

    private PatternScanResult FindFunctionPattern(IMemoryPattern pattern)
    {
        byte[] patternData = Data;
        int num = patternData.Length;
        int offset;
        if (!pattern.GetMask().Contains('?'))
        {
            // Fast path for exact no mask
            var res = patternData.AsSpan().IndexOf(pattern.GetBytes().ToArray());
            if (res >= 0)
            {
                return new PatternScanResult
                {
                    BaseAddress = _module.BaseAddress + res,
                    ReadAddress = _module.BaseAddress + res,
                    Offset = res,
                    Found = true
                };
            }
        }
        for (offset = 0; offset < num; offset++)
        {
            if (!pattern.GetMask().Where((char m, int b) => m == 'x' && pattern.GetBytes()[b] != patternData[b + offset]).Any())
            {
                return new PatternScanResult
                {
                    BaseAddress = _module.BaseAddress + offset,
                    ReadAddress = _module.BaseAddress + offset,
                    Offset = offset,
                    Found = true
                };
            }
        }

        return new PatternScanResult
        {
            BaseAddress = IntPtr.Zero,
            ReadAddress = IntPtr.Zero,
            Offset = 0,
            Found = false
        };
    }

    private PatternScanResult FindDataPattern(IMemoryPattern pattern)
    {
        byte[] patternData = Data;
        IList<byte> patternBytes = pattern.GetBytes();
        string mask = pattern.GetMask();
        PatternScanResult result = default;
        int offset;
        for (offset = 0; offset < patternData.Length; offset++)
        {
            if (!mask.Where((char m, int b) => m == 'x' && patternBytes[b] != patternData[b + offset]).Any())
            {
                result.Found = true;
                result.ReadAddress = _module.Read<IntPtr>(offset + pattern.Offset);
                result.BaseAddress = new IntPtr(result.ReadAddress.ToInt64() - _module.BaseAddress.ToInt64());
                result.Offset = offset;
                return result;
            }
        }

        result.Found = false;
        result.Offset = 0;
        result.ReadAddress = IntPtr.Zero;
        result.BaseAddress = IntPtr.Zero;
        return result;
    }
}
