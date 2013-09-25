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

namespace FreqSeqWPF
{
	public partial class FinishPage
	{
		public FinishPage(List<string> Data)
		{
			this.InitializeComponent();
            lstResults.ItemsSource = Data;

			// Insert code required on object creation below this point.
		}

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
	}
}