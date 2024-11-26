using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MagicQCTRLDesktopApp
{
    /// <summary>
    /// Interaction logic for LogWindow.xaml
    /// </summary>
    public partial class LogWindow : Window
    {
        public LogWindow()
        {
            InitializeComponent();
        }

        //https://stackoverflow.com/a/46548292
        private void ScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.OriginalSource is ScrollViewer scrollViewer &&
                Math.Abs(e.ExtentHeightChange) > 0.0)
            {
                scrollViewer.ScrollToBottom();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.LogList.Clear();
        }

        private void SaveToDiskButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new()
            {
                AddExtension = true,
                DereferenceLinks = true,
                Filter = "Text Files (*.txt)|*.txt|All files (*.*)|*.*",
                OverwritePrompt = true,
                Title = "Save Log File"
            };
            if (saveFileDialog.ShowDialog() ?? false)
            {
                try
                {
                    File.WriteAllLinesAsync(saveFileDialog.FileName, ViewModel.LogList).ContinueWith(_ =>
                    {
                        ViewModel.Log($"Log file exported to: {saveFileDialog.FileName}");
                    });
                }
                catch { }
            }
        }
    }
}
