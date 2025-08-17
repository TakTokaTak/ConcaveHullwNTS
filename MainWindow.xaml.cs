using NetTopologySuite.Geometries;
using System.Diagnostics;
using System.Text;
using System.Windows;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

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
			InputSettingsTabInstance!.StatusUpdated += OnStatusUpdated;
			InputSettingsTabInstance!.HullCalculated += OnHullCalculated;
			InputSettingsTabInstance!.PointsLoaded += OnPointsLoaded;
			VisualizationTabInstance!.SaveRequested += OnVisualizationSaveRequested;
			VisualizationTabInstance!.HullGeometryModified += OnVisualizationHullModified;
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
		private void OnVisualizationHullModified(NtsGeometry? newHullGeometry)
		{
			var inputTab = InputSettingsTabInstance;

			if (inputTab != null)
			{
				// --- Используем новый публичный метод ---
				inputTab.UpdateCurrentHullResult(newHullGeometry);
				// ---------------------------------------
			}
			else
			{
				//System.Diagnostics.Debug.WriteLine("OnVisualizationHullModified: InputSettingsTabInstance is null.");
			}
		}
		#endregion

		#region Логика MainWindow (если потребуется)

		#endregion
	}
}