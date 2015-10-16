using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace VitalSigns.Views
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class EqLogOutput : UserControl
    {
        EqLogViewModel ViewModel;

        public EqLogOutput()
        {
            InitializeComponent();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            // Open an ofd for .txt files
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            ofd.AddExtension = true;
            ofd.FileOk += ofd_FileOk;
            ofd.ShowDialog();
        }

        void ofd_FileOk(object sender, CancelEventArgs e)
        {
            // Format: eqlog_CHARNAME_SERVERNAME.txt
            OpenFileDialog ofd = sender as OpenFileDialog;

            // Dispose old view model, if there was one
            if (ViewModel != null)
            {
                ViewModel.Dispose();
                ViewModel = null;
            }

            // Using the file name, create character info
            ViewModel = new EqLogViewModel(ofd.FileName);
            this.DataContext = ViewModel.Subject;
        }
    }
}
