// Добавим using alias для часто используемых типов NTS
using NtsConcaveHull = NetTopologySuite.Algorithm.Hull.ConcaveHull;
using NtsGeometryFactory = NetTopologySuite.Geometries.GeometryFactory;
using NtsCoordinate = NetTopologySuite.Geometries.Coordinate;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;
using NtsLinearRing = NetTopologySuite.Geometries.LinearRing;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ConcaveHullwNTS
{
	/// <summary>
	/// Логика взаимодействия для InputSettingsTab.xaml
	/// </summary>
	public partial class InputSettingsTab : UserControl
	{
		// Делегаты и события для коммуникации с MainWindow
		public event Action<string>? StatusUpdated;
		public event Action<string>? InfoUpdated;
		public event Action<NtsGeometry, NtsCoordinate[]>? HullCalculated; // Используем alias

		// Хранилище для загруженных точек и результата
		private NtsCoordinate[]? _loadedCoordinates;
		private NtsGeometry? _resultHullGeometry;

		// Флаги состояния
		private bool _pointsLoaded = false;
		private bool _hullCalculated = false;

		// Настройки файла
		private string _filePath = "";
		private bool _isCsv = true;
		private bool _hasHeader = false;
		private char _delimiter = ',';
		private char _decimalSeparator = '.';
		private CultureInfo _customCulture = CultureInfo.InvariantCulture;
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
				return;	// Элементы ещё не инициализированы, выходим
			}

			// Используем pattern matching и nameof для безопасности типов
			if (ParameterTypeComboBox.SelectedItem is ComboBoxItem item)
			{
				// Предполагается, что MaximumEdgeLengthRatio это второй элемент (индекс 1)
				// и MaximumEdgeLength это первый (индекс 0)
				// Проверка по индексу более надежна, чем по Content, если Content может локализоваться
				_useLengthRatio = ParameterTypeComboBox.SelectedIndex == 1;

				if (_useLengthRatio)
				{
					ParameterValueTextBox.Text = "0.3";
					_parameterValue = 0.3;
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
			if (double.TryParse(ParameterValueTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
			{
				_parameterValue = value;
			}
			// else: оставляем предыдущее значение или NaN, валидация будет при нажатии кнопки
		}

		private async void LoadPointsButton_Click(object sender, RoutedEventArgs e) // async для потенциальной разгрузки UI
		{
			// --- Добавлено: Сброс состояния в самом начале ---
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
		NtsCoordinate[] coordinates = LoadCoordinatesFromFile(_filePath, _hasHeader, _delimiter, _decimalSeparator, ref _headerX, ref _headerY);

				if (coordinates is { Length: > 0 }) // Используем pattern matching с property pattern
				{
					_loadedCoordinates = coordinates;
					_pointsLoaded = true; // Устанавливаем флаг только при успехе
					_loadedFileSettings = (_hasHeader, _delimiter, _decimalSeparator, _headerX, _headerY);
					UpdateButtonStates(); // Обновляем состояние кнопок при успехе
					StatusUpdated?.Invoke($"Успешно загружено {_loadedCoordinates.Length} точек.");
					InfoTextBlock.Text = $"Загружено {_loadedCoordinates.Length} точек из файла '{System.IO.Path.GetFileName(_filePath)}'.";
				}
				else
				{
					// обновление UI при "пустом" результате ---
					StatusUpdated?.Invoke("Ошибка: Не удалось загрузить точки или файл пуст.");
					InfoTextBlock.Text = "Ошибка при загрузке точек.";
				}
			}
			catch (Exception ex)
			{
				// Сброс состояния и обновление UI при исключении ---
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
					SavePolygonToFile(polygon, finalFileName, isCsv, delimiter, hasHeader, _filePath, decimalSeparator);
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

		private static NtsCoordinate[] LoadCoordinatesFromFile(string filePath, bool hasHeader, char delimiter, char decimalSeparator, ref string headerX, ref string headerY)
		{
			var coordinates = new List<NtsCoordinate>();
			var lines = File.ReadAllLines(filePath);
			int startIndex = hasHeader ? 1 : 0;

			// --- Создаем культуру "на лету" с нужным десятичным разделителем ---
			CultureInfo customCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
			customCulture.NumberFormat.NumberDecimalSeparator = decimalSeparator.ToString();
			// -------------------------------------------------------------------

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

				var parts = line.Split(new char[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length >= 2)
				{
					// --- Используем созданную культуру customCulture ---
					if (double.TryParse(parts[0].Trim(), NumberStyles.Float, customCulture, out double x) &&
						double.TryParse(parts[1].Trim(), NumberStyles.Float, customCulture, out double y))
					{
						coordinates.Add(new NtsCoordinate(x, y));
					}
					// else: логируем ошибку парсинга или пропускаем
					// ---
				}
			}

			return [.. coordinates]; // Collection Expression для преобразования List в массив
		}

		private void SavePolygonToFile(NtsPolygon polygon, string filePath, bool isCsv, char delimiter, bool hasHeader, string sourceFileName, char decimalSeparator)
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

			File.WriteAllLines(filePath, lines, Encoding.UTF8);
		}

		private void UpdateButtonStates()
		{
			bool isFileSelected = !string.IsNullOrEmpty(_filePath) && File.Exists(_filePath);
			LoadPointsButton.IsEnabled = isFileSelected;
			CalculateHullButton.IsEnabled = _pointsLoaded;
			SaveResultButton.IsEnabled = _hullCalculated && _resultHullGeometry != null;
		}

		#endregion

	}
}