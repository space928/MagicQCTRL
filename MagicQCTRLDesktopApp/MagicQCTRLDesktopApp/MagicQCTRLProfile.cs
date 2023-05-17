using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagicQCTRLDesktopApp
{
    [Serializable]
    public struct MagicQCTRLProfile
    {
        [MinLength(ViewModel.MAX_PAGES), MaxLength(ViewModel.MAX_PAGES)]
        public MagicQCTRLPage[] pages;
        public float baseBrightness;
        public float pressedBrightness;

        public MagicQCTRLProfile()
        {
            pages = new MagicQCTRLPage[]
            {
                new(),
                new(),
                new()
            };
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
        public string name;
        public string oscMessagePress;
        public string oscMessageRotate;
        public MagicQCTRLSpecialFunction specialFunction;
        public int customKeyCode;
        public MagicQCTRLColour keyColourOn;
        public MagicQCTRLColour keyColourOff;
    }

    public class ItemCategoryAttribute : Attribute
    {
        public string Category { get; init; }

        public ItemCategoryAttribute(string category) 
        {
            Category = category;
        }
    }

    [Serializable]
    public enum MagicQCTRLSpecialFunction
    {
        [ItemCategory("")] None = 0,

        [ItemCategory("Window Management")] Layout1 = 236,
        [ItemCategory("Window Management")] Layout2 = 237,
        [ItemCategory("Window Management")] Layout3 = 238,

        [ItemCategory("Window Management")] Prog = 208,
        [ItemCategory("Window Management")] Out = 209,
        [ItemCategory("Window Management")] Setup = 199,
        [ItemCategory("Window Management")] Macro = 200,
        [ItemCategory("Window Management")] Help = 201,
        [ItemCategory("Window Management")] Patch = 211,
        [ItemCategory("Window Management")] Vis = 221,
        [ItemCategory("Window Management")] Plot = 222,
        [ItemCategory("Window Management")] Execute = 229,

        [ItemCategory("Window Management")] Group = 192,
        [ItemCategory("Window Management")] Intensity = 193,
        [ItemCategory("Window Management")] Pos = 194,
        [ItemCategory("Window Management")] Col = 195,
        [ItemCategory("Window Management")] Beam = 196,

        [ItemCategory("Window Management")] FocusNextWindow = 183,
        [ItemCategory("Window Management")] ActivateWindow = 185,
        [ItemCategory("Window Management")] Close = 186,
        [ItemCategory("Window Management")] Maximise = 187,
        [ItemCategory("Window Management")] Restore = 188,

        [ItemCategory("Selection")] Highlight = 215,
        [ItemCategory("Selection")] Single = 179,
        [ItemCategory("Selection")] All = 180,
        [ItemCategory("Selection")] OddEven = 178,
        [ItemCategory("Selection")] Fan = 177,
        [ItemCategory("Selection")] NextHead = 181,
        [ItemCategory("Selection")] PrevHead = 182,
        [ItemCategory("Selection")] Pair = 207,

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

        [ItemCategory("Programming")] InvertPan = 206,

        [ItemCategory("Playback")] Go = 290,
        [ItemCategory("Playback")] Pause = 287,
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
    }

    [Serializable]
    public struct MagicQCTRLColour
    {
        public byte r, g, b;

        public static implicit operator Color(MagicQCTRLColour other) => Color.FromArgb(255, other.r, other.g, other.b);
        public static implicit operator MagicQCTRLColour(Color other) => new() { r=other.R, g=other.G,  b=other.B };
        public static MagicQCTRLColour operator *(MagicQCTRLColour a, float x) => new() { 
            r= (byte)Math.Clamp(a.r * x, 0, 255),
            g= (byte)Math.Clamp(a.g * x, 0, 255),
            b= (byte)Math.Clamp(a.b * x, 0, 255) 
        };

        public MagicQCTRLColour Pow(float x) => new()
        {
            r = (byte)Math.Clamp(Math.Pow(r/255f, x)*255, 0, 255),
            g = (byte)Math.Clamp(Math.Pow(g/255f, x)*255, 0, 255),
            b = (byte)Math.Clamp(Math.Pow(b/255f, x)*255, 0, 255)
        };
    }
}
