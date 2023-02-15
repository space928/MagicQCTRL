using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagicQCTRLDesktopApp
{
    [Serializable]
    public struct MagicQCTRLProfile
    {
        public MagicQCTRLPage[] pages;
    }

    [Serializable]
    public struct MagicQCTRLPage
    {
        [MaxLength(8)]
        public MagicQCTRLEncoder[] encoders;
        [MaxLength(8)]
        public MagicQCTRLEncoderButton[] encoderButtons;
        [MaxLength(12)]
        public MagicQCTRLKey[] keys;
    }

    [Serializable]
    public struct MagicQCTRLEncoder
    {
        public string oscMessage;
        public MagicQCTRLSpecialFunction specialFunction;
        public float scaleValue;
    }

    [Serializable]
    public struct MagicQCTRLEncoderButton
    {
        public string oscMessageOn;
        public string oscMessageOff;
        public MagicQCTRLSpecialFunction specialFunction;
    }

    [Serializable]
    public struct MagicQCTRLKey
    {
        public string oscMessageOn;
        public string oscMessageOff;
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

        public static implicit operator Color(MagicQCTRLColour other) => Color.FromArgb(other.r, other.g, other.b);
    }
}
