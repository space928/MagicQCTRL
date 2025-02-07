using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagicQCTRLDesktopApp;

[Serializable]
public struct MagicQCTRLProfile
{
    [MinLength(ViewModel.MAX_PAGES), MaxLength(ViewModel.MAX_PAGES)]
    public MagicQCTRLPage[] pages;
    public float baseBrightness;
    public float pressedBrightness;

    public MagicQCTRLProfile()
    {
        pages = Enumerable.Range(0, ViewModel.MAX_PAGES).Select(x => new MagicQCTRLPage()).ToArray();
        baseBrightness = 0.5f;
        pressedBrightness = 3.0f;
    }
}

[Serializable]
public struct MagicQCTRLPage
{
    [MinLength(ViewModel.BUTTON_COUNT), MaxLength(ViewModel.BUTTON_COUNT)]
    public MagicQCTRLKey[] keys;

    public MagicQCTRLPage()
    {
        keys = new MagicQCTRLKey[ViewModel.BUTTON_COUNT];
    }
}

[Serializable]
public struct MagicQCTRLEncoder
{
    public string oscMessage;
    public MagicQCTRLSpecialFunction specialFunction;
    public float scaleValue;
}

[Serializable]
public struct MagicQCTRLKey
{
    public string name = string.Empty;
    public string oscMessagePress = string.Empty;
    public string oscMessageRotate = string.Empty;
    public MagicQCTRLSpecialFunction specialFunction = MagicQCTRLSpecialFunction.None;
    public int customKeyCode;
    public MagicQCTRLEncoderType encoderFunction = MagicQCTRLEncoderType.None;
    public MagicQCTRLColour keyColourOn;
    public MagicQCTRLColour keyColourOff;
    public int executeItemPage;
    public int executeItemIndex;
    public ExecuteItemCommand executeItemAction;

    public MagicQCTRLKey() { }
}

[AttributeUsage(AttributeTargets.Field)]
public class ItemCategoryAttribute(string category) : Attribute
{
    public string Category { get; init; } = category;
}

[Serializable]
public enum MagicQCTRLSpecialFunction
{
    [ItemCategory("")] None = 0,

    [ItemCategory("Window Management")] Layout1 = SOpenLayout + 1,//224 | 0x100000,// 236, // The keycode values work sometimes, but often crash
    [ItemCategory("Window Management")] Layout2 = SOpenLayout + 2,//225 | 0x100000,// 237,
    [ItemCategory("Window Management")] Layout3 = SOpenLayout + 3,//226 | 0x100000,// 238,

    [ItemCategory("Window Management")] Prog = 208,
    [ItemCategory("Window Management")] Out = 209,
    [ItemCategory("Window Management")] Setup = 199,
    [ItemCategory("Window Management")] Macro = 200,
    [ItemCategory("Window Management")] Help = 201,
    [ItemCategory("Window Management")] Patch = 211,
    [ItemCategory("Window Management")] Vis = 221,
    [ItemCategory("Window Management")] Plot = 222,
    [ItemCategory("Window Management")] Execute = 229,
    [ItemCategory("Window Management")] Page = 203,
    [ItemCategory("Window Management")] StackStore = 204,
    [ItemCategory("Window Management")] CueStore = 205,
    [ItemCategory("Window Management")] FXStore = 210,
    [ItemCategory("Window Management")] PlaybackStore = 212,
    [ItemCategory("Window Management")] CueStack = 213,
    [ItemCategory("Window Management")] Timeline = 227,
    [ItemCategory("Window Management")] Media = 228,

    [ItemCategory("Window Management")] Group = 192,
    [ItemCategory("Window Management")] Intensity = 193,
    [ItemCategory("Window Management")] Pos = 194,
    [ItemCategory("Window Management")] Col = 195,
    [ItemCategory("Window Management")] Beam = 196,
    [ItemCategory("Window Management")] Cue = 197,

    [ItemCategory("Window Management")] FocusNextWindow = 183,
    [ItemCategory("Window Management")] ActivateWindow = 185,
    [ItemCategory("Window Management")] Close = 186,
    [ItemCategory("Window Management")] CloseAll = 230,
    [ItemCategory("Window Management")] Maximise = 187,
    [ItemCategory("Window Management")] Restore = 188,
    [ItemCategory("Window Management")] ScrollLeft = 216,
    [ItemCategory("Window Management")] ScrollRight = 217,
    [ItemCategory("Window Management")] NextPage = 291,
    [ItemCategory("Window Management")] PrevPage = 293,

    [ItemCategory("Soft Keys")] Soft1 = 305,
    [ItemCategory("Soft Keys")] Soft2 = 306,
    [ItemCategory("Soft Keys")] Soft3 = 307,
    [ItemCategory("Soft Keys")] Soft4 = 308,
    [ItemCategory("Soft Keys")] Soft5 = 309,
    [ItemCategory("Soft Keys")] Soft6 = 310,
    [ItemCategory("Soft Keys")] Soft7 = 311,
    [ItemCategory("Soft Keys")] Soft8 = 312,
    [ItemCategory("Soft Keys")] Soft9 = 313,
    [ItemCategory("Soft Keys")] Soft10 = 314,
    [ItemCategory("Soft Keys")] Soft11 = 315,
    [ItemCategory("Soft Keys")] Soft12 = 316,

