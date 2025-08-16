// PointsVisual.cs
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ConcaveHullwNTS
{
	/// <summary>
	/// Пользовательский элемент управления для эффективной отрисовки большого количества точек.
	/// Использует DrawingVisual для минимизации количества элементов в визуальном дереве WPF.
	/// </summary>
	public class PointsVisual : FrameworkElement
	{
		// Статическая общая кисть для всех точек, чтобы не создавать тысячи экземпляров
		private static readonly Brush PointBrush = Brushes.Blue;
		private readonly DrawingVisual _visual;
		private readonly List<Point> _points; // Используем Point для координат Canvas

		public PointsVisual()
		{
			_visual = new DrawingVisual();
			_points = [];
			// Добавляем наш DrawingVisual в визуальное дерево этого элемента
			this.AddVisualChild(_visual);
			this.AddLogicalChild(_visual);
		}


		// Необходимо переопределить эти свойство и метод для корректной работы FrameworkElement с DrawingVisual
		// Не в коде прямой ссылки на них, но это потому что они вызываются внутренними механизмами WPF во время процесса отрисовки
		protected override int VisualChildrenCount => 1;
		protected override Visual GetVisualChild(int index)
		{
			ArgumentOutOfRangeException.ThrowIfNotEqual(index, 0);
			return _visual;
		}

		/// <summary>
		/// Устанавливает точки для отрисовки. Координаты должны быть уже преобразованы в систему координат Canvas.
		/// </summary>
		/// <param name="points">Список точек в координатах Canvas.</param>
		public void SetPoints(IEnumerable<Point> points)
		{
			_points.Clear();
			if (points != null)
			{
				_points.AddRange(points);
			}
			RenderPoints();
		}

		/// <summary>
		/// Выполняет фактическую отрисовку точек на DrawingVisual.
		/// </summary>
		private void RenderPoints()
		{
			// Используем using для корректного освобождения DrawingContext
			using DrawingContext dc = _visual.RenderOpen();
			// Определяем размер точки. Радиус 1 пиксель -> диаметр 2 пикселя
			const double pointHalfSize = 1.0;
			const double pointSize = pointHalfSize * 2;

			// Рисуем каждую точку как прямоугольник (обычно быстрее, чем эллипс)
			foreach (var point in _points)
			{
				// Rect для DrawRectangle: верхний левый угол (x - размер/2, y - размер/2)
				dc.DrawRectangle(PointBrush, null, // Brush, Pen (null = без контура)
					new Rect(point.X - pointHalfSize, point.Y - pointHalfSize, pointSize, pointSize));
			}
		}
	}
}