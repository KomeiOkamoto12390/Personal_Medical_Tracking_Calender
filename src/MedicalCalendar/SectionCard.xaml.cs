using System.Windows;
using System.Windows.Controls;

namespace MedicalCalendar;

/// <summary>
/// A category card (Medications, Symptoms, …). <see cref="Compact"/> switches the
/// add row to a two-line layout for narrow places like the calendar side panel.
/// </summary>
public partial class SectionCard : UserControl
{
    public SectionCard()
    {
        InitializeComponent();
        ApplyLayout();
    }

    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact), typeof(bool), typeof(SectionCard),
        new PropertyMetadata(false, (d, _) => ((SectionCard)d).ApplyLayout()));

    public bool Compact { get => (bool)GetValue(CompactProperty); set => SetValue(CompactProperty, value); }

    private void ApplyLayout()
    {
        var cols = AddRow.ColumnDefinitions;
        cols.Clear();

        if (!Compact)
        {
            // [ name * ] [ detail 170 ] [ time 96 ] [ Add ] [ Cancel ]
            foreach (var w in new[] { new GridLength(1, GridUnitType.Star), new GridLength(8), new GridLength(170),
                                      new GridLength(8), new GridLength(96), new GridLength(8), GridLength.Auto, GridLength.Auto })
                cols.Add(new ColumnDefinition { Width = w });
            cols[0].MinWidth = 200;
            Place(NameBox, 0, 0, 1);
            Place(DetailHost, 0, 2, 1);
            Place(TimeBox, 0, 4, 1);
            Place(CommitBtn, 0, 6, 1);
            Place(CancelBtn, 0, 7, 1);
            NameBox.Margin = new Thickness(0);
        }
        else
        {
            // row 0: [ name ............................ ]
            // row 1: [ detail * ] [ time 88 ] [ Add ] [ Cancel ]
            foreach (var w in new[] { new GridLength(1, GridUnitType.Star), new GridLength(8), new GridLength(88),
                                      new GridLength(8), GridLength.Auto, GridLength.Auto })
                cols.Add(new ColumnDefinition { Width = w });
            Place(NameBox, 0, 0, 6);
            Place(DetailHost, 1, 0, 1);
            Place(TimeBox, 1, 2, 1);
            Place(CommitBtn, 1, 4, 1);
            Place(CancelBtn, 1, 5, 1);
            NameBox.Margin = new Thickness(0, 0, 0, 8);
            CardBorder.Padding = new Thickness(16, 14, 16, 16);
            CardBorder.Margin = new Thickness(0, 0, 0, 12);
        }
    }

    private static void Place(UIElement el, int row, int col, int span)
    {
        Grid.SetRow(el, row);
        Grid.SetColumn(el, col);
        Grid.SetColumnSpan(el, span);
    }
}