    [ItemCategory("Soft Keys")] SoftA = 317,
    [ItemCategory("Soft Keys")] SoftB = 318,
    [ItemCategory("Soft Keys")] SoftC = 319,
    [ItemCategory("Soft Keys")] SoftD = 320,
    [ItemCategory("Soft Keys")] SoftE = 321,
    [ItemCategory("Soft Keys")] SoftF = 322,
    [ItemCategory("Soft Keys")] SoftX = 323,
    [ItemCategory("Soft Keys")] SoftY = 324,

    [ItemCategory("Selection")] Highlight = 215,
    [ItemCategory("Selection")] Single = 179,
    [ItemCategory("Selection")] All = 180,
    [ItemCategory("Selection")] OddEven = 178,
    [ItemCategory("Selection")] Fan = 177,
    [ItemCategory("Selection")] NextHead = 181,
    [ItemCategory("Selection")] PrevHead = 182,
    [ItemCategory("Selection")] Pair = 207,
    [ItemCategory("Selection")] Sel = 164,
    [ItemCategory("Selection")] Release = 214,

    [ItemCategory("Programming")] Include = 174,
    [ItemCategory("Programming")] Update = 170,
    [ItemCategory("Programming")] Record = 175,

    [ItemCategory("Programming")] Clear = 167,
    [ItemCategory("Programming")] Set = 172,
    [ItemCategory("Programming")] Remove = 171,
    [ItemCategory("Programming")] Move = 173,
    [ItemCategory("Programming")] Undo = 165,
    [ItemCategory("Programming")] Copy = 169,
    [ItemCategory("Programming")] Blind = 198,
    [ItemCategory("Programming")] Locate = 176,
    [ItemCategory("Programming")] Head = 189,
    [ItemCategory("Programming")] Time = 190,
    [ItemCategory("Programming")] Goto = 191,

    [ItemCategory("Programming")] InvertPan = 206,
    [ItemCategory("Programming")] Thru = 160,
    [ItemCategory("Programming")] Full = 161,
    [ItemCategory("Programming")] At = 162,

    [ItemCategory("Playback")] Go = 290,
    [ItemCategory("Playback")] Pause = 287,
    [ItemCategory("Playback")] JumpForward = 219,
    [ItemCategory("Playback")] JumpBack = 220,
    [ItemCategory("Playback")] AddSwap = 218,
    [ItemCategory("Playback")] GoPB1 = 276,
    [ItemCategory("Playback")] GoPB2 = 277,
    [ItemCategory("Playback")] GoPB3 = 278,
    [ItemCategory("Playback")] GoPB4 = 279,
    [ItemCategory("Playback")] GoPB5 = 280,
    [ItemCategory("Playback")] GoPB6 = 281,
    [ItemCategory("Playback")] GoPB7 = 282,
    [ItemCategory("Playback")] GoPB8 = 283,
    [ItemCategory("Playback")] GoPB9 = 284,
    [ItemCategory("Playback")] GoPB10 = 285,
    [ItemCategory("Playback")] PausePB1 = 266,
    [ItemCategory("Playback")] PausePB2 = 267,
    [ItemCategory("Playback")] PausePB3 = 268,
    [ItemCategory("Playback")] PausePB4 = 269,
    [ItemCategory("Playback")] PausePB5 = 270,
    [ItemCategory("Playback")] PausePB6 = 271,
    [ItemCategory("Playback")] PausePB7 = 272,
    [ItemCategory("Playback")] PausePB8 = 273,
    [ItemCategory("Playback")] PausePB9 = 274,
    [ItemCategory("Playback")] PausePB10 = 275,

    [ItemCategory("Other")] LampOnAll = 235,
    [ItemCategory("Other")] QuickSave = 265,

    [ItemCategory("Special")] SLampOnAll = 1000,
    [ItemCategory("Special")] SLampOffAll = 1001,
    [ItemCategory("Special")] SResetAll = 1002,
    [ItemCategory("Special")] SOpenLayout = 1003,
    //1004 // Reserved space for SOpenLayout overrides
    //1005
    //1006
    //1007
    //1008
    //1009
    //1010
    //1011
    //1012
    //1013
    [ItemCategory("Special")] SSetKeyLight = 2000,
}

[Serializable]
public enum MagicQCTRLButtonLight : byte
{
    None = 255,

    [ItemCategory("Playback")] Go = 31,
    [ItemCategory("Playback")] Pause = 30,
    [ItemCategory("Playback")] JumpPrevCue = 34,
    [ItemCategory("Playback")] JumpNextCue = 35,

