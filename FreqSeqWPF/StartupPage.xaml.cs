using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Diagnostics;
using Forms=System.Windows.Forms;
using FREQSeq;
namespace FreqSeqWPF
{
	public partial class StartupPage
	{
		 /// <summary>
        /// Path of the selected file
        /// </summary>
        public string SelectedFilePath { get; set; }
		 /// <summary>
        /// Flag to see if user requested to open a file from disk
        /// </summary>
        public bool ShowOpenFileDialog { get; set; }
		public StartupPage()
		{
			this.InitializeComponent();

			// Insert code required on object creation below this point.
		}
				/// <summary>
        /// Close this window and take the user to the empty workspace
        /// </summary>
        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            ShowOpenFileDialog = false;
            SelectedFilePath = null;
            Application.Current.Shutdown();
        }
		        /// <summary>
        /// Raised when user clicks the open file button
        /// </summary>
        private void OnOpenClick(object sender, RoutedEventArgs e)
        {
            ShowOpenFileDialog = true;
            //this.Close();
        }
		private void BeginClick(object sender, RoutedEventArgs e)
        {
        	   
		}
		void HandleRequestNavigate(object sender, RoutedEventArgs e)
	{
   string navigateUri = hl.NavigateUri.ToString();
   // if the URI somehow came from an untrusted source, make sure to
   // validate it before calling Process.Start(), e.g. check to see
   // the scheme is HTTP, etc.
	   try
		{
		Process.Start(new ProcessStartInfo(navigateUri));
		}
		catch(Exception thrown)
		{
			
		}
			e.Handled = true;
	}

        private void button1_Click(object sender, RoutedEventArgs e)
        {
           // NavigationService.Navigate(new XMLSelect());
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Forms.OpenFileDialog OFD = new System.Windows.Forms.OpenFileDialog();
            OFD.Filter = "XML Files|*.xml|All Files|*.*";
            Forms.DialogResult DR = OFD.ShowDialog();
            if (DR == Forms.DialogResult.OK)
            {
                App.AlleleSearcher = XML_Parser.CreateAlleleFinderFromXML(OFD.FileName);
            }
            
            Continue();
        }
        private void Continue()
        {
            //Forms.SaveFileDialog SFD = new Forms.SaveFileDialog();
            //SFD.Title = "Select Output File Name";
            //SFD.Filter = "CSV File (*.csv)|*.csv";
            
            //Forms.DialogResult DR = SFD.ShowDialog();
            //if (DR == Forms.DialogResult.OK)
            //{
            //    App.AlleleSearcher = XML_Parser.CreateAlleleFinderFromXML(SFD.FileName);
            //}
            //else
            //{
            //    MessageBox.Show("You must pick an output file name before continuing");
            //    Application.Current.Shutdown();
            //}
            NavigationService.Navigate(new ImportSeqPage());
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Wait for this feature to appear soon! For now only XML file loading is supported.","Coming Soon",MessageBoxButton.OK);

        }
	
	}
}