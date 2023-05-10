using ColorPicker.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MagicQCTRLDesktopApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            ((ViewModel)DataContext).CloseLogExecute();
            ((ViewModel)DataContext).OnExit();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            int id = 0;
            foreach(Button button in Enumerable.Concat(Keys.Children.Cast<Button>(), Enumerable.Concat(KnobsA.Children.Cast<Button>(), KnobsB.Children.Cast<Button>())))
            {
                button.SetBinding(ContentProperty, new MultiBinding()
                {
                    Bindings =
                    {
                        new Binding(nameof(ViewModel.ButtonEditors)),
                        new Binding(nameof(ViewModel.CurrentPage)),
                    },
                    Converter = new ButtonIndexNameConverter(id),
                    Mode = BindingMode.OneWay
                });
                button.SetBinding(BackgroundProperty, new MultiBinding()
                {
                    Bindings =
                    {
                        new Binding(nameof(ViewModel.ButtonEditors)),
                        new Binding(nameof(ViewModel.CurrentPage)),
                    },
                    Converter = new ButtonIndexColorConverter(id),
                    Mode = BindingMode.OneWay
                });
                id++;
                id %= ViewModel.BUTTON_COUNT;
            }
        }
    }

    public class ButtonIndexNameConverter : IMultiValueConverter
    {
        private int baseIndex;

        public ButtonIndexNameConverter(int baseIndex) 
        {
            this.baseIndex = baseIndex;
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if(values.Length != 2 || values[0] is not ObservableCollection<ButtonEditorViewModel> || values[1] is not int) 
                throw new NotImplementedException($"The button index converter does not support these object types. Expected: {{ObservableCollection<string>, int}} Found: {{{values.GetValue(0)?.GetType()}, {values.GetValue(1)?.GetType()}}}");

            return ((ObservableCollection<ButtonEditorViewModel>)values[0])[baseIndex + ((int)values[1]) * ViewModel.BUTTON_COUNT].Name;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ButtonIndexColorConverter : IMultiValueConverter
    {
        private int baseIndex;

        public ButtonIndexColorConverter(int baseIndex)
        {
            this.baseIndex = baseIndex;
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2 || values[0] is not ObservableCollection<ButtonEditorViewModel> || values[1] is not int)
                throw new NotImplementedException($"The button index converter does not support these object types. Expected: {{ObservableCollection<string>, int}} Found: {{{values.GetValue(0)?.GetType()}, {values.GetValue(1)?.GetType()}}}");

            var colorState = ((ObservableCollection<ButtonEditorViewModel>)values[0])[baseIndex + ((int)values[1]) * ViewModel.BUTTON_COUNT].Colour;
            return new SolidColorBrush(colorState.ToColor());
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public static partial class ExtensionMethods
    {
        public static Color ToColor(this ColorState x)
        {
            return Color.FromArgb((byte)(x.A*255), (byte)(x.RGB_R*255), (byte)(x.RGB_G*255), (byte)(x.RGB_B*255));
        }
    }
}
