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
		private Rect _dataBounds = new Rect(0, 0, 100, 100);

		public event Action? SaveRequested;

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
			UpdateVisualization();
		}

		public void SetData(NtsGeometry? hullGeometry, NtsCoordinate[]? originalPoints)
		{
			_hullGeometry = hullGeometry;
			_originalPoints = originalPoints;
			UpdateVisualization();
		}

		private void UpdateVisualization()
		{
			MainCanvas.Children.Clear();

			if (!HasData())
			{
				// Тестовый элемент при отсутствии данных
				var rect = new Rectangle
				{
					Width = 50,
					Height = 50,
					Fill = Brushes.LightGray,
					Stroke = Brushes.DarkGray
				};
				Canvas.SetLeft(rect, 10);
				Canvas.SetTop(rect, 10);
				MainCanvas.Children.Add(rect);
				return;
			}

			CalculateDataBounds();
			DrawAllElements();
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
		}

		private bool HasData()
		{
			return (_originalPoints != null && _originalPoints.Length > 0) ||
				   (_hullGeometry != null);
		}

		private void DrawAllElements()
		{
			// Сначала рисуем оболочку
			if (_hullGeometry is NtsPolygon polygon)
			{
				DrawPolygon(polygon);
			}

			// Затем точки поверх оболочки
			if (_originalPoints != null)
			{
				foreach (var point in _originalPoints)
				{
					DrawPoint(point);
				}
			}
		}

		private void DrawPoint(NtsCoordinate point)
		{
			// Явное указание System.Windows.Point
			System.Windows.Point canvasPoint = WorldToCanvas(point);

			Ellipse ellipse = new Ellipse
			{
				Width = 6,
				Height = 6,
				Fill = Brushes.Blue,
				ToolTip = $"X: {point.X:F2}\nY: {point.Y:F2}"
			};

			Canvas.SetLeft(ellipse, canvasPoint.X - 3);
			Canvas.SetTop(ellipse, canvasPoint.Y - 3);
			MainCanvas.Children.Add(ellipse);
		}

		private void DrawPolygon(NtsPolygon polygon)
		{
			var shell = polygon.Shell;
			if (shell?.Coordinates == null || shell.Coordinates.Length < 3)
				return;

			PathGeometry geometry = new PathGeometry();
			// Явное указание System.Windows.Point
			PathFigure figure = new PathFigure
			{
				IsClosed = true,
				StartPoint = WorldToCanvas(shell.Coordinates[0])
			};

			for (int i = 1; i < shell.Coordinates.Length; i++)
			{
				// Явное указание System.Windows.Media.LineSegment
				figure.Segments.Add(new System.Windows.Media.LineSegment(WorldToCanvas(shell.Coordinates[i]), true));
			}

			geometry.Figures.Add(figure);

			Path path = new Path
			{
				Stroke = Brushes.Red,
				StrokeThickness = 2,
				Data = geometry
			};

			MainCanvas.Children.Add(path);
		}

		// Явное указание System.Windows.Point
		private System.Windows.Point WorldToCanvas(NtsCoordinate worldPoint)
		{
			if (_dataBounds.IsEmpty)
				return new System.Windows.Point(10, 10);

			double canvasWidth = MainCanvas.ActualWidth;
			double canvasHeight = MainCanvas.ActualHeight;

			// Если размеры канваса неизвестны, используем минимальные значения
			if (canvasWidth <= 0) canvasWidth = 100;
			if (canvasHeight <= 0) canvasHeight = 100;

			double x = (worldPoint.X - _dataBounds.Left) / _dataBounds.Width * canvasWidth;
			double y = canvasHeight - (worldPoint.Y - _dataBounds.Top) / _dataBounds.Height * canvasHeight;

			return new System.Windows.Point(x, y);
		}

		private void SaveResultButton_Click(object sender, RoutedEventArgs e)
		{
			SaveRequested?.Invoke();
		}
	}
}