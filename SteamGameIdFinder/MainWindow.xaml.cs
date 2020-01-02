using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SteamGameIdFinder
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

		private void gameNameTextBox_GotFocus(object sender, RoutedEventArgs e)
		{
			if(sender == null || e == null)
			{
				return;
			}
			TextBox? textBox = sender as TextBox;


		}

		private void gameNameTextBox_LostFocus(object sender, RoutedEventArgs e)
		{

		}
	}
}
