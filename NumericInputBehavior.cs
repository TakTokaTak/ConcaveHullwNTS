// NumericInputBehavior.cs
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ConcaveHullwNTS
{
	/// <summary>
	/// Предоставляет присоединенные свойства для управления вводом числовых данных в TextBox.
	/// </summary>
	public static class NumericInputBehavior
	{
		#region AllowOnlyDecimal Property

		/// <summary>
		/// Определяет присоединенное свойство AllowOnlyDecimal.
		/// При значении true ограничивает ввод только допустимыми числовыми символами согласно текущей культуре.
		/// </summary>
		public static readonly DependencyProperty AllowOnlyDecimalProperty =
			DependencyProperty.RegisterAttached(
				"AllowOnlyDecimal",
				typeof(bool),
				typeof(NumericInputBehavior),
				new PropertyMetadata(false, OnAllowOnlyDecimalChanged));

		/// <summary>
		/// Получает значение свойства AllowOnlyDecimal для указанного TextBox.
		/// </summary>
		/// <param name="textBox">TextBox для получения значения свойства.</param>
		/// <returns>Значение свойства AllowOnlyDecimal.</returns>
		public static bool GetAllowOnlyDecimal(TextBox textBox)
		{
			return (bool)textBox.GetValue(AllowOnlyDecimalProperty);
		}

		/// <summary>
		/// Устанавливает значение свойства AllowOnlyDecimal для указанного TextBox.
		/// </summary>
		/// <param name="textBox">TextBox для установки значения свойства.</param>
		/// <param name="value">Значение свойства.</param>
		public static void SetAllowOnlyDecimal(TextBox textBox, bool value)
		{
			textBox.SetValue(AllowOnlyDecimalProperty, value);
		}

		#endregion

		#region AllowNegative Property

		/// <summary>
		/// Определяет присоединенное свойство AllowNegative.
		/// При значении true разрешает ввод знака минус в начале числа.
		/// Имеет эффект только если AllowDecimal=true.
		/// </summary>
		public static readonly DependencyProperty AllowNegativeProperty =
			DependencyProperty.RegisterAttached(
				"AllowNegative",
				typeof(bool),
				typeof(NumericInputBehavior),
				new PropertyMetadata(true, OnAllowOnlyDecimalChanged)); // Используем тот же обработчик, так как он управляет подписками

		/// <summary>
		/// Получает значение свойства AllowNegative для указанного TextBox.
		/// </summary>
		/// <param name="textBox">TextBox для получения значения свойства.</param>
		/// <returns>Значение свойства AllowNegative.</returns>
		public static bool GetAllowNegative(TextBox textBox)
		{
			return (bool)textBox.GetValue(AllowNegativeProperty);
		}

		/// <summary>
		/// Устанавливает значение свойства AllowNegative для указанного TextBox.
		/// </summary>
		/// <param name="textBox">TextBox для установки значения свойства.</param>
		/// <param name="value">Значение свойства.</param>
		public static void SetAllowNegative(TextBox textBox, bool value)
		{
			textBox.SetValue(AllowNegativeProperty, value);
		}

		#endregion

		private static void OnAllowOnlyDecimalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			// Обработчик используется для обоих свойств (AllowOnlyDecimal и AllowNegative)
			// потому что они тесно связаны и требуют одних и тех же подписок/отписок.
			if (d is TextBox textBox)
			{
				// Отписка от событий для предотвращения дублирования
				textBox.PreviewTextInput -= TextBoxPreviewTextInput;
				textBox.PreviewKeyDown -= TextBoxPreviewKeyDown;
				DataObject.RemovePastingHandler(textBox, OnPaste);

				// Подписка на события при активации функции (AllowOnlyDecimal=true)
				// AllowNegative учитывается внутри обработчиков
				if (GetAllowOnlyDecimal(textBox)) // Используем GetAllowOnlyDecimal для проверки
				{
					textBox.PreviewTextInput += TextBoxPreviewTextInput;
					textBox.PreviewKeyDown += TextBoxPreviewKeyDown;
					DataObject.AddPastingHandler(textBox, OnPaste);
				}
			}
		}

		private static void TextBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			var textBox = (TextBox)sender;
			// Получаем текст, который будет в TextBox после ввода
			string newText = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength).Insert(textBox.CaretIndex, e.Text);

			// Проверяем, является ли результат допустимым числовым вводом
			e.Handled = !IsValidNumericInput(newText, GetAllowNegative(textBox));
		}

		private static void TextBoxPreviewKeyDown(object sender, KeyEventArgs e)
		{
			// Разрешаем стандартные клавиши редактирования
			if (e.Key is Key.Back or Key.Delete or Key.Tab or Key.Left or Key.Right or Key.End or Key.Home)
				return;
			// Разрешаем Ctrl+V, Ctrl+C
			if ((e.Key == Key.V || e.Key == Key.C) && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
				return;


			var textBox = (TextBox)sender;

			// Разрешаем цифры на основной клавиатуре (D0-D9)
			if (e.Key >= Key.D0 && e.Key <= Key.D9) return;

			// Разрешаем цифры на цифровой клавиатуре (NumPad0-NumPad9)
			if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) return;

			// Обработка знака минус
			if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
			{
				bool allowNegative = GetAllowNegative(textBox);
				if (!allowNegative)
				{
					e.Handled = true; // Запрещаем минус, если не разрешено
					return;
				}

				string text = textBox.Text;
				int selectionStart = textBox.SelectionStart;
				int selectionLength = textBox.SelectionLength;

				// Минус разрешен только в начале числа и только один раз
				// Проверяем, будет ли минус находиться в начале после вставки
				// (то есть, вставка происходит в позиции 0 или выделение начинается с 0)
				if (selectionStart == 0)
				{
					// Проверяем, есть ли уже минус в начале (в части текста, которая останется)
					// Учитываем, что выделенная часть будет удалена
					string textAfterRemoval = text.Remove(selectionStart, selectionLength);
					if (textAfterRemoval.StartsWith('-'))
					{
						// Уже есть минус в начале, запрещаем ввод нового
						e.Handled = true;
						return;
					}
					// Если минуса в начале нет, разрешаем его ввод
					// Стандартная обработка клавиши минуса TextBox справится с этим
					return;
				}
				else
				{
					// Попытка ввести минус не в начале - запрещаем
					e.Handled = true;
					return;
				}
			}

			// Обработка десятичного разделителя
			if (e.Key == Key.Decimal || e.Key == Key.OemComma || e.Key == Key.OemPeriod)
			{
				bool allowDecimal = GetAllowOnlyDecimal(textBox); // Должно быть true, если мы здесь

				if (!allowDecimal)
				{
					e.Handled = true;
					return;
				}

				string separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
				string text = textBox.Text;
				int selectionStart = textBox.SelectionStart;
				int selectionLength = textBox.SelectionLength;

				// Проверяем, есть ли уже десятичный разделитель в тексте
				// (учитываем выделенный текст, который будет заменен)
				string textToCheck = text.Remove(selectionStart, selectionLength);
				if (textToCheck.Contains(separator))
				{
					// Если разделитель уже есть, запрещаем ввод нового
					e.Handled = true;
					return;
				}

				// Если разделителя нет, вставляем корректный для культуры
				// Удаляем выделенный текст и вставляем разделитель в позицию каретки
				string newText = text.Remove(selectionStart, selectionLength).Insert(selectionStart, separator);
				textBox.Text = newText;

				// Устанавливаем каретку ПОСЛЕ вставленного разделителя
				textBox.CaretIndex = selectionStart + separator.Length;

				e.Handled = true; // Предотвращаем стандартную обработку клавиши
				return;
			}

			// Запрещаем все остальные клавиши
			e.Handled = true;
		}

		private static void OnPaste(object sender, DataObjectPastingEventArgs e)
		{
			if (e.DataObject.GetDataPresent(DataFormats.Text))
			{
				if (sender is not TextBox textBox)
				{
					e.CancelCommand();
					return;
				}

				bool allowNegative = GetAllowNegative(textBox);
				// bool allowDecimal = GetAllowDecimal(textBox); // Не используется напрямую в проверке, но подразумевается

				string textToPaste = (string)e.DataObject.GetData(DataFormats.Text);

				// Симулируем вставку: получаем текст, который будет в TextBox после вставки
				string newText = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength).Insert(textBox.SelectionStart, textToPaste);

				// Проверяем, является ли результат допустимым числовым вводом
				if (!IsValidNumericInput(newText, allowNegative))
				{
					// Если нет, отменяем операцию вставки
					e.CancelCommand();
				}
				// Если проверка прошла, операция вставки продолжится нормально.
			}
			else
			{
				// Если вставляются данные не текстового формата, отменяем
				e.CancelCommand();
			}
		}

		/// <summary>
		/// Проверяет, является ли строка допустимым числовым вводом.
		/// </summary>
		/// <param name="input">Строка для проверки.</param>
		/// <param name="allowNegative">Разрешены ли отрицательные числа.</param>
		/// <returns>True, если строка допустима; иначе false.</returns>
		private static bool IsValidNumericInput(string input, bool allowNegative)
		{
			// Разрешаем пустую строку или только знак минуса (для промежуточных состояний)
			if (string.IsNullOrEmpty(input) || input == "-")
				return true;

			// Если отрицательные числа запрещены, проверяем, начинается ли строка с минуса
			if (!allowNegative && input.StartsWith('-'))
				return false;

			// Используем NumberStyles.Float для строгой проверки формата числа с плавающей точкой
			// Используем CultureInfo.CurrentCulture для соответствия региональным настройкам пользователя
			return double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out _);
		}
	}
}
