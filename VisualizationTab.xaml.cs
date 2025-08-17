// VisualizationTab.xaml.cs
// --- Добавлены using alias ---
using System.Windows; // Для Point, RoutedEventArgs и т.д.
using System.Windows.Controls; // Для Canvas, UserControl и т.д.
using System.Windows.Media; // Для Brush, Pen, Path, PathGeometry и т.д.
using System.Windows.Shapes; // Для Line (если потребуется в будущем)
using System; // Для основных типов
using System.Collections.Generic; // Для List<>
using System.Linq; // Для Linq-запросов
				   // --- Псевдонимы для типов NTS ---
using NtsCoordinate = NetTopologySuite.Geometries.Coordinate;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;
using NtsPoint = NetTopologySuite.Geometries.Point;
using NtsGeometryFactory = NetTopologySuite.Geometries.GeometryFactory;
using NtsLinearRing = NetTopologySuite.Geometries.LinearRing;
// --- Для контекстного меню ---
using System.Windows.Controls.Primitives; // Для ContextMenu
										  // -----------------------------

namespace ConcaveHullwNTS
{
	public partial class VisualizationTab : UserControl
	{
		private NtsCoordinate[]? _originalPoints;
		private NtsGeometry? _hullGeometry;
		private Rect _dataBounds = new(0, 0, 100, 100);
		public event Action? SaveRequested;
		private bool _isVisualizationDirty = true;
		private PointsVisual? _pointsVisual;

		// --- Добавлено: Поле для нового интерактивного полигона ---
		private InteractivePolygonVisual? _polygonVisual;
		// --- Добавлено: Поле для хранения контекстного меню ---
		private ContextMenu? _segmentContextMenu;
		// --- Добавлено: Поле для хранения индекса выделенной вершины ---
		private int _highlightedVertexIndex = -1;
		// -------------------------------------------------------------

		public VisualizationTab()
		{
			InitializeComponent();
			Loaded += OnLoaded;
			MainCanvas.SizeChanged += OnCanvasSizeChanged;

			// --- Добавлено: Инициализация InteractivePolygonVisual ---
			_polygonVisual = new InteractivePolygonVisual();
			// Подписываемся на его события
			_polygonVisual.SegmentsHighlighted += OnPolygonSegmentsHighlighted;
			_polygonVisual.SegmentRightClicked += OnPolygonSegmentRightClicked;
			// Добавляем его на Canvas (позади точек, если они будут добавлены позже)
			// Порядок добавления в Children определяет z-порядок (последний добавленный сверху)
			// Добавим его первым, чтобы точки (PointsVisual) были сверху
			MainCanvas.Children.Add(_polygonVisual);
			// -----------------------------------------------------------

			// --- Добавлено: Создание и настройка ContextMenu ---
			_segmentContextMenu = new ContextMenu();
			MenuItem removeSegmentItem = new MenuItem { Header = "Удалить сегмент" };
			removeSegmentItem.Click += RemoveSegmentMenuItem_Click;
			_segmentContextMenu.Items.Add(removeSegmentItem);
			// -----------------------------------------------------
		}

		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			UpdateVisualization();
		}

		private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (e.PreviousSize != e.NewSize)
			{
				_isVisualizationDirty = true;
				UpdateVisualization();
			}
		}

		public void SetData(NtsGeometry? hullGeometry, NtsCoordinate[]? originalPoints)
		{
			bool pointsChanged = !ReferenceEquals(_originalPoints, originalPoints);
			_hullGeometry = hullGeometry;
			_originalPoints = originalPoints;
			_isVisualizationDirty = true;
			UpdateVisualization();
		}

		private void UpdateVisualization()
		{
			if (!_isVisualizationDirty)
			{
				return;
			}
			if (MainCanvas.ActualWidth <= 1 || MainCanvas.ActualHeight <= 1)
			{
				return;
			}

			ButtonState();

			// --- Изменено: Не удаляем Path напрямую, это делает _polygonVisual ---
			// Вместо этого, мы очищаем данные у _polygonVisual, если данных нет

			if (!HasData())
			{
				// Очищаем полигон, если нет данных ---
				_polygonVisual?.SetPolygonData(null);
				// Сбрасываем индекс выделенной вершины ---
				_highlightedVertexIndex = -1;
				_isVisualizationDirty = false;
				return;
			}

			CalculateDataBounds();
			DrawAllElements();
			_isVisualizationDirty = false;
		}

		private void CalculateDataBounds()
		{
			if (!HasData()) return;

			double minX = double.MaxValue;
			double minY = double.MaxValue;
			double maxX = double.MinValue;
			double maxY = double.MinValue;

			foreach (var p in _originalPoints ?? Enumerable.Empty<NtsCoordinate>())
			{
				minX = Math.Min(minX, p.X);
				minY = Math.Min(minY, p.Y);
				maxX = Math.Max(maxX, p.X);
				maxY = Math.Max(maxY, p.Y);
			}

			if (_hullGeometry is NtsPolygon polygon)
			{
				foreach (var c in polygon.Shell?.Coordinates ?? Enumerable.Empty<NtsCoordinate>())
				{
					minX = Math.Min(minX, c.X);
					minY = Math.Min(minY, c.Y);
					maxX = Math.Max(maxX, c.X);
					maxY = Math.Max(maxY, c.Y);
				}
			}

			if (minX == maxX) maxX = minX + 1.0;
			if (minY == maxY) maxY = minY + 1.0;

			double marginX = (maxX - minX) * 0.1;
			double marginY = (maxY - minY) * 0.1;

			_dataBounds = new Rect(
				minX - marginX,
				minY - marginY,
				(maxX - minX) + 2 * marginX,
				(maxY - minY) + 2 * marginY);
		}

		private bool HasData()
		{
			return (_originalPoints != null && _originalPoints.Length > 0) ||
				   (_hullGeometry != null);
		}

		private void ButtonState()
		{
			SaveResultButton_VisualizationTab.IsEnabled = _hullGeometry != null;
		}

		private void DrawAllElements()
		{
			// правление отрисовкой точек ---
			if (_pointsVisual == null)
			{
				_pointsVisual = new PointsVisual();
				// Добавляем PointsVisual ПОВЕРХ _polygonVisual
				MainCanvas.Children.Add(_pointsVisual);
			}

			List<System.Windows.Point> canvasPoints = [];
			if (_originalPoints != null)
			{
				foreach (NtsCoordinate point in _originalPoints)
				{
					canvasPoints.Add(WorldToCanvas(point));
				}
			}
			_pointsVisual.SetPoints(canvasPoints);


			// Управление отрисовкой полигона ---
			// Вместо вызова DrawPolygon, который создавал Path, 
			// мы передаем данные в InteractivePolygonVisual
			if (_hullGeometry is NtsPolygon polygon)
			{
				// Получаем координаты Shell
				NtsCoordinate[]? shellCoordinates = polygon.Shell?.Coordinates;

				if (shellCoordinates != null && shellCoordinates.Length >= 3)
				{
					// Преобразуем координаты NTS в координаты Canvas
					// InteractivePolygonVisual ожидает, что координаты уже в системе Canvas
					NtsCoordinate[] canvasCoordinates = new NtsCoordinate[shellCoordinates.Length];
					for (int i = 0; i < shellCoordinates.Length; i++)
					{
						var wpfPoint = WorldToCanvas(shellCoordinates[i]);
						// Создаем новый NtsCoordinate с координатами Canvas
						// Это немного необычно, но позволяет InteractivePolygonVisual
						// работать с уже преобразованными точками.
						canvasCoordinates[i] = new NtsCoordinate(wpfPoint.X, wpfPoint.Y);
					}

					// Передаем преобразованные координаты в InteractivePolygonVisual
					_polygonVisual?.SetPolygonData(canvasCoordinates);
				}
				else
				{
					// Если полигон некорректен, очищаем визуализацию полигона
					_polygonVisual?.SetPolygonData(null);
				}
			}
			else
			{
				// Если _hullGeometry не NtsPolygon или null, очищаем визуализацию полигона
				_polygonVisual?.SetPolygonData(null);
			}
		}

		private System.Windows.Point WorldToCanvas(NtsCoordinate worldPoint)
		{
			if (_dataBounds.IsEmpty)
				return new System.Windows.Point(10, 10);

			double canvasWidth = MainCanvas.ActualWidth;
			double canvasHeight = MainCanvas.ActualHeight;

			if (canvasWidth <= 0) canvasWidth = 100;
			if (canvasHeight <= 0) canvasHeight = 100;

			// --- Исправлена потенциальная ошибка деления на ноль ---
			// Если _dataBounds.Width или Height равны 0, масштабирование невозможно.
			// Логика защиты от одинаковых точек в CalculateDataBounds должна это предотвратить,
			// но добавим дополнительную проверку на всякий случай.
			if (_dataBounds.Width <= 0 || _dataBounds.Height <= 0)
			{
				//System.Diagnostics.Debug.WriteLine("WARNING: _dataBounds has zero width or height!");
				return new System.Windows.Point(canvasWidth / 2, canvasHeight / 2); // Центр Canvas
			}
			// --------------------------------------------------------

			double x_ratio = (worldPoint.X - _dataBounds.Left) / _dataBounds.Width;
			double y_ratio = (worldPoint.Y - _dataBounds.Top) / _dataBounds.Height;

			double x = x_ratio * canvasWidth;
			double y = canvasHeight - (y_ratio * canvasHeight); // Инверсия Y

			// --- Убрано: Логирование предупреждений о выходе за границы ---
			// Это может происходить при нормальном масштабировании и не является ошибкой.
			// if (x < 0 || x > canvasWidth || y < 0 || y > canvasHeight)
			// {
			// 	System.Diagnostics.Debug.WriteLine($"WARNING: Point ({worldPoint.X}, {worldPoint.Y}) maps to ({x}, {y}) outside canvas {canvasWidth}x{canvasHeight}");
			// }
			// ---------------------------------------------------------------

			//System.Diagnostics.Debug.WriteLine($"Mapped ({worldPoint.X}, {worldPoint.Y}) -> ({x}, {y})");
			return new System.Windows.Point(x, y);
		}

		private void SaveResultButton_Click(object sender, RoutedEventArgs e)
		{
			SaveRequested?.Invoke();
		}

		// --- Добавлено: Обработчики событий от InteractivePolygonVisual ---
		private void OnPolygonSegmentsHighlighted(int vertexIndex)
		{
			// Сохраняем индекс выделенной вершины для последующего использования
			_highlightedVertexIndex = vertexIndex;
			// Здесь можно добавить логику обновления UI, если это потребуется
			// Например, активировать кнопки редактирования, если они будут
			// System.Diagnostics.Debug.WriteLine($"Polygon segments highlighted at vertex index: {vertexIndex}");
		}

		private void OnPolygonSegmentRightClicked(System.Windows.Point position, int vertexIndex)
		{
			_highlightedVertexIndex = vertexIndex;
			// Убедитесь, что _segmentContextMenu не назначен как ContextMenu какому-либо элементу в XAML
			// и не открывается другим способом одновременно.
			if (_segmentContextMenu != null)
			{
				// WPF автоматически использует позицию мыши из MouseButtonEventArgs
				_segmentContextMenu.Placement = PlacementMode.MousePoint;
				_segmentContextMenu.IsOpen = true;
			}
		}

		// --- Добавлено: Обработчик клика по пункту меню "Удалить сегмент" ---
		private void RemoveSegmentMenuItem_Click(object sender, RoutedEventArgs e)
		{
			// Проверяем, что у нас есть выделенная вершина и исходная геометрия оболочки
			if (_highlightedVertexIndex != -1 && _hullGeometry is NtsPolygon polygon && _originalPoints != null)
			{
				//System.Diagnostics.Debug.WriteLine($"Attempting to remove vertex at index: {_highlightedVertexIndex}");
				RemoveVertexFromHull(_highlightedVertexIndex);
			}
			else
			{
				//System.Diagnostics.Debug.WriteLine("Cannot remove segment: No vertex highlighted or no hull geometry.");
				MessageBox.Show("Невозможно удалить сегмент: нет выделенной вершины или данных оболочки.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
			}

			// Сбрасываем индекс после попытки удаления
			_highlightedVertexIndex = -1;
		}
		// -------------------------------------------------------------------

		// --- Добавлено: Логика удаления вершины из полигона ---
		/// <summary>
		/// Удаляет вершину из полигона оболочки и обновляет визуализацию.
		/// </summary>
		/// <param name="vertexIndexToRemove">Индекс вершины для удаления.</param>
		private void RemoveVertexFromHull(int vertexIndexToRemove)
		{
			// 1. Проверка входных данных
			if (_hullGeometry is not NtsPolygon polygon || _originalPoints == null)
			{
				//System.Diagnostics.Debug.WriteLine("RemoveVertexFromHull: Invalid state - no hull or points.");
				return;
			}

			NtsCoordinate[]? shellCoords = polygon.Shell?.Coordinates;
			if (shellCoords == null || shellCoords.Length <= 3) // Минимум 3 точки для полигона
			{
				//System.Diagnostics.Debug.WriteLine("RemoveVertexFromHull: Not enough coordinates to remove a vertex.");
				MessageBox.Show("Невозможно удалить вершину: в оболочке недостаточно вершин.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (vertexIndexToRemove < 0 || vertexIndexToRemove >= shellCoords.Length)
			{
				//System.Diagnostics.Debug.WriteLine($"RemoveVertexFromHull: Vertex index {vertexIndexToRemove} is out of range [0, {shellCoords.Length - 1}].");
				MessageBox.Show($"Индекс вершины {vertexIndexToRemove} выходит за допустимый диапазон.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			// 2. Создание нового массива координат без указанной вершины
			// Важно: NTS Polygon ожидает, что первая и последняя точки совпадают (замкнутый полигон).
			// Нам нужно удалить одну точку из последовательности и сохранить замкнутость.

			// Получаем общее количество точек в исходном массиве (включая дублирующуюся первую/последнюю)
			int originalLength = shellCoords.Length;

			// Создаем новый список координат
			List<NtsCoordinate> newShellCoordsList = new List<NtsCoordinate>(originalLength - 1);

			// Сценарий зависит от того, какая вершина удаляется:
			// - Если удаляется первая (индекс 0) или последняя (индекс Length-1) точка:
			//   Это одна и та же точка в замкнутом полигоне. Мы должны удалить одну копию
			//   и убедиться, что новая первая и последняя точки совпадают.
			// - Если удаляется промежуточная точка (индекс i, где 0 < i < Length-1):
			//   Просто копируем все точки, кроме нее.

			if (vertexIndexToRemove == 0 || vertexIndexToRemove == originalLength - 1)
			{
				// Удаляем одну из точек (например, первую), копируем остальные, исключая последнюю
				// и добавляем новую "первую" точку (которая была второй в оригинале) в конец как замыкающую.
				// Но проще: копируем с индекса 1 до Length-2 (все, кроме первой и последней, которые совпадают)
				// и добавляем точку с индексом 1 как новую первую и последнюю.
				// Еще проще: копируем с 1 по Length-2, затем добавляем точку[1] в конец.
				// Или: копируем с 1 по Length-1 (исключая 0), и заменяем последнюю точку на точку[1].
				// Самый простой и понятный способ:
				// 1. Добавить все точки от 1 до Length-2 (исключая 0 и Length-1)
				for (int i = 1; i < originalLength - 1; i++) // i = 1 to N-2
				{
					newShellCoordsList.Add(shellCoords[i]);
				}
				// 2. Добавить точку[1] в конец, чтобы замкнуть полигон новой первой точкой
				//    (т.е. новая первая точка будет shellCoords[1])
				newShellCoordsList.Add(shellCoords[1]); // Замыкаем полигон
			}
			else
			{
				// Удаляем промежуточную вершину
				for (int i = 0; i < originalLength; i++)
				{
					if (i != vertexIndexToRemove)
					{
						newShellCoordsList.Add(shellCoords[i]);
					}
				}
			}

			// 3. Создание нового полигона на основе новых координат
			if (newShellCoordsList.Count < 3)
			{
				//System.Diagnostics.Debug.WriteLine("RemoveVertexFromHull: After removal, less than 3 vertices remain.");
				MessageBox.Show("После удаления останется менее 3 вершин. Операция отменена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			try
			{
				NtsCoordinate[] newShellCoords = newShellCoordsList.ToArray();
				// Создаем новый внешний контур (Shell)
				NtsLinearRing newShell = new NtsLinearRing(newShellCoords);
				// Создаем новый полигон. Предполагаем, что дыр (Holes) нет.
				NtsPolygon newPolygon = new NtsPolygon(newShell, null, NtsGeometryFactory.Default);

				// 4. Обновление визуализации с новым полигоном
				// Вызываем SetData с новым полигоном и теми же исходными точками
				// Это приведет к повторному вызову UpdateVisualization -> DrawAllElements
				SetData(newPolygon, _originalPoints);

				//System.Diagnostics.Debug.WriteLine($"Successfully removed vertex at index {vertexIndexToRemove}. New polygon has {newShellCoords.Length - 1} unique vertices.");
			}
			catch (Exception ex)
			{
				//System.Diagnostics.Debug.WriteLine($"RemoveVertexFromHull: Exception during polygon creation/removal: {ex.Message}");
				MessageBox.Show($"Ошибка при удалении вершины: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
		// ---------------------------------------------------------
	}
}