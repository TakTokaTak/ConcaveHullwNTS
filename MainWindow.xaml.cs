using NetTopologySuite.Geometries;
using System.Diagnostics;
using System.Text;
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
			Debug.AutoFlush = true;
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); //Надо добавлять перед инициализацией
			InitializeComponent();

			//// -- Тестовые данные -- 
			//Loaded += (s, e) =>
			//{
			//	Coordinate[] points = new[]
			//	{
			//		new Coordinate(0, 0),
			//		new Coordinate(50, 0),
			//		new Coordinate(25, 25),
			//		new Coordinate(50, 50),
			//		new Coordinate(0, 50),
			//		new Coordinate(0, 0)
			//	};
			//	GeometryFactory factory = new GeometryFactory();
			//	Polygon hull = factory.CreatePolygon(points);

			//	VisualizationTabInstance.SetData(hull, points);
			//};
			//// ---- 

			// Null-forgiving operator (!) используется, так как мы уверены, что элемент существует в XAML
			if (InputSettingsTabInstance != null)
			{
				InputSettingsTabInstance.StatusUpdated += OnStatusUpdated;
				// Подписываемся на HullCalculated
				InputSettingsTabInstance.HullCalculated += OnHullCalculated;
			}

			// Подписываемся на SaveRequested от VisualizationTab
			if (VisualizationTabInstance != null)
			{
				VisualizationTabInstance.SaveRequested += OnVisualizationSaveRequested;
			}
		}

		#region Обработчики событий от InputSettingsTab

		private void OnStatusUpdated(string message)
		{
			// Используем null-forgiving operator (!), так как StatusBarTextBlock определен в XAML
			StatusBarTextBlock!.Text = message;
		}


		private void OnHullCalculated(Geometry hullGeometry, Coordinate[] originalPoints)
		{
			VisualizationTabInstance.SetData(hullGeometry, originalPoints);
		}

		#endregion

		#region Обработчики событий от VisualizationTab

		private void OnVisualizationSaveRequested()
		{
			// Когда пользователь нажимает "Сохранить результат" на вкладке визуализации,
			// мы вызываем метод сохранения из InputSettingsTab
			if (InputSettingsTabInstance != null)
			{
				InputSettingsTabInstance.TriggerSave(); // TriggerSave() расположен в InputSettingsTab.xaml.cs
			}
		}

		#endregion

		#region Логика MainWindow (если потребуется)

		// Дополнительные методы и логика MainWindow

		#endregion
	}
}