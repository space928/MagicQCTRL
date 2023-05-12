using ColorPicker.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;
using System.Windows.Media;
using static MagicQCTRLDesktopApp.ViewModel;

namespace MagicQCTRLDesktopApp
{
    public class ButtonEditorViewModel : ObservableObject
    {
        #region Bindable Properties
        [Reactive] public int Id { get; init; }
        [Reactive] public string Name { get; set; } = "Button";
        [Reactive] public string OnPressOSC { get; set; } = "/";
        [Reactive] public string OnRotateOSC { get; set; } = "/";
        [Reactive] public ObservableCollection<MagicQCTRLSpecialFunction> SpecialFunctions { get; private set; }
        [Reactive] public MagicQCTRLSpecialFunction SpecialFunction { get; set; } = MagicQCTRLSpecialFunction.None;
        [Reactive] public ColorState Colour { get; set; } = new ColorState(0.1, 0.1, 0.1, 1.0, 0.0, 0.0, 0.1, 0.0, 0.0, 0.05);
        [Reactive] public ColorState ActiveColour { get; set; }
        #endregion

        public ButtonEditorViewModel(int id)
        {
            this.Id = id;
            SpecialFunctions = new ObservableCollection<MagicQCTRLSpecialFunction>(Enum.GetValues<MagicQCTRLSpecialFunction>());
        }

        /// <summary>
        /// I don't know why but the colour seem to need an extra little notification when you update them
        /// </summary>
        public void OnColourUpdate()
        {
            OnPropertyChanged(nameof(Colour));
            OnPropertyChanged(nameof(ActiveColour));
        }
    }

    public static partial class ExtensionMethods
    {
        public static void FromMagicQProfile(this ObservableCollection<ButtonEditorViewModel> x, MagicQCTRLProfile profile)
        {
            if(profile.pages == null || profile.pages.Length != MAX_PAGES)
            {
                Log($"Error Invalid MagicQCTRLProfile loaded! Pages={profile.pages}", LogLevel.Error);
                return;
            }

            if(x.Count < profile.pages.Length * profile.pages[0].keys.Length)
            {
                Log($"Error Invalid MagicQCTRLProfile loaded! Keys={profile.pages[0].keys}", LogLevel.Error);
                return;
            }

            int page = 0;
            int id = 0;
            foreach(var ed in x)
            {
                ed.Name = profile.pages[page].keys[id].name;
                ed.OnPressOSC = profile.pages[page].keys[id].oscMessagePress;
                ed.OnRotateOSC = profile.pages[page].keys[id].oscMessageRotate;
                ed.Colour = profile.pages[page].keys[id].keyColourOff.ToColorState();
                //ed.Colour.SetColor(profile.pages[page].keys[id].keyColourOff);
                ed.ActiveColour = profile.pages[page].keys[id].keyColourOn.ToColorState();
                //ed.ActiveColour.SetColor(profile.pages[page].keys[id].keyColourOn);
                ed.SpecialFunction = profile.pages[page].keys[id].specialFunction;

                id++;
                if(id >= BUTTON_COUNT)
                {
                    id = 0;
                    page++;
                }
            }
        }

        public static void ToMagicQProfile(this ObservableCollection<ButtonEditorViewModel> x, ref MagicQCTRLProfile profile)
        {
            // Reallocate the profile
            if (profile.pages == null || profile.pages.Length != MAX_PAGES)
            {
                profile.pages = new MagicQCTRLPage[MAX_PAGES];
                for (int i = 0; i < profile.pages.Length; i++)
                    profile.pages[i].keys = new MagicQCTRLKey[BUTTON_COUNT];
            }

            // Copy all properties accross
            int page = 0;
            int id = 0;
            foreach (var ed in x)
            {
                profile.pages[page].keys[id].name = ed.Name;
                profile.pages[page].keys[id].oscMessagePress = ed.OnPressOSC;
                profile.pages[page].keys[id].oscMessageRotate = ed.OnRotateOSC;
                profile.pages[page].keys[id].keyColourOff = ed.Colour.ToColor();
                profile.pages[page].keys[id].keyColourOn = ed.ActiveColour.ToColor();
                profile.pages[page].keys[id].specialFunction = ed.SpecialFunction;

                id++;
                if (id >= BUTTON_COUNT)
                {
                    id = 0;
                    page++;
                }
            }
        }

        public static void SetColor(this ColorState x, MagicQCTRLColour colour)
        {
            x.SetARGB(1, colour.r / 255d, colour.g / 255d, colour.b / 255d);
        }

        public static ColorState ToColorState(this MagicQCTRLColour x)
        {
            ColorState c = new();/*new()
            {
                A=1,
                RGB_R=x.r/255d,
                RGB_G=x.g/255d,
                RGB_B=x.b/255d
            };*/
            c.SetARGB(1, x.r / 255d, x.g / 255d, x.b / 255d);

            return c;
        }
    }
}