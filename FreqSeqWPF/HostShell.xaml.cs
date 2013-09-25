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
using System.Windows.Navigation;
using FREQSeq;
namespace FreqSeqWPF
{
	/// <summary>
	/// Interaction logic for HostShell.xaml
	/// </summary>
	public partial class HostShell : NavigationWindow
	{
		public HostShell()
		{
			this.InitializeComponent();
           // App.AlleleSearcher = XML_Parser.CreateAlleleFinderFromXML("DefaultSettings.xml");
			// Insert code required on object creation below this point.
		}
	}
}