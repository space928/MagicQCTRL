using ColorPicker.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using static MagicQCTRLDesktopApp.ViewModel;

namespace MagicQCTRLDesktopApp;

public class ButtonEditorViewModel : ObservableObject
{
    private int executeItemPage = 0;
    private int executeItemIndex = 0;
    #region Bindable Properties
    [Reactive] public int Id { get; init; }
    [Reactive] public string Name { get; set; } = "Button";
    [Reactive] public string OnPressOSC { get; set; } = "/";
    [Reactive] public string OnRotateOSC { get; set; } = "/";
    [Reactive] public ObservableCollection<SpecialFunctionItem> SpecialFunctions { get; private set; }
    [Reactive] public ListCollectionView SpecialFunctionsView { get; private set; }
    [Reactive] public SpecialFunctionItem SpecialFunction { get; set; } = new() { SpecialFunction = MagicQCTRLSpecialFunction.None, Category = "" };
    [Reactive] public ObservableCollection<MagicQCTRLEncoderType> EncoderFunctions { get; private set; }
    [Reactive] public ObservableCollection<string> EncoderFunctionNames { get; private set; }
    [Reactive] public MagicQCTRLEncoderType EncoderFunction { get; set; } = MagicQCTRLEncoderType.None;
    [Reactive] public int CustomKeyCode { get; set; } = -1;
    //[Reactive] public bool RespondToButtonLight { get; set; } = true;//MagicQCTRLButtonLight ButtonLightBinding { get; set; };
    [Reactive] public ColorState Colour { get; set; } = new ColorState(0.1, 0.1, 0.1, 1.0, 0.0, 0.0, 0.1, 0.0, 0.0, 0.05);
    [Reactive] public ColorState ActiveColour { get; set; }
    [Reactive] public int ExecuteItemPage { get => executeItemPage; set { executeItemPage = value; OnPropertyChanged(nameof(ExecuteItemEnabled)); } }
    [Reactive] public int ExecuteItemIndex { get => executeItemIndex; set { executeItemIndex = value; OnPropertyChanged(nameof(ExecuteItemEnabled)); } }
    [Reactive] public ObservableCollection<ExecuteItemCommand> ExecuteItemCommands { get; private set; }
    [Reactive] public ExecuteItemCommand ExecuteItemCommand { get; set; } = ExecuteItemCommand.Toggle;
    [Reactive] public bool ColorFromExecuteItem { get; set; } = true;

    [Reactive] public bool ExecuteItemEnabled => ExecuteItemPage > 0 && ExecuteItemIndex >= 0;
    [Reactive] public Visibility IsEncoder => (Id % BUTTON_COUNT) >= KEY_COUNT ? Visibility.Visible : Visibility.Collapsed;
    [Reactive] public Visibility IsKey => (Id % BUTTON_COUNT) < KEY_COUNT ? Visibility.Visible : Visibility.Collapsed;
    #endregion

    public ButtonEditorViewModel(int id)
    {
        this.Id = id;
        var specialFunctionItemsCategories = typeof(MagicQCTRLSpecialFunction)
            .GetMembers(BindingFlags.Public | BindingFlags.Static)
            .Select(x => (x.Name, x.GetCustomAttribute<ItemCategoryAttribute>()?.Category))
            .ToDictionary(x => x.Name, y => y.Category);

        SpecialFunctions = new ObservableCollection<SpecialFunctionItem>(
            Enum.GetValues<MagicQCTRLSpecialFunction>()
            .Select(x => new SpecialFunctionItem()
            {
                SpecialFunction = x,
                Category = specialFunctionItemsCategories.GetValueOrDefault(x.ToString(), string.Empty)!,
                SpecialFunctionName = FormatEnumString(x.ToString())
            }));

        SpecialFunctionsView = new(SpecialFunctions);
        SpecialFunctionsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SpecialFunctionItem.Category)));

        EncoderFunctions = new(Enum.GetValues<MagicQCTRLEncoderType>());
        EncoderFunctionNames = new(EncoderFunctions.Select(x => FormatEnumString(x.ToString())));
        EncoderFunction = MagicQCTRLEncoderType.None;

        ExecuteItemCommands = new(Enum.GetValues<ExecuteItemCommand>());
        ExecuteItemCommand = ExecuteItemCommand.Toggle;
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