    [ItemCategory("Playback")] SelPB1 = 39,
    [ItemCategory("Playback")] SelPB2 = 40,
    [ItemCategory("Playback")] SelPB3 = 41,
    [ItemCategory("Playback")] SelPB4 = 42,
    [ItemCategory("Playback")] SelPB5 = 43,
    [ItemCategory("Playback")] SelPB6 = 44,
    [ItemCategory("Playback")] SelPB7 = 45,
    [ItemCategory("Playback")] SelPB8 = 46,
    [ItemCategory("Playback")] SelPB9 = 47,
    [ItemCategory("Playback")] SelPB10 = 48,
    [ItemCategory("Playback")] FlashPB1 = 0,
    [ItemCategory("Playback")] FlashPB2 = 1,
    [ItemCategory("Playback")] FlashPB3 = 2,
    [ItemCategory("Playback")] FlashPB4 = 3,
    [ItemCategory("Playback")] FlashPB5 = 4,
    [ItemCategory("Playback")] FlashPB6 = 5,
    [ItemCategory("Playback")] FlashPB7 = 6,
    [ItemCategory("Playback")] FlashPB8 = 7,
    [ItemCategory("Playback")] FlashPB9 = 8,
    [ItemCategory("Playback")] FlashPB10 = 9,
    [ItemCategory("Playback")] GoPB1 = 20,
    [ItemCategory("Playback")] GoPB2 = 21,
    [ItemCategory("Playback")] GoPB3 = 22,
    [ItemCategory("Playback")] GoPB4 = 23,
    [ItemCategory("Playback")] GoPB5 = 24,
    [ItemCategory("Playback")] GoPB6 = 25,
    [ItemCategory("Playback")] GoPB7 = 26,
    [ItemCategory("Playback")] GoPB8 = 27,
    [ItemCategory("Playback")] GoPB9 = 28,
    [ItemCategory("Playback")] GoPB10 = 29,
    [ItemCategory("Playback")] PausePB1 = 10,
    [ItemCategory("Playback")] PausePB2 = 11,
    [ItemCategory("Playback")] PausePB3 = 12,
    [ItemCategory("Playback")] PausePB4 = 13,
    [ItemCategory("Playback")] PausePB5 = 14,
    [ItemCategory("Playback")] PausePB6 = 15,
    [ItemCategory("Playback")] PausePB7 = 16,
    [ItemCategory("Playback")] PausePB8 = 17,
    [ItemCategory("Playback")] PausePB9 = 18,
    [ItemCategory("Playback")] PausePB10 = 19,
    [ItemCategory("Playback")] FlashGrandMaster = 32,
    [ItemCategory("Playback")] FlashSubMaster = 33,
    [ItemCategory("Playback")] FlashGo = 38,
    [ItemCategory("Programming")] Rel = 49,
    [ItemCategory("Programming")] Sel = 51,
    [ItemCategory("Programming")] Clear = 53,

    [ItemCategory("Programming")] Shift = 50,
    [ItemCategory("Programming")] Blind = 52,
    [ItemCategory("Programming")] Backspace = 54,

    [ItemCategory("Programming")] Undo = 55,
    [ItemCategory("Programming")] Set = 56,

    [ItemCategory("Programming")] Remove = 57,
    [ItemCategory("Programming")] Move = 59,
    [ItemCategory("Programming")] Copy = 61,

    [ItemCategory("Programming")] Include = 58,
    [ItemCategory("Programming")] Update = 60,
    [ItemCategory("Programming")] Record = 62,

    [ItemCategory("Selection")] Fan = 63,
    [ItemCategory("Selection")] Highlight = 64,
    [ItemCategory("Selection")] Single = 67,
    [ItemCategory("Selection")] OddEven = 68,

    __MaxVal
}

[Serializable]
public enum MagicQCTRLEncoderType : ushort
{
    X = 0,
    Y = 1,
    F = 2,
    E = 3,
    D = 4,
    C = 5,
    B = 6,
    A = 7,
    _1 = 8,
    _2 = 9,
    _3 = 10,
    _4 = 11,
    I = 12,
    None = ushort.MaxValue
}

[Serializable]
public struct MagicQCTRLColour
{
    public byte r, g, b;

    public static implicit operator Color(MagicQCTRLColour other) => Color.FromArgb(255, other.r, other.g, other.b);
    public static implicit operator MagicQCTRLColour(Color other) => new() { r = other.R, g = other.G, b = other.B };
    public static MagicQCTRLColour operator *(MagicQCTRLColour a, float x) => new()
    {
        r = (byte)Math.Clamp(a.r * x, 0, 255),
        g = (byte)Math.Clamp(a.g * x, 0, 255),
        b = (byte)Math.Clamp(a.b * x, 0, 255)
    };

    public MagicQCTRLColour Pow(float x) => new()
    {
        r = (byte)Math.Clamp(Math.Pow(r / 255f, x) * 255, 0, 255),
        g = (byte)Math.Clamp(Math.Pow(g / 255f, x) * 255, 0, 255),
        b = (byte)Math.Clamp(Math.Pow(b / 255f, x) * 255, 0, 255)
    };
}
