using NetTopologySuite.Geometries;
// Псевдонимы для типов NTS
using NtsCoordinate = NetTopologySuite.Geometries.Coordinate;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;
using NtsPoint = NetTopologySuite.Geometries.Point;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ConcaveHullwNTS
{
	public partial class VisualizationTab : UserControl
	{
		private NtsCoordinate[]? _originalPoints;
		private NtsGeometry? _hullGeometry;
		private Rect _dataBounds = new(0, 0, 100, 100);
		public event Action? SaveRequested;
		private bool _isVisualizationDirty = true; // true означает, что данные изменились или размер изменился, и визуализацию нужно обновить. Изначально true, чтобы гарантировать первую отрисовку
		private PointsVisual? _pointsVisual;
		private NtsCoordinate[]? _currentlyDrawnPoints;

		public VisualizationTab()
		{
			InitializeComponent();
			Loaded += OnLoaded;
			MainCanvas.SizeChanged += OnCanvasSizeChanged;
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
				UpdateVisualization(); // Попытка обновить
			}
		}

		public void SetData(NtsGeometry? hullGeometry, NtsCoordinate[]? originalPoints)
		{
			// Определяем, изменились ли точки
			bool pointsChanged = !ReferenceEquals(_originalPoints, originalPoints);
			// Более строгая (но потенциально медленная) проверка:
			// bool pointsChanged = !((_originalPoints == null && originalPoints == null) ||
			// (_originalPoints != null && originalPoints != null && _originalPoints.SequenceEqual(originalPoints)));
			// Обновляем внутренние данные
			_hullGeometry = hullGeometry;
			_originalPoints = originalPoints; // Сохраняем ссылку на новые точки

			// При изменении данных визуализация становится "грязной"
			_isVisualizationDirty = true;

			// Если точки изменились, обновляем ссылку на "текущие отрисованные"
			// Это нужно для корректной работы логики в UpdateVisualization/DrawAllElements
			if (pointsChanged)
			{
				_currentlyDrawnPoints = _originalPoints; // Может быть null
			}
			// Если точки не изменились, _currentlyDrawnPoints остается прежним
			// Это сигнализирует DrawAllElements, что перерисовывать точки не нужно

			UpdateVisualization(); // Попытка обновить, UpdateVisualization проверит _isVisualizationDirty
		}

		private void UpdateVisualization()
		{
			if (!_isVisualizationDirty)
			{
				return;
			}
			if (MainCanvas.ActualWidth <= 1 || MainCanvas.ActualHeight <= 1) // Используем <= 1 для надежности
			{
				return;
			}
			ButtonState(); // Проверяется, надо ли активировать кнопку сохранения
			// удаляем только старый полигон (по тегу)
			Path? oldPath = MainCanvas.Children.OfType<Path>().FirstOrDefault(p => p.Tag as string == "HullPolygon");
			if (oldPath != null)
			{
				MainCanvas.Children.Remove(oldPath);
			}
			if (!HasData())
			{
				_currentlyDrawnPoints = null;
				_isVisualizationDirty = false; 
				return;
			}
			// Рассчитываем границы только если есть данные
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

			// Защита от одинаковых точек
			if (minX == maxX) maxX = minX + 1.0;
			if (minY == maxY) maxY = minY + 1.0;

			// Добавляем отступы 10%
			double marginX = (maxX - minX) * 0.1;
			double marginY = (maxY - minY) * 0.1;

			_dataBounds = new Rect(
				minX - marginX,
				minY - marginY,
				(maxX - minX) + 2 * marginX,
				(maxY - minY) + 2 * marginY);
			//System.Diagnostics.Debug.WriteLine($"--- CalculateDataBounds ---");
			//System.Diagnostics.Debug.WriteLine($"Data Min: ({minX}, {minY}), Max: ({maxX}, {maxY})");
			//System.Diagnostics.Debug.WriteLine($"Margins: X={marginX}, Y={marginY}");
			//System.Diagnostics.Debug.WriteLine($"_dataBounds: {_dataBounds}");

		}

		private bool HasData()
		{
			
			return (_originalPoints != null && _originalPoints.Length > 0) ||
				   (_hullGeometry != null);
		}

		/// <summary>
		/// Метод, чтобы активировать или нет кнопку
		/// </summary>
		private void ButtonState()
		{
			SaveResultButton_VisualizationTab.IsEnabled = _hullGeometry != null;
		}

		private void DrawAllElements()
		{
			// Всегда пересчитываем и обновляем точки, если они есть.
			// Это необходимо при изменении размера Canvas.
			// Если _originalPoints == null, мы просто передадим пустой список.
			if (_pointsVisual == null)
			{
				_pointsVisual = new PointsVisual();
				MainCanvas.Children.Add(_pointsVisual);
			}

			// Подготавливаем список точек в координатах Canvas для отрисовки
			List<System.Windows.Point> canvasPoints = [];
			if (_originalPoints != null)
			{
				foreach (NtsCoordinate point in _originalPoints)
				{
					canvasPoints.Add(WorldToCanvas(point));
				}
			}
			// Передаем подготовленные точки в PointsVisual для отрисовки
			// Если _originalPoints было null, передается пустой список, что корректно.
			_pointsVisual.SetPoints(canvasPoints);

			// Обновляем ссылку на "текущие отрисованные" точки.
			// Это позволяет оптимизации в SetData определить, что точки не менялись 
			// при следующем вызове SetData с теми же точками.
			_currentlyDrawnPoints = _originalPoints;

			// Рисуем новый полигон, если есть данные
			if (_hullGeometry is NtsPolygon polygon)
			{
				DrawPolygon(polygon);
			}
		}

		private void DrawPolygon(NtsPolygon polygon)
		{
			var shell = polygon.Shell;
			if (shell?.Coordinates == null || shell.Coordinates.Length < 3)
				return;

			PathGeometry geometry = new();
			PathFigure figure = new()
			{
				IsClosed = true,
				StartPoint = WorldToCanvas(shell.Coordinates[0])
			};

			for (int i = 1; i < shell.Coordinates.Length; i++)
			{
				figure.Segments.Add(new System.Windows.Media.LineSegment(WorldToCanvas(shell.Coordinates[i]), true));
			}

			geometry.Figures.Add(figure);

			Path path = new()
			{
				Stroke = Brushes.Red,
				StrokeThickness = 2,
				Data = geometry,
				Tag = "HullPolygon",
			};

			MainCanvas.Children.Add(path);
		}

		private System.Windows.Point WorldToCanvas(NtsCoordinate worldPoint)
		{
			if (_dataBounds.IsEmpty)
				return new System.Windows.Point(10, 10);

			double canvasWidth = MainCanvas.ActualWidth;
			double canvasHeight = MainCanvas.ActualHeight;

			// Если размеры полотна неизвестны, используем минимальные значения
			if (canvasWidth <= 0) canvasWidth = 100;
			if (canvasHeight <= 0) canvasHeight = 100;

			double x_ratio = (worldPoint.X - _dataBounds.Left) / _dataBounds.Width;
			double y_ratio = (worldPoint.Y - _dataBounds.Top) / _dataBounds.Height;

			double x = x_ratio * canvasWidth;
			double y = canvasHeight - (y_ratio * canvasHeight);

			if (x < 0 || x > canvasWidth || y < 0 || y > canvasHeight)
			{
				System.Diagnostics.Debug.WriteLine($"WARNING: Point ({worldPoint.X}, {worldPoint.Y}) maps to ({x}, {y}) outside canvas {canvasWidth}x{canvasHeight}");
			}
			//System.Diagnostics.Debug.WriteLine($"Mapped ({worldPoint.X}, {worldPoint.Y}) -> ({x}, {y})");

			return new System.Windows.Point(x, y);
		}

		private void SaveResultButton_Click(object sender, RoutedEventArgs e)
		{
			SaveRequested?.Invoke();
		}
	}
}