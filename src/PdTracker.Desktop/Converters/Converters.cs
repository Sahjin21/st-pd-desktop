using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

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
    private static readonly string[] Titles =
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
        => value == null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public static class AutoCompleteBehavior
{
    private sealed class AutoCompleteState
    {
        public bool IsAttached { get; set; }
        public IEnumerable<string>? Source { get; set; }
        public NotifyCollectionChangedEventHandler? CollectionChangedHandler { get; set; }
        public INotifyCollectionChanged? ObservableSource { get; set; }
        public bool SuppressNextTextChanged { get; set; }
    }

    public static readonly DependencyProperty SuggestionsProperty =
        DependencyProperty.RegisterAttached(
            "Suggestions",
            typeof(string[]),
            typeof(AutoCompleteBehavior),
            new PropertyMetadata(null, OnSuggestionsChanged));

    public static string[] GetSuggestions(DependencyObject obj) => (string[])obj.GetValue(SuggestionsProperty);
    public static void SetSuggestions(DependencyObject obj, string[] value) => obj.SetValue(SuggestionsProperty, value);

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

    public static readonly DependencyProperty IsAutoCompleteEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsAutoCompleteEnabled",
            typeof(bool),
            typeof(AutoCompleteBehavior),
            new PropertyMetadata(false, OnAutoCompleteEnabledChanged));

    public static bool GetIsAutoCompleteEnabled(DependencyObject obj) => (bool)obj.GetValue(IsAutoCompleteEnabledProperty);
    public static void SetIsAutoCompleteEnabled(DependencyObject obj, bool value) => obj.SetValue(IsAutoCompleteEnabledProperty, value);

    private static readonly DependencyProperty AutoCompleteStateProperty =
        DependencyProperty.RegisterAttached(
            "AutoCompleteState",
            typeof(AutoCompleteState),
            typeof(AutoCompleteBehavior),
            new PropertyMetadata(null));

    private static Popup? _activePopup;
    private static ListBox? _activeListBox;
    private static TextBox? _associatedTextBox;

    private static AutoCompleteState GetOrCreateState(DependencyObject obj)
    {
        if (obj.GetValue(AutoCompleteStateProperty) is AutoCompleteState state)
            return state;

        state = new AutoCompleteState();
        obj.SetValue(AutoCompleteStateProperty, state);
        return state;
    }

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

    private static void AttachAutoComplete(TextBox textBox, IEnumerable<string>? source)
    {
        var state = GetOrCreateState(textBox);
        UpdateSuggestionSource(textBox, state, source);
        if (state.IsAttached)
            return;

        state.IsAttached = true;

        textBox.GotFocus += (s, args) =>
        {
            if (_associatedTextBox != null && _associatedTextBox != textBox)
                ClosePopup();
        };

        textBox.TextChanged += (s, args) =>
        {
            var currentState = GetOrCreateState(textBox);
            if (currentState.SuppressNextTextChanged)
            {
                currentState.SuppressNextTextChanged = false;
                ClosePopup(textBox);
                return;
            }

            var suggestions = GetSuggestionsFor(textBox);
            var typed = textBox.Text.Trim();
            if (suggestions == null || string.IsNullOrEmpty(typed))
            {
                ClosePopup(textBox);
                return;
            }

            var matches = suggestions
                .Where(x => !string.IsNullOrEmpty(x) &&
                            x.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x)
                .Take(8)
                .ToList();

            if (matches.Count == 0)
            {
                ClosePopup(textBox);
                return;
            }

            ShowPopup(textBox, matches);
        };

        textBox.PreviewKeyDown += (s, args) =>
        {
            if (_activePopup == null || !_activePopup.IsOpen || _associatedTextBox != textBox)
                return;

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
                        CommitSelection(textBox, _activeListBox.SelectedItem.ToString()!);
                    args.Handled = true;
                    break;
                case Key.Escape:
                    ClosePopup(textBox);
                    args.Handled = true;
                    break;
            }
        };

        textBox.LostFocus += (s, args) =>
        {
            Task.Delay(150).ContinueWith(_ =>
            {
                textBox.Dispatcher.Invoke(() => ClosePopup(textBox));
            });
        };
    }

    private static void UpdateSuggestionSource(TextBox textBox, AutoCompleteState state, IEnumerable<string>? source)
    {
        if (ReferenceEquals(state.Source, source))
            return;

        if (state.ObservableSource != null && state.CollectionChangedHandler != null)
            state.ObservableSource.CollectionChanged -= state.CollectionChangedHandler;

        state.Source = source;
        state.ObservableSource = null;
        state.CollectionChangedHandler = null;

        if (source is not INotifyCollectionChanged observable)
            return;

        NotifyCollectionChangedEventHandler handler = (s, args) =>
        {
            if (!ShouldRefreshPopup(textBox))
                return;

            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                ClosePopup(textBox);
                return;
            }

            RefreshPopup(textBox);
        };

        observable.CollectionChanged += handler;
        state.ObservableSource = observable;
        state.CollectionChangedHandler = handler;
    }

    private static IEnumerable<string>? GetSuggestionsFor(TextBox textBox)
        => GetOrCreateState(textBox).Source;

    private static bool ShouldRefreshPopup(TextBox textBox)
        => textBox.IsKeyboardFocusWithin || _associatedTextBox == textBox;

    private static void RefreshPopup(TextBox textBox)
    {
        if (!ShouldRefreshPopup(textBox))
            return;

        var suggestions = GetSuggestionsFor(textBox);
        var typed = textBox.Text.Trim();
        if (suggestions == null || string.IsNullOrEmpty(typed))
        {
            ClosePopup(textBox);
            return;
        }

        var matches = suggestions
            .Where(x => !string.IsNullOrEmpty(x) &&
                        x.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x)
            .Take(8)
            .ToList();

        if (matches.Count == 0)
            ClosePopup(textBox);
        else
            UpdatePopupList(textBox, matches);
    }

    private static void UpdatePopupList(TextBox anchor, List<string> items)
    {
        if (_activeListBox == null || _activePopup == null || _associatedTextBox != anchor)
        {
            ShowPopup(anchor, items);
            return;
        }

        _activeListBox.Items.Clear();
        foreach (var item in items)
            _activeListBox.Items.Add(item);

        if (!_activePopup.IsOpen)
            _activePopup.IsOpen = true;
    }

    private static void ShowPopup(TextBox anchor, List<string> items)
    {
        ClosePopup();

        anchor.Dispatcher.Invoke(() => { });

        var listBox = new ListBox
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
            BorderThickness = new Thickness(1),
            MaxHeight = 200,
            Width = Math.Max(anchor.ActualWidth, 250),
            MinWidth = 250,
            FontSize = 13,
            Foreground = Brushes.Black,
            ItemContainerStyle = new Style(typeof(ListBoxItem))
            {
                Setters = { new Setter(Control.PaddingProperty, new Thickness(6, 4, 6, 4)) }
            }
        };

        foreach (var item in items)
            listBox.Items.Add(item);

        listBox.MouseLeftButtonUp += (s, args) =>
        {
            if (listBox.SelectedItem != null && _associatedTextBox != null)
                CommitSelection(_associatedTextBox, listBox.SelectedItem.ToString()!);
        };

        var popup = new Popup
        {
            PlacementTarget = anchor,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = false,
            Width = Math.Max(anchor.ActualWidth, 250),
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

        anchor.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            popup.IsOpen = true;
        });
    }

    private static void CommitSelection(TextBox textBox, string selected)
    {
        var state = GetOrCreateState(textBox);
        state.SuppressNextTextChanged = true;
        textBox.Text = selected;
        textBox.CaretIndex = selected.Length;
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        ClosePopup(textBox);
    }

    private static void ClosePopup(TextBox? owner = null)
    {
        if (owner != null && _associatedTextBox != owner)
            return;

        if (_activePopup == null)
            return;

        _activePopup.IsOpen = false;
        _activePopup = null;
        _activeListBox = null;
        _associatedTextBox = null;
    }
}
