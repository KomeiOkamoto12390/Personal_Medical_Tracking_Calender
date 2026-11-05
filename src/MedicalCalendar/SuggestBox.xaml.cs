using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MedicalCalendar;

/// <summary>
/// Text box with a grouped suggestion list that filters as you type.
/// ↑/↓ move through suggestions, Enter picks one, Esc closes the list.
/// Typing something not in the list is fine — the caller treats it as a custom item.
/// </summary>
public partial class SuggestBox : UserControl
{
    private bool _suppress;            // true while we set Input.Text from code
    private string _filter = "";
    private DateTime _closedAt;

    public SuggestBox()
    {
        InitializeComponent();
    }

    // ---------- Bindable properties ----------

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SuggestBox),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(System.Collections.IList), typeof(SuggestBox),
        new PropertyMetadata(null, (d, e) => ((SuggestBox)d).AttachView()));

    /// <summary>The CatalogItem list. Each SuggestBox builds its own grouped, filtered view of it.</summary>
    public System.Collections.IList? ItemsSource { get => (System.Collections.IList?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    private System.Windows.Data.ListCollectionView? _view;

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(SuggestBox), new PropertyMetadata(""));

    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    public static readonly DependencyProperty RemoveItemCommandProperty = DependencyProperty.Register(
        nameof(RemoveItemCommand), typeof(ICommand), typeof(SuggestBox));

    public ICommand? RemoveItemCommand { get => (ICommand?)GetValue(RemoveItemCommandProperty); set => SetValue(RemoveItemCommandProperty, value); }

    public bool IsOpen => Pop.IsOpen;

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (SuggestBox)d;
        var text = (string?)e.NewValue ?? "";
        if (box.Input.Text != text)
        {
            box._suppress = true;               // set from code (reset / edit): don't pop the list open
            box.Input.Text = text;
            box.Input.CaretIndex = text.Length;
            box._suppress = false;
        }
        box.Hint.Visibility = text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AttachView()
    {
        _view = null;
        if (ItemsSource != null)
        {
            _view = new System.Windows.Data.ListCollectionView(ItemsSource);
            _view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(CatalogItem.Group)));
            _view.Filter = o => _filter.Length == 0
                || ((CatalogItem)o).Name.Contains(_filter, StringComparison.CurrentCultureIgnoreCase);
        }
        List.ItemsSource = _view;
    }

    // ---------- Behaviour ----------

    private void ApplyFilter(string text)
    {
        _filter = text.Trim();
        _view?.Refresh();
        List.SelectedIndex = -1;
        bool empty = _view?.IsEmpty ?? true;
        NoMatch.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        NoMatch.Text = $"No match — press Enter to add “{_filter}” as your own item.";
        List.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Open(string filter)
    {
        ApplyFilter(filter);
        if (_filter.Length == 0 && (_view?.IsEmpty ?? true)) return;
        Pop.IsOpen = true;
    }

    private void Choose(CatalogItem item)
    {
        Pop.IsOpen = false;
        SetCurrentValue(TextProperty, item.Name);
        Input.Focus();
        Input.CaretIndex = Input.Text.Length;
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        Hint.Visibility = Input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_suppress) return;
        SetCurrentValue(TextProperty, Input.Text);
        if (Input.IsKeyboardFocused)
        {
            if (Input.Text.Length == 0) Pop.IsOpen = false;
            else Open(Input.Text);
        }
    }

    private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (!Pop.IsOpen) Open(Input.Text);
                else Move(1);
                e.Handled = true;
                break;
            case Key.Up when Pop.IsOpen:
                Move(-1);
                e.Handled = true;
                break;
            case Key.Enter when Pop.IsOpen:
                if (List.SelectedItem is CatalogItem item) { Choose(item); e.Handled = true; }
                else Pop.IsOpen = false;       // fall through: Enter adds the typed text
                break;
            case Key.Escape when Pop.IsOpen:
                Pop.IsOpen = false;
                e.Handled = true;
                break;
            case Key.Tab:
                Pop.IsOpen = false;
                break;
        }
    }

    private void Move(int step)
    {
        int count = List.Items.Count;
        if (count == 0) return;
        int i = List.SelectedIndex + step;
        List.SelectedIndex = Math.Clamp(i, 0, count - 1);
        List.ScrollIntoView(List.SelectedItem);
    }

    private void Input_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Keep the list open if focus moved into it (e.g. clicking a remove button).
        if (e.NewFocus is DependencyObject d && IsInside(d, Pop.Child)) return;
        Pop.IsOpen = false;
    }

    private void Arrow_Click(object sender, RoutedEventArgs e)
    {
        // A click on the arrow while open first closes the popup (StaysOpen=False); don't reopen it.
        if ((DateTime.UtcNow - _closedAt).TotalMilliseconds < 200) return;
        Input.Focus();
        Open("");                               // show everything
    }

    private void Pop_Closed(object? sender, EventArgs e) => _closedAt = DateTime.UtcNow;

    private void List_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject src) return;
        if (FindAncestor<Button>(src) != null) return;          // the × button handles itself
        if (FindAncestor<ListBoxItem>(src)?.DataContext is CatalogItem item) Choose(item);
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null && d is not T) d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        return d as T;
    }

    private static bool IsInside(DependencyObject d, DependencyObject? root)
    {
        for (; d != null; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d == root) return true;
        return false;
    }
}
