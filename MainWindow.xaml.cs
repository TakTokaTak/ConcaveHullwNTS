using NetTopologySuite.Geometries;
using System.Windows;

namespace ConcaveHullwNTS
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			// Null-forgiving operator (!) используется, так как мы уверены, что элемент существует в XAML
			InputSettingsTabInstance!.StatusUpdated += OnStatusUpdated;
			InputSettingsTabInstance!.InfoUpdated += OnInfoUpdated;
			InputSettingsTabInstance!.HullCalculated += OnHullCalculated;
		}

		#region Обработчики событий от InputSettingsTab

		private void OnStatusUpdated(string message)
		{
			// Используем null-forgiving operator (!), так как StatusBarTextBlock определен в XAML
			StatusBarTextBlock!.Text = message;
		}

		private void OnInfoUpdated(string message)
		{
			// Обработка в InputSettingsTab
		}

		private void OnHullCalculated(Geometry hullGeometry, Coordinate[] originalPoints)
		{
			// Логика, которая должна выполниться, когда оболочка вычислена
			// Например, автоматически переключить вкладку на "Визуализация"
			// и передать данные в элемент управления визуализацией (который мы создадим позже)

			// Пока просто покажем сообщение
			// MessageBox.Show("Оболочка вычислена! Переключитесь на вкладку 'Визуализация'.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);

			// TODO: Здесь будет логика для передачи данных в VisualizationTab
			// Например, если у VisualizationTab будет метод SetData(Geometry hull, Coordinate[] points)
			// var visualizationTab = MainTabControl.Items[1] as VisualizationTab; // Предполагая, что это UserControl
			// visualizationTab?.SetData(hullGeometry, originalPoints);
		}

		#endregion

		#region Логика MainWindow (если потребуется)

		// Дополнительные методы и логика MainWindow могут быть добавлены здесь

		#endregion
	}
}