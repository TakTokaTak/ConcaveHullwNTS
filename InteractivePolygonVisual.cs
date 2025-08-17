// InteractivePolygonVisual.cs
using NetTopologySuite.Geometries; // Для Coordinate
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows; // Для System.Windows.Point, FrameworkElement, DependencyProperty и т.д.
using System.Windows.Input; // Для MouseEventArgs, MouseButtonEventArgs
using System.Windows.Media; // Для Brush, Pen, DrawingVisual, DrawingContext и т.д.
							// --- Добавлен alias для Point ---
using WpfPoint = System.Windows.Point; // Чтобы отличать от System.Drawing.Point, если он будет использоваться
									   // using NtsCoordinate = NetTopologySuite.Geometries.Coordinate; // Уже есть using для NetTopologySuite.Geometries

namespace ConcaveHullwNTS
{
	/// <summary>
	/// Визуальный элемент для отрисовки и интерактивности полигона оболочки.
	/// Использует DrawingVisual для эффективной отрисовки и позволяет выделять сегменты.
	/// Предполагается, что координаты уже преобразованы в координаты Canvas (экрана).
	/// </summary>
	public class InteractivePolygonVisual : FrameworkElement
	{
		// --- Поля для отрисовки ---
		// Кисти и перья для отрисовки сегментов
		private static readonly Brush NormalBrush = Brushes.Red;
		private static readonly Brush HighlightedBrush = Brushes.Crimson;
		private static readonly double NormalThickness = 2.0;
		private static readonly double HighlightedThickness = 4.0;

		private readonly DrawingVisual _visual;
		private Coordinate[]? _shellCoordinates; // Координаты из NTS, уже преобразованные в Canvas

		// Индекс вершины, сегменты которой (предыдущий и следующий) выделены
		// -1 означает, что ничего не выделено
		private int _highlightedVertexIndex = -1;

		// --- События ---
		/// <summary>
		/// Событие, возникающее при выделении сегментов, соединяющихся в вершине.
		/// Передает индекс выделенной вершины.
		/// </summary>
		public event Action<int>? SegmentsHighlighted;

		/// <summary>
		/// Событие, возникающее при клике правой кнопкой мыши на сегменте или вершине.
		/// Передает позицию клика (в координатах Canvas) и индекс ближайшей вершины.
		/// </summary>
		// --- Используем alias в объявлении события ---
		public event Action<WpfPoint, int>? SegmentRightClicked;
		// ---------------------------------------------

		// --- Конструктор ---
		public InteractivePolygonVisual()
		{
			_visual = new DrawingVisual();
			this.AddVisualChild(_visual);
			this.AddLogicalChild(_visual);

			// Подписываемся на события мыши
			this.MouseMove += OnMouseMove;
			this.MouseRightButtonDown += OnMouseRightButtonDown;
			// Убедимся, что элемент может получать фокус мыши для корректной работы событий
			this.Focusable = false; // Обычно не требуется фокус для визуальных элементов
		}

		// --- Необходимые переопределения для FrameworkElement с DrawingVisual ---
		protected override int VisualChildrenCount => 1;

		protected override Visual GetVisualChild(int index)
		{
			if (index != 0)
				throw new ArgumentOutOfRangeException(nameof(index));
			return _visual;
		}

		// --- Основной метод для установки данных ---
		/// <summary>
		/// Устанавливает данные полигона для отрисовки.
		/// Ожидается, что координаты уже преобразованы в координаты Canvas.
		/// </summary>
		/// <param name="shellCoordinates">
		/// Координаты внешней границы (Shell) полигона.
		/// Координаты должны быть в системе координат Canvas.
		/// </param>
		public void SetPolygonData(Coordinate[]? shellCoordinates)
		{
			_shellCoordinates = shellCoordinates;
			_highlightedVertexIndex = -1; // Сбрасываем выделение
			RenderPolygon(); // Перерисовываем
		}

		// --- Метод отрисовки ---
		/// <summary>
		/// Выполняет фактическую отрисовку полигона на DrawingVisual.
		/// </summary>
		private void RenderPolygon()
		{
			// Используем using для корректного освобождения DrawingContext
			using (DrawingContext dc = _visual.RenderOpen())
			{
				// Проверка на наличие данных и минимальное количество точек для полигона
				if (_shellCoordinates == null || _shellCoordinates.Length < 3)
				{
					// Нечего рисовать или недостаточно точек для полигона
					return;
				}

				// --- Преобразуем NTS Coordinate[] в WpfPoint[] ---
				// Предполагается, что X и Y в Coordinate уже являются координатами Canvas
				WpfPoint[] points = new WpfPoint[_shellCoordinates.Length];
				for (int i = 0; i < _shellCoordinates.Length; i++)
				{
					points[i] = new WpfPoint(_shellCoordinates[i].X, _shellCoordinates[i].Y);
				}
				// --------------------------------------------------

				int numPoints = points.Length;

				// Определяем перо (Pen) для выделенных и обычных сегментов
				Pen normalPen = new Pen(NormalBrush, NormalThickness);
				Pen highlightedPen = new Pen(HighlightedBrush, HighlightedThickness);

				// Рисуем каждый сегмент
				for (int i = 0; i < numPoints; i++)
				{
					WpfPoint start = points[i];
					// Следующая точка, с учетом замкнутости полигона
					WpfPoint end = points[(i + 1) % numPoints];

					// Определяем, является ли этот сегмент выделенным
					// Сегмент от points[i] до points[i+1] "соединен" с вершинами i и (i+1)%numPoints
					// Выделим сегмент, если одна из его конечных вершин выделена
					bool isHighlighted = (_highlightedVertexIndex != -1) &&
										 (i == _highlightedVertexIndex || (i + 1) % numPoints == _highlightedVertexIndex);

					Pen penToUse = isHighlighted ? highlightedPen : normalPen;
					dc.DrawLine(penToUse, start, end);
				}
			}
			// DrawingContext автоматически закрывается и изменения применяются
		}

		// --- Логика проверки попадания ---
		/// <summary>
		/// Выполняет проверку попадания курсора мыши на сегмент или вершину.
		/// </summary>
		/// <param name="mousePosition">Позиция курсора мыши в координатах Canvas.</param>
		/// <returns>Индекс ближайшей вершины, если попадание обнаружено; иначе -1.</returns>
		private int HitTestSegments(WpfPoint mousePosition)
		// --------------------------------------------
		{
			if (_shellCoordinates == null || _shellCoordinates.Length < 3)
			{
				return -1;
			}

			// --- Преобразуем NTS Coordinate[] в WpfPoint[] ---
			WpfPoint[] points = new WpfPoint[_shellCoordinates.Length];
			for (int i = 0; i < _shellCoordinates.Length; i++)
			{
				points[i] = new WpfPoint(_shellCoordinates[i].X, _shellCoordinates[i].Y);
			}
			// --------------------------------------------------

			int numPoints = points.Length;
			const double hitTestRadius = 5.0; // Радиус для проверки попадания
			int closestVertexIndex = -1;
			double minDistance = double.MaxValue;

			// Сначала проверяем попадание непосредственно на вершины
			for (int i = 0; i < numPoints; i++)
			{
				WpfPoint vertex = points[i];
				// Вычисляем евклидово расстояние
				double dx = mousePosition.X - vertex.X;
				double dy = mousePosition.Y - vertex.Y;
				double distance = Math.Sqrt(dx * dx + dy * dy);

				if (distance <= hitTestRadius && distance < minDistance)
				{
					minDistance = distance;
					closestVertexIndex = i;
				}
			}

			// Если не попали точно в вершину, проверяем попадание на сегменты
			if (closestVertexIndex == -1)
			{
				for (int i = 0; i < numPoints; i++)
				{
					WpfPoint start = points[i];
					WpfPoint end = points[(i + 1) % numPoints]; // Следующая точка, замыкаем полигон

					// Проверяем расстояние до сегмента
					// --- Используем alias в вызове IsPointNearSegment ---
					if (IsPointNearSegment(mousePosition, start, end, hitTestRadius, out double distance))
					// ---------------------------------------------------
					{
						if (distance < minDistance)
						{
							minDistance = distance;
							// При попадании на сегмент возвращаем индекс начальной вершины сегмента
							closestVertexIndex = i;
						}
					}
				}
			}

			return closestVertexIndex; // Возвращаем индекс ближайшей вершины или -1
		}

		/// <summary>
		/// Проверяет, находится ли точка вблизи отрезка.
		/// </summary>
		/// <param name="p">Точка для проверки (WpfPoint).</param>
		/// <param name="a">Начало отрезка (WpfPoint).</param>
		/// <param name="b">Конец отрезка (WpfPoint).</param>
		/// <param name="radius">Радиус проверки.</param>
		/// <param name="distance">
		/// Выходной параметр. Расстояние от точки до отрезка,
		/// если точка находится в пределах радиуса; иначе значение не определено.
		/// </param>
		/// <returns>
		/// True, если точка находится в пределах радиуса от отрезка; иначе false.
		/// </returns>
		private static bool IsPointNearSegment(WpfPoint p, WpfPoint a, WpfPoint b, double radius, out double distance)
		// --------------------------------------------
		{
			distance = double.MaxValue;

			// Вектор от A к B
			// --- Используем alias для Vector ---
			System.Windows.Vector ab = b - a;
			// Вектор от A к P
			System.Windows.Vector ap = p - a;
			// -----------------------------------

			// Длина вектора AB в квадрате
			double abLengthSquared = ab.X * ab.X + ab.Y * ab.Y;

			// Если A и B совпадают, расстояние - это расстояние до точки A
			if (abLengthSquared == 0)
			{
				distance = Math.Sqrt((ap.X * ap.X) + (ap.Y * ap.Y));
				return distance <= radius;
			}

			// Проекция AP на AB, нормализованная по длине AB
			// t = (AP . AB) / (AB . AB)
			double t = (ap.X * ab.X + ap.Y * ab.Y) / abLengthSquared;

			WpfPoint closestPoint;
			if (t <= 0.0)
			{
				// Ближайшая точка - A
				closestPoint = a;
			}
			else if (t >= 1.0)
			{
				// Ближайшая точка - B
				closestPoint = b;
			}
			else
			{
				// Ближайшая точка лежит на отрезке AB
				// closestPoint = A + t * AB
				// --- Используем alias для Vector ---
				closestPoint = a + t * ab;
				// -----------------------------------
			}

			// Расстояние от P до ближайшей точки на отрезке
			// --- Используем alias для Vector ---
			System.Windows.Vector diff = p - closestPoint;
			distance = Math.Sqrt(diff.X * diff.X + diff.Y * diff.Y);
			// ------------------------------------

			return distance <= radius;
		}

		// --- Логика выделения ---
		/// <summary>
		/// Выделяет сегменты, соединяющиеся в указанной вершине.
		/// </summary>
		/// <param name="vertexIndex">Индекс вершины для выделения.</param>
		public void HighlightSegments(int vertexIndex)
		{
			// Проверка корректности индекса
			if (_shellCoordinates == null || vertexIndex < 0 || vertexIndex >= _shellCoordinates.Length)
			{
				return;
			}

			if (_highlightedVertexIndex != vertexIndex)
			{
				_highlightedVertexIndex = vertexIndex;
				RenderPolygon(); // Перерисовываем с новым выделением
				SegmentsHighlighted?.Invoke(_highlightedVertexIndex); // Уведомляем подписчиков
			}
		}

		/// <summary>
		/// Убирает выделение со всех сегментов.
		/// </summary>
		public void ClearHighlight()
		{
			if (_highlightedVertexIndex != -1)
			{
				_highlightedVertexIndex = -1;
				RenderPolygon(); // Перерисовываем без выделения
								 // Оповещать об очистке выделения пока не будем, если не потребуется
			}
		}

		// --- Обработчики событий мыши ---
		// --- Используем alias в сигнатуре обработчиков ---
		private void OnMouseMove(object sender, MouseEventArgs e)
		{
			// --- Используем alias для Point ---
			WpfPoint mousePosition = e.GetPosition(this); // Получаем позицию в координатах этого элемента (Canvas)
			int hitIndex = HitTestSegments(mousePosition);

			if (hitIndex != -1)
			{
				HighlightSegments(hitIndex);
			}
			else
			{
				// Если мышь не над сегментом/вершиной, сбрасываем выделение
				// Это может вызывать "мерцание", если HitTest нестабилен.
				// В более сложной реализации можно добавить гистерезис или таймер.
				ClearHighlight();
			}
		}

		private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			// --- Используем alias для Point ---
			WpfPoint mousePosition = e.GetPosition(this); // Получаем позицию в координатах этого элемента
			int hitIndex = HitTestSegments(mousePosition);

			if (hitIndex != -1)
			{
				// Вызываем событие, передавая позицию клика и индекс вершины
				// --- Используем alias в вызове события ---
				SegmentRightClicked?.Invoke(mousePosition, hitIndex);
				// Помечаем событие как обработанное, чтобы оно не всплывало дальше
				e.Handled = true;
			}
			// Если попадания не было, событие не помечается как Handled и может всплыть
		}
	}
}