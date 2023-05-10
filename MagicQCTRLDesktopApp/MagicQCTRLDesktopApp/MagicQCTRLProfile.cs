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
        public MagicQCTRLColour keyColourOn;
        public MagicQCTRLColour keyColourOff;
    }

    [Serializable]
    public enum MagicQCTRLSpecialFunction
    {
        None,
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
