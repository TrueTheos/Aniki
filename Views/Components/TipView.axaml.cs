using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Aniki.Views;

internal sealed partial class TipView : UserControl
{
    private const double Gap = 10, Pad = 4, Edge = 12;
    private readonly Control _anchor;
    private Rect _target;

    public TipView(string message, Control anchor)
    {
        InitializeComponent();
        TipText.Text = message;
        _anchor = anchor;
        anchor.LayoutUpdated += OnAnchorLayoutUpdated;
        anchor.DetachedFromVisualTree += OnAnchorDetached;
    }

    private Rect AnchorRect() => _anchor.TranslatePoint(default, this) is { } p ? new Rect(p, _anchor.Bounds.Size) : default;

    private void OnAnchorLayoutUpdated(object? sender, EventArgs e)
    {
        if (AnchorRect() != _target) InvalidateArrange();
    }

    private void OnAnchorDetached(object? sender, VisualTreeAttachmentEventArgs e) => Close();

    protected override Size ArrangeOverride(Size finalSize)
    {
        _target = AnchorRect();
        Highlight.Width = _target.Width + Pad * 2;
        Highlight.Height = _target.Height + Pad * 2;
        Canvas.SetLeft(Highlight, _target.X - Pad);
        Canvas.SetTop(Highlight, _target.Y - Pad);

        Card.Measure(Size.Infinity);
        Size card = Card.DesiredSize;
        bool fitsBelow = _target.Bottom + Pad + Gap + card.Height <= finalSize.Height - Edge;
        Canvas.SetTop(Card, fitsBelow ? _target.Bottom + Pad + Gap : _target.Y - Pad - Gap - card.Height);
        Canvas.SetLeft(Card, Math.Max(Edge, Math.Min(_target.X, finalSize.Width - Edge - card.Width)));
        return base.ArrangeOverride(finalSize);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _anchor.LayoutUpdated -= OnAnchorLayoutUpdated;
        _anchor.DetachedFromVisualTree -= OnAnchorDetached;
    }

    private void Close() => (Parent as Panel)?.Children.Remove(this);

    private void Dismiss(object? sender, RoutedEventArgs e) => Close();
}
