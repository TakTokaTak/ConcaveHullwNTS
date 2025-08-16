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
			if (InputSettingsTabInstance != null)
			{
				InputSettingsTabInstance.StatusUpdated += OnStatusUpdated;
				InputSettingsTabInstance.HullCalculated += OnHullCalculated;
				InputSettingsTabInstance.PointsLoaded += OnPointsLoaded;
			}

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

		private void OnPointsLoaded(Coordinate[] points)
		{
			// Вызывается после успешной загрузки точек в InputSettingsTab
			// Передаем точки в VisualizationTab, оболочку устанавливаем в null
			// Это приведет к отрисовке только точек (очистке оболочки, если она была)
			VisualizationTabInstance?.SetData(null, points);
		}

		#endregion

		#region Обработчики событий от VisualizationTab

		private void OnVisualizationSaveRequested()
		{
			// Когда пользователь нажимает "Сохранить результат" на вкладке визуализации,
			// мы вызываем метод сохранения из InputSettingsTab
			InputSettingsTabInstance?.TriggerSave(); // TriggerSave() расположен в InputSettingsTab.xaml.cs
		}

		#endregion

		#region Логика MainWindow (если потребуется)

		#endregion
	}
}