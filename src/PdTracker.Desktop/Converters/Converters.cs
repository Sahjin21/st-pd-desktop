using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PdTracker.Desktop.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNull = value == null;
        bool inverse = parameter?.ToString() == "Inverse";
        bool visible = inverse ? isNull : !isNull;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StepVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int currentStep && parameter is string paramStr && int.TryParse(paramStr, out var targetStep))
            return currentStep == targetStep ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StepNumberToTitleConverter : IValueConverter
{
    private static readonly string[] Titles = new[]
    {
        "Personal", "Address / Phone", "Charges", "Financial",
        "Spouse", "Other Financial", "Comments / Court", "Juvenile"
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int step && step >= 0 && step < Titles.Length)
            return Titles[step];
        return "Step";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NullToEnabledConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value == null; // Enabled when null (new record), disabled when not null (editing existing)

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

namespace PdTracker.Desktop.Converters
{
    using System.Collections.Specialized;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows;
    using System.Windows.Controls.Primitives;

    // ============================================================
    // AutoComplete Behavior — attached property for TextBox autofill
    //
    // Two modes:
    //   Suggestions      — string[] (static XAML resource, e.g. StaticResource RaceSuggestions)
    //   SuggestionSource — IEnumerable<string>, ideally ObservableCollection<string>
    //                      (live DB-driven; subscribes to CollectionChanged for live refresh)
    //
    // Usage (static):
    //   <TextBox conv:AutoCompleteBehavior.Suggestions="{StaticResource RaceSuggestions}"/>
    //
    // Usage (dynamic DB-driven):
    //   <TextBox conv:AutoCompleteBehavior.SuggestionSource="{Binding RaceSuggestions}"/>
    // ============================================================
    public static class AutoCompleteBehavior
    {

        // --- Attached Properties ---

        // Static string[] source (e.g. declared as x:Array in App.xaml)
        public static readonly DependencyProperty SuggestionsProperty =
            DependencyProperty.RegisterAttached(
                "Suggestions",
                typeof(string[]),
                typeof(AutoCompleteBehavior),
                new PropertyMetadata(null, OnSuggestionsChanged));

        public static string[] GetSuggestions(DependencyObject obj) => (string[])obj.GetValue(SuggestionsProperty);
        public static void SetSuggestions(DependencyObject obj, string[] value) => obj.SetValue(SuggestionsProperty, value);

        // Dynamic IEnumerable<string> source (e.g. ObservableCollection<string> from DB query).
        // Supports INotifyCollectionChanged for live updates when the collection changes.
        public static readonly DependencyProperty SuggestionSourceProperty =
            DependencyProperty.RegisterAttached(
                "SuggestionSource",
                typeof(IEnumerable<string>),
                typeof(AutoCompleteBehavior),
                new PropertyMetadata(null, OnSuggestionSourceChanged));

        public static IEnumerable<string> GetSuggestionSource(DependencyObject obj)
            => (IEnumerable<string>)obj.GetValue(SuggestionSourceProperty);
        public static void SetSuggestionSource(DependencyObject obj, IEnumerable<string> value)
            => obj.SetValue(SuggestionSourceProperty, value);

        // Convenience: enable autocomplete on a TextBox with no predefined source
        public static readonly DependencyProperty IsAutoCompleteEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsAutoCompleteEnabled",
                typeof(bool),
                typeof(AutoCompleteBehavior),
                new PropertyMetadata(false, OnAutoCompleteEnabledChanged));

        public static bool GetIsAutoCompleteEnabled(DependencyObject obj) => (bool)obj.GetValue(IsAutoCompleteEnabledProperty);
        public static void SetIsAutoCompleteEnabled(DependencyObject obj, bool value) => obj.SetValue(IsAutoCompleteEnabledProperty, value);

        // --- Internal state (one active popup at a time) ---
        private static Popup? _activePopup;
        private static ListBox? _activeListBox;
        private static IEnumerable<string>? _activeSuggestions;
        private static TextBox? _associatedTextBox;

        // -------------------------------------------------------
        // Property changed callbacks
        // -------------------------------------------------------

        private static void OnSuggestionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox tb && e.NewValue is string[] arr)
                AttachAutoComplete(tb, arr);
        }

        private static void OnSuggestionSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox tb && e.NewValue is IEnumerable<string> src)
                AttachAutoComplete(tb, src);
        }

        private static void OnAutoCompleteEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox tb && e.NewValue is true)
                AttachAutoComplete(tb, null);
        }

        // -------------------------------------------------------
        // Core attach — handles both string[] and IEnumerable<string>
        // -------------------------------------------------------

        private static void AttachAutoComplete(TextBox textBox, IEnumerable<string>? source)
        {
            if (textBox.Tag is bool alreadyAttached && alreadyAttached)
                return;
            textBox.Tag = true;

            _activeSuggestions = source;

            // Subscribe to CollectionChanged if it's an ObservableCollection (live DB updates)
            if (source is System.Collections.Specialized.INotifyCollectionChanged observable)
            {
                observable.CollectionChanged += (s, args) =>
                {
                    if (string.IsNullOrEmpty(textBox.Text))
                        ClosePopup();
                    else
                        RefreshPopup(textBox);
                };
            }

            textBox.TextChanged += (s, args) =>
            {
                string typed = textBox.Text.Trim();
                if (string.IsNullOrEmpty(typed) || _activeSuggestions == null)
                {
                    ClosePopup();
                    return;
                }

                var matches = _activeSuggestions
                    .Where(x => !string.IsNullOrEmpty(x) &&
                                (x.StartsWith(typed, StringComparison.OrdinalIgnoreCase) ||
                                 x.Contains(typed, StringComparison.OrdinalIgnoreCase)))
                    .Take(8)
                    .ToList();

                if (matches.Count == 0)
                {
                    ClosePopup();
                    return;
                }

                ShowPopup(textBox, matches);
            };

            textBox.PreviewKeyDown += (s, args) =>
            {
                if (_activePopup == null || !_activePopup.IsOpen) return;

                switch (args.Key)
                {
                    case Key.Down:
                        if (_activeListBox != null && _activeListBox.Items.Count > 0)
                        {
                            _activeListBox.SelectedIndex = Math.Min(_activeListBox.SelectedIndex + 1, _activeListBox.Items.Count - 1);
                            _activeListBox.ScrollIntoView(_activeListBox.SelectedItem);
                        }
                        args.Handled = true;
                        break;
                    case Key.Up:
                        if (_activeListBox != null && _activeListBox.SelectedIndex > 0)
                        {
                            _activeListBox.SelectedIndex--;
                            _activeListBox.ScrollIntoView(_activeListBox.SelectedItem);
                        }
                        args.Handled = true;
                        break;
                    case Key.Enter:
                        if (_activeListBox?.SelectedItem != null)
                        {
                            textBox.Text = _activeListBox.SelectedItem.ToString()!;
                            textBox.CaretIndex = textBox.Text.Length;
                            ClosePopup();
                        }
                        args.Handled = true;
                        break;
                    case Key.Escape:
                        ClosePopup();
                        args.Handled = true;
                        break;
                }
            };

            textBox.LostFocus += (s, args) =>
            {
                Task.Delay(150).ContinueWith(_ =>
                {
                    textBox.Dispatcher.Invoke(() => ClosePopup());
                });
            };
        }

        private static void RefreshPopup(TextBox textBox)
        {
            if (_activeSuggestions == null) return;
            string typed = textBox.Text.Trim();
            if (string.IsNullOrEmpty(typed)) { ClosePopup(); return; }

            var matches = _activeSuggestions
                .Where(x => !string.IsNullOrEmpty(x) &&
                            (x.StartsWith(typed, StringComparison.OrdinalIgnoreCase) ||
                             x.Contains(typed, StringComparison.OrdinalIgnoreCase)))
                .Take(8)
                .ToList();

            if (matches.Count == 0)
                ClosePopup();
            else
                ShowPopup(textBox, matches);
        }

        private static void ShowPopup(TextBox anchor, List<string> items)
        {
            ClosePopup();

            var listBox = new ListBox
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                BorderThickness = new Thickness(1),
                MaxHeight = 200,
                Width = anchor.ActualWidth > 0 ? anchor.ActualWidth : anchor.Width,
                FontSize = 13,
                Foreground = Brushes.Black,
                ItemContainerStyle = new Style(typeof(ListBoxItem))
                {
                    Setters = { new Setter(PaddingProperty, new Thickness(6, 4, 6, 4)) }
                }
            };

            listBox.ItemTemplate = new DataTemplate
            {
                VisualTree = new FrameworkElementFactory(typeof(TextBlock)),
            };

            foreach (var item in items)
                listBox.Items.Add(item);

            listBox.SelectionChanged += (s, args) =>
            {
                if (listBox.SelectedItem != null && _associatedTextBox != null)
                {
                    _associatedTextBox.Text = listBox.SelectedItem.ToString()!;
                    _associatedTextBox.CaretIndex = _associatedTextBox.Text.Length;
                }
            };

            listBox.MouseLeftButtonUp += (s, args) =>
            {
                if (listBox.SelectedItem != null && _associatedTextBox != null)
                {
                    _associatedTextBox.Text = listBox.SelectedItem.ToString()!;
                    _associatedTextBox.CaretIndex = _associatedTextBox.Text.Length;
                    ClosePopup();
                }
            };

            var popup = new Popup
            {
                PlacementTarget = anchor,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = false,
                Child = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                    BorderThickness = new Thickness(1),
                    Child = listBox,
                    MaxHeight = 210
                }
            };

            _activePopup = popup;
            _activeListBox = listBox;
            _associatedTextBox = anchor;
            popup.IsOpen = true;
        }

        private static void ClosePopup()
        {
            if (_activePopup != null)
            {
                _activePopup.IsOpen = false;
                _activePopup = null;
                _activeListBox = null;
                _associatedTextBox = null;
            }
        }
    }
}