public struct SpecialFunctionItem
{
    public SpecialFunctionItem() { }

    public MagicQCTRLSpecialFunction SpecialFunction { get; set; } = MagicQCTRLSpecialFunction.None;
    public string Category { get; set; } = "";
    public string SpecialFunctionName { get; set; } = "";
}

public static partial class ExtensionMethods
{
    public static void FromMagicQProfile(this ObservableCollection<ButtonEditorViewModel> x, MagicQCTRLProfile profile)
    {
        if (profile.pages == null || profile.pages.Length != MAX_PAGES)
        {
            Log($"Error Invalid MagicQCTRLProfile loaded! Pages={profile.pages}", LogLevel.Error);
            return;
        }

        if (x.Count < profile.pages.Length * profile.pages[0].keys.Length)
        {
            Log($"Error Invalid MagicQCTRLProfile loaded! Keys={profile.pages[0].keys}", LogLevel.Error);
            return;
        }

        int page = 0;
        int id = 0;
        foreach (var ed in x)
        {
            var key = profile.pages[page].keys[id];
            ed.Name = key.name;
            ed.OnPressOSC = key.oscMessagePress;
            ed.OnRotateOSC = key.oscMessageRotate;
            ed.Colour = profile.pages[page].keys[id].keyColourOff.ToColorState();
            //ed.Colour.SetColor(profile.pages[page].keys[id].keyColourOff);
            ed.ActiveColour = key.keyColourOn.ToColorState();
            //ed.ActiveColour.SetColor(profile.pages[page].keys[id].keyColourOn);
            var specialFunction = key.specialFunction;
            if (!Enum.IsDefined(specialFunction))
            {
                // Quick fix in case an invalid SpecialFunction is loaded.
                specialFunction = MagicQCTRLSpecialFunction.None;
                profile.pages[page].keys[id].specialFunction = specialFunction;
            }
            ed.SpecialFunction = new()
            {
                SpecialFunction = specialFunction,
                Category = specialFunction.GetAttributeOfType<ItemCategoryAttribute>()?.Category ?? string.Empty,
                SpecialFunctionName = FormatEnumString(specialFunction.ToString())
            };

            var encoderFunction = key.encoderFunction;
            if (!Enum.IsDefined(encoderFunction))
            {
                encoderFunction = MagicQCTRLEncoderType.None;
                profile.pages[page].keys[id].encoderFunction = MagicQCTRLEncoderType.None;
            }
            ed.EncoderFunction = encoderFunction;

            ed.ExecuteItemPage = key.executeItemPage;
            ed.ExecuteItemIndex = key.executeItemIndex;
            var execAction = key.executeItemAction;
            if (!Enum.IsDefined(execAction))
            {
                execAction = ExecuteItemCommand.Toggle;
                profile.pages[page].keys[id].executeItemAction = execAction;
            }
            ed.ExecuteItemCommand = execAction;

            id++;
            if (id >= BUTTON_COUNT)
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
            profile.pages[page].keys[id].specialFunction = ed.SpecialFunction.SpecialFunction;
            profile.pages[page].keys[id].encoderFunction = ed.EncoderFunction;
            profile.pages[page].keys[id].executeItemPage = ed.ExecuteItemPage;
            profile.pages[page].keys[id].executeItemIndex = ed.ExecuteItemIndex;
            profile.pages[page].keys[id].executeItemAction = ed.ExecuteItemCommand;

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

    /// <summary>
    /// Gets an attribute on an enum field value
    /// </summary>
    /// <typeparam name="T">The type of the attribute you want to retrieve</typeparam>
    /// <param name="enumVal">The enum value</param>
    /// <returns>The attribute of type T that exists on the enum value</returns>
    public static T? GetAttributeOfType<T>(this Enum enumVal) where T : Attribute
    {
        var type = enumVal.GetType();
        var memInfo = type.GetMember(enumVal.ToString());
        var attributes = memInfo[0].GetCustomAttributes(typeof(T), false);
        return (attributes.Length > 0) ? (T)attributes[0] : null;
    }
}