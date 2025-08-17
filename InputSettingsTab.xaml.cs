// Добавим using alias для часто используемых типов NTS
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.TextFormatting;
using System.Xml;
using NtsConcaveHull = NetTopologySuite.Algorithm.Hull.ConcaveHull;
using NtsCoordinate = NetTopologySuite.Geometries.Coordinate;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using NtsGeometryFactory = NetTopologySuite.Geometries.GeometryFactory;
using NtsLinearRing = NetTopologySuite.Geometries.LinearRing;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;

namespace ConcaveHullwNTS
{
	/// <summary>
	/// Логика взаимодействия для InputSettingsTab.xaml
	/// </summary>
	public partial class InputSettingsTab : UserControl
	{
		// Делегаты и события для коммуникации с MainWindow
		public event Action<string>? StatusUpdated;
		public event Action<NtsGeometry, NtsCoordinate[]>? HullCalculated; // Используем alias
		public event Action<NtsCoordinate[]>? PointsLoaded;

		// Хранилище для загруженных точек и результата
		private NtsCoordinate[]? _loadedCoordinates;
		private NtsGeometry? _resultHullGeometry;

		// Флаги состояния
		private bool _pointsLoaded = false;
		private bool _hullCalculated = false;

		// Настройки файла
		private string _filePath = "";
		private bool _hasHeader = false;
		private char _delimiter = ';';
		private char _decimalSeparator = ',';
#pragma warning disable IDE0044 // Добавить модификатор только для чтения
		private Encoding _fileEncoding = Encoding.UTF8;
#pragma warning restore IDE0044 // Добавить модификатор только для чтения
		private (bool hasHeader, char delimiter, char decimalSeparator, string headerX, string headerY) _loadedFileSettings;

		// Параметры ConcaveHull
		private bool _useLengthRatio = true; // true для MaximumEdgeLengthRatio, false для MaximumEdgeLength
		private double _parameterValue = 0.3;
		
		public InputSettingsTab()
		{
			InitializeComponent();
			GetFileFormatSettings();
			UpdateButtonStates();
		}

		#region Обработчики событий UI

		private void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new Microsoft.Win32.OpenFileDialog
			{
				Filter = "CSV or Text files (*.csv, *.txt)|*.csv;*.txt|All files (*.*)|*.*"
			};

			if (openFileDialog.ShowDialog() == true)
			{
				_filePath = openFileDialog.FileName;
				FilePathTextBox.Text = _filePath;
				UpdateButtonStates();
				InfoTextBlock.Text = $"Выбран файл: {_filePath}";
			}
		}

		private void ParameterTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (ParameterTypeComboBox == null || ParameterValueTextBox == null)
			{
				return; // Элементы ещё не инициализированы, выходим
			}

			// Используем pattern matching и nameof для безопасности типов
			if (ParameterTypeComboBox.SelectedItem is ComboBoxItem)
			{
				// Предполагается, что MaximumEdgeLengthRatio это второй элемент (индекс 1)
				// и MaximumEdgeLength это первый (индекс 0)
				// Проверка по индексу более надежна, чем по Content, если Content может локализоваться
				_useLengthRatio = ParameterTypeComboBox.SelectedIndex == 1;

				if (_useLengthRatio)
				{
					ParameterValueTextBox.Text = "";
					_parameterValue = double.NaN;
				}
				else // MaximumEdgeLength
				{
					ParameterValueTextBox.Text = "";
					_parameterValue = double.NaN;
				}
			}
		}


		private void ParameterValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (sender is TextBox textBox)
			{
				string text = textBox.Text;

				// Разрешаем пустую строку или только знак минуса
				if (string.IsNullOrEmpty(text) || text == "-")
				{
					_parameterValue = double.NaN; // Или другое значение по умолчанию, если нужно
												  // Сбрасываем цвет фона при промежуточном вводе
					textBox.Background = System.Windows.Media.Brushes.White;
					return;
				}

				// Пытаемся распознать число с учетом текущей культуры
				// Используем NumberStyles.Float для согласованности и строгости
				if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value))
				{
					_parameterValue = value;
					// Визуальная обратная связь: корректный ввод
					textBox.Background = System.Windows.Media.Brushes.White;
				}
				else
				{
					// Визуальная обратная связь: некорректный ввод
					textBox.Background = System.Windows.Media.Brushes.LightCoral;
				}
			}
		}

		private void LoadPointsButton_Click(object sender, RoutedEventArgs e) // async для потенциальной разгрузки UI
		{
			// Сброс состояния в самом начале ---
			// На случай, если это повторная попытка загрузки после ошибки
			// Флаги сбросятся, если загрузка пройдет успешно
			_pointsLoaded = false;
			_hullCalculated = false;
			_resultHullGeometry = null;
			UpdateButtonStates(); // Обновляем состояние кнопок сразу

			if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
			{
				StatusUpdated?.Invoke("Ошибка: Файл не выбран или не существует.");
				return;
			}

			try
			{
				GetFileFormatSettings(); // Получаем актуальные настройки из UI
				string _headerX = "";
				string _headerY = "";
				// Для простоты оставим синхронно, так как файлы, вероятно, не гигантские
				NtsCoordinate[] coordinates = LoadCoordinatesFromFile(_filePath, _hasHeader, _delimiter, _decimalSeparator, _fileEncoding, ref _headerX, ref _headerY);

				if (coordinates is { Length: > 0 }) // Используем pattern matching с property pattern
				{
					_loadedCoordinates = coordinates;
					_pointsLoaded = true; // Устанавливаем флаг только при успехе
					_loadedFileSettings = (_hasHeader, _delimiter, _decimalSeparator, _headerX, _headerY);
					UpdateButtonStates(); // Обновляем состояние кнопок при успехе
					StatusUpdated?.Invoke($"Успешно загружено {_loadedCoordinates.Length} точек.");
					InfoTextBlock.Text += $"\nЗагружено {_loadedCoordinates.Length} точек из файла '{System.IO.Path.GetFileName(_filePath)}'.";
					PointsLoaded?.Invoke(_loadedCoordinates); // Уведомляем MainWindow о загрузке точек 
				}
				else
				{
					// обновление UI при "пустом" результате
					StatusUpdated?.Invoke("Ошибка: Не удалось загрузить точки или файл пуст.");
					InfoTextBlock.Text += $"\nОшибка при загрузке точек.";
				}
			}
			catch (Exception ex)
			{
				// Сброс состояния и обновление UI при исключении
				_pointsLoaded = false;
				_hullCalculated = false;
				_resultHullGeometry = null;
				UpdateButtonStates();
				StatusUpdated?.Invoke($"Ошибка при загрузке точек: {ex.Message}");
				InfoTextBlock.Text = $"Ошибка: {ex.Message}";
			}
		}

		private async void CalculateHullButton_Click(object sender, RoutedEventArgs e) // async
		{
			if (!_pointsLoaded || _loadedCoordinates == null || _loadedCoordinates.Length == 0)
			{
				StatusUpdated?.Invoke("Ошибка: Точки не загружены.");
				return;
			}

			if (_useLengthRatio && (_parameterValue < 0 || _parameterValue > 1))
			{
				StatusUpdated?.Invoke("Ошибка: Значение MaximumEdgeLengthRatio должно быть между 0 и 1.");
				return;
			}

			if (!_useLengthRatio && double.IsNaN(_parameterValue))
			{
				StatusUpdated?.Invoke("Ошибка: Необходимо ввести корректное значение MaximumEdgeLength.");
				return;
			}

			try
			{
				// 1. Создаем MultiPoint из загруженных координат
				var factory = NtsGeometryFactory.Default;
				var multiPoint = factory.CreateMultiPointFromCoords(_loadedCoordinates);

				// 2. Вычисляем вогнутую оболочку
				const bool holesAllowed = false; // Отверстий пока не будет
				NtsGeometry hullGeometry = await Task.Run(() => // Выполняем вычисление в фоновом потоке
				{
					if (_useLengthRatio)
					{
						return NtsConcaveHull.ConcaveHullByLengthRatio(multiPoint, _parameterValue, holesAllowed);
					}
					else
					{
						return NtsConcaveHull.ConcaveHullByLength(multiPoint, _parameterValue, holesAllowed);
					}
				});

				// 3. Сохраняем результат
				_resultHullGeometry = hullGeometry;
				_hullCalculated = true;
				UpdateButtonStates();

				// 4. Сообщаем MainWindow о результате
				HullCalculated?.Invoke(_resultHullGeometry, _loadedCoordinates);

				StatusUpdated?.Invoke("Вогнутая оболочка успешно вычислена.");
				InfoTextBlock.Text += $"\nОболочка вычислена. Тип результата: {hullGeometry.GeometryType}.";

				if (hullGeometry is NtsPolygon polygon)
				{
					var shellCoords = polygon.Shell.Coordinates;
					InfoTextBlock.Text += $"\nВнешняя граница содержит {shellCoords.Length - 1} уникальных точек."; // -1 потому что первая и последняя совпадают
				}
			}
			catch (Exception ex)
			{
				StatusUpdated?.Invoke($"Ошибка при вычислении оболочки: {ex.Message}");
				InfoTextBlock.Text += $"\nОшибка: {ex.Message}";
			}
		}

		private void SaveResultButton_Click(object sender, RoutedEventArgs e)
		{
			if (!_hullCalculated || _resultHullGeometry is not NtsPolygon polygon)
			{
				StatusUpdated?.Invoke("Ошибка: Оболочка не вычислена или результат некорректен.");
				return;
			}

			try
			{
				GetFileFormatSettings(); // Обновляем поля _hasHeader, _delimiter, _decimalSeparator из UI
				bool hasHeader = _hasHeader;
				char delimiter = _delimiter; 
				char decimalSeparator = _decimalSeparator; 

				Microsoft.Win32.SaveFileDialog saveFileDialog = new()
				{
					Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt",
					FileName = System.IO.Path.GetFileNameWithoutExtension(_filePath) + "_Оболочка"
				};

				if (saveFileDialog.ShowDialog() == true)
				{
					// --- Определяем тип файла на основе расширения, введенного/выбранного пользователем ---
					string selectedExtension = System.IO.Path.GetExtension(saveFileDialog.FileName);
					bool isCsv = selectedExtension.Equals(".csv", StringComparison.OrdinalIgnoreCase);

					// Убеждаемся, что имя файла заканчивается правильным расширением
					// (на случай, если пользователь ввел имя без расширения и выбрал фильтр)
					string finalFileName = saveFileDialog.FileName;
					if (!finalFileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) &&
						!finalFileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
					{
						// Это маловероятно с заданным Filter, но на всякий случай
						finalFileName = System.IO.Path.ChangeExtension(finalFileName, isCsv ? ".csv" : ".txt");
					}

					// Передаем все необходимые настройки в метод сохранения
					SavePolygonToFile(polygon, finalFileName, delimiter, hasHeader, decimalSeparator);
					StatusUpdated?.Invoke($"Результат успешно сохранен в файл '{finalFileName}'.");
					InfoTextBlock.Text += $"\nРезультат сохранен в '{finalFileName}'.";
				}
			}
			catch (Exception ex)
			{
				StatusUpdated?.Invoke($"Ошибка при сохранении результата: {ex.Message}");
				InfoTextBlock.Text += $"\nОшибка сохранения: {ex.Message}";
			}
		}

		#endregion

		#region Вспомогательные методы


		private void GetFileFormatSettings()
		{
			_hasHeader = HasHeaderCheckBox.IsChecked == true;

			// Используем switch expression для более лаконичного кода (C# 8+)
			_delimiter = (DelimiterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
			{
				"Запятая (,)" => ',',
				"Точка с запятой (;)" => ';',
				"Пробел" => ' ',
				"Табуляция" => '\t',
				_ => ';' // Значение по умолчанию (точка с запятой)
			};

			_decimalSeparator = (DecimalSeparatorComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
			{
				"Запятая (,)" => ',',
				"Точка (.)" => '.',
				_ => '.' // Значение по умолчанию (точка)
			};
		}

		private NtsCoordinate[] LoadCoordinatesFromFile(string filePath, bool hasHeader, char delimiter, char decimalSeparator, Encoding encoding, ref string headerX, ref string headerY)
		{
			var coordinates = new List<NtsCoordinate>();

			if (delimiter == decimalSeparator)
			{
				InfoTextBlock.Text += $"\nРазделитель столбцов не должен совпадать с десятичным разделителем. Такое не обрабатываем";
				return [];
			}

			Encoding finalEncoding = encoding;
			// Автоопределения кодировки, нужно только для заголовка
			if (hasHeader)
			{
				try
				{
					finalEncoding = DetectFileEncoding(filePath); // Попытка автоопределения																  
					InfoTextBlock.Text += $"\nАвтоопределена кодировка: {finalEncoding.EncodingName}"; // Можно вывести в InfoTextBlock информацию о выбранной кодировке
				}
				catch (Exception ex)
				{
					// Если автоопределение не удалось, используем UTF-8
					finalEncoding = Encoding.UTF8;
					InfoTextBlock.Text += $"\nНе удалось определить кодировку, пусть будет UTF-8. Ошибка: {ex.Message}";
				}
			}

			var lines = File.ReadAllLines(filePath, finalEncoding);
			int startIndex = hasHeader ? 1 : 0;

			// Создаем культуру "на лету" с нужным десятичным разделителем 
			CultureInfo customCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
			customCulture.NumberFormat.NumberDecimalSeparator = decimalSeparator.ToString();

			if (hasHeader)
			{
				string line = lines[0].Trim();
				if (!string.IsNullOrEmpty(line))
				{
					string[]? parts = line.Split([delimiter], StringSplitOptions.RemoveEmptyEntries);
					headerX = parts[0];
					if (parts.Length > 1) headerY = parts[1];
				}
			}

			for (int i = startIndex; i < lines.Length; i++)
			{
				var line = lines[i].Trim();
				if (string.IsNullOrEmpty(line)) continue;

				var parts = line.Split([delimiter], StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length >= 2)
				{
					if (double.TryParse(parts[0].Trim(), NumberStyles.Float, customCulture, out double x) &&
						double.TryParse(parts[1].Trim(), NumberStyles.Float, customCulture, out double y))
					{
						coordinates.Add(new NtsCoordinate(x, y));
					}
					else InfoTextBlock.Text += $"\nНераспознанные данные: в {i+1} строке:  {line}";
				}
			}
			return [.. coordinates]; // Collection Expression для преобразования List в массив
		}


		/// <summary>
		/// Определяет кодировку текстового файла только по BOM (Byte Order Mark) в его начале.
		/// Если BOM не найден, проверяем UTF-8 без BOM по первой строке. Если не UTF-8, то возвращается кодировка Windows-1251.
		/// </summary>
		/// <param name="filePath">Путь к файлу.</param>
		/// <returns>Объект Encoding, представляющий определенную кодировку или Windows-1251 по умолчанию.</returns>
		private static Encoding DetectFileEncoding(string filePath)
		{
			// 1. Сначала проверяем BOM
			using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				byte[] bom = new byte[4];
				int bytesRead = fs.Read(bom, 0, bom.Length);

				// Проверка известных BOM
				if (bytesRead >= 4 && bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF)
					return Encoding.GetEncoding("utf-32BE");

				if (bytesRead >= 4 && bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00)
					return Encoding.UTF32;

				if (bytesRead >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
					return Encoding.UTF8;

				if (bytesRead >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
					return Encoding.BigEndianUnicode;

				if (bytesRead >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
					return Encoding.Unicode;

				// 2. Если BOM не найден, проверяем UTF-8 без BOM по первой строке
				fs.Position = 0; // Возвращаемся в начало файла

				// Читаем первые 4096 байт (достаточно для анализа первой строки)
				byte[] buffer = new byte[4096];
				int readBytes = fs.Read(buffer, 0, buffer.Length);

				// Проверяем, является ли содержимое валидным UTF-8
				if (IsValidUtf8(buffer, readBytes))
				{
					return Encoding.UTF8;
				}
			}

			// 3. Если не UTF-8, возвращаем Windows-1251 по умолчанию
			return Encoding.GetEncoding(1251);
		}

		private static bool IsValidUtf8(byte[] buffer, int length)
		{
			int position = 0;
			while (position < length)
			{
				byte current = buffer[position];

				// ASCII (0x00-0x7F)
				if (current <= 0x7F)
				{
					position++;
					continue;
				}

				// Проверяем валидность UTF-8 последовательности
				int followingBytes;
				if ((current & 0xE0) == 0xC0) followingBytes = 1;  // 2 байта
				else if ((current & 0xF0) == 0xE0) followingBytes = 2; // 3 байта
				else if ((current & 0xF8) == 0xF0) followingBytes = 3; // 4 байта
				else return false; // Невалидный первый байт

				// Проверяем наличие следующих байтов
				if (position + followingBytes >= length)
					break;

				// Проверяем следующие байты (должны быть 10xxxxxx)
				for (int i = 1; i <= followingBytes; i++)
				{
					if ((buffer[position + i] & 0xC0) != 0x80)
						return false;
				}

				position += followingBytes + 1;
			}

			return true;
		}


		private void SavePolygonToFile(
			NtsPolygon polygon,
			string filePath,
			char delimiter,
			bool hasHeader,
			char decimalSeparator)
		{
			var lines = new List<string>();

			if (hasHeader)
			{
				string header = $"{_loadedFileSettings.headerX}{delimiter}{_loadedFileSettings.headerY}";
				lines.Add(header);
			}

			var shell = polygon.Shell;
			var shellCoordinates = shell.Coordinates;
			// --- Создаем культуру "на лету" для форматирования чисел с нужным десятичным разделителем ---
			CultureInfo writeCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
			writeCulture.NumberFormat.NumberDecimalSeparator = decimalSeparator.ToString();

			lines.AddRange(shellCoordinates
							 .Select(coord => $"{coord.X.ToString(writeCulture)}{delimiter}{coord.Y.ToString(writeCulture)}"));

			// Если бы были дыры:
			// foreach (var hole in polygon.Holes) { ... }

			File.WriteAllLines(filePath, lines, new UTF8Encoding(true)); // Всегда сохраняем в UTF-8 с BOM
		}

		private void UpdateButtonStates()
		{
			bool isFileSelected = !string.IsNullOrEmpty(_filePath) && File.Exists(_filePath);
			LoadPointsButton.IsEnabled = isFileSelected;
			CalculateHullButton.IsEnabled = _pointsLoaded;
			SaveResultButton.IsEnabled = _hullCalculated && _resultHullGeometry != null;
		}

		public void TriggerSave()
		{
			// Этот метод будет вызываться из MainWindow при нажатии кнопки сохранения на вкладке визуализации
			// Он просто вызывает существующий обработчик, имитируя клик пользователя
			// Это позволяет переиспользовать всю логику проверок и сохранения
			SaveResultButton_Click(SaveResultButton, new RoutedEventArgs());
		}

		#endregion

		#region Public API

		/// <summary>
		/// Обновляет внутреннее состояние результата оболочки.
		/// Используется для синхронизации с внешними изменениями геометрии (например, из VisualizationTab).
		/// </summary>
		/// <param name="newHullGeometry">Новая геометрия оболочки.</param>
		public void UpdateCurrentHullResult(NtsGeometry? newHullGeometry)
		{
			// Обновляем внутреннее поле с результатом
			_resultHullGeometry = newHullGeometry;

			// Обновляем флаг, что оболочка "вычислена" (или скорректируйте логику по необходимости)
			// Если новая геометрия null, возможно, стоит сбросить флаг.
			_hullCalculated = newHullGeometry != null;

			// Обновляем состояние кнопок, так как доступность "Сохранить" зависит от _hullCalculated и _resultHullGeometry
			UpdateButtonStates();

			//System.Diagnostics.Debug.WriteLine($"InputSettingsTab: Hull result updated via external modification. HullCalculated = {_hullCalculated}");
		}

		#endregion
	}
}