using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

namespace ConcaveHullwNTS
{
	/// <summary>
	/// Логика взаимодействия для AboutTab.xaml
	/// </summary>
	public partial class AboutTab : UserControl
	{
		public AboutTab()
		{
			InitializeComponent();
			Assembly assembly = Assembly.GetExecutingAssembly();

			// Проверяем по порядку приоритета
			string version = assembly.GetName().Version?.ToString() ??
							 assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ??
							 "Неизвестна";
			txtVersion.Text = version;
		}
	}
}
