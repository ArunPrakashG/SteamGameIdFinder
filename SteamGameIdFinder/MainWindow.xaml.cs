using System;
using System.Windows;
using System.Windows.Controls;

namespace SteamGameIdFinder
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private readonly Core Core;

		public MainWindow()
		{
			InitializeComponent();
			Core = new Core(this);
		}

		private void gameNameTextBox_GotFocus(object sender, RoutedEventArgs e)
		{
			if (sender == null || e == null)
			{
				return;
			}

			TextBox? textBox = sender as TextBox;

			if (textBox == null)
			{
				return;
			}

			if (string.IsNullOrEmpty(textBox.Text) || textBox.Text.Equals("Type game name here...", StringComparison.OrdinalIgnoreCase))
			{
				textBox.Text = string.Empty;
				textBox.Opacity = 1;
			}
		}

		private void gameNameTextBox_LostFocus(object sender, RoutedEventArgs e)
		{
			if (sender == null || e == null)
			{
				return;
			}

			TextBox? textBox = sender as TextBox;

			if (textBox == null)
			{
				return;
			}

			if (string.IsNullOrEmpty(textBox.Text))
			{
				textBox.Text = "Type game name here...";
				textBox.Opacity = 0.5;
			}
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			gameListBox.ItemsSource = Core.GameCollection;
			Core.InBackgroundThread(() => Core.Init());
		}

		private void gameNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			string text = gameNameTextBox.Text;

			Core.SearchToken.Cancel();
			Core.SearchToken = new System.Threading.CancellationTokenSource(TimeSpan.FromDays(1));

			if ("Enter Game name here...".Contains(text, StringComparison.InvariantCultureIgnoreCase))
			{
				Core.LastTextEnteredTime = DateTime.Now;
				Core.InBackgroundThread(() => Core.LoadDefaults());
				return;
			}

			if (Core.StartProcessing && !string.IsNullOrEmpty(text) &&
				(DateTime.Now - Core.LastTextEnteredTime).Milliseconds > 200)
			{
				Core.InBackgroundThread(() => Core.ProcessSearch(text, DateTime.Now));
			}

			Core.LastTextEnteredTime = DateTime.Now;
		}
	}
}
