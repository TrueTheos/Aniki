using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

internal sealed partial class ToastView : UserControl
{
    public ToastView(string message, Action? onClick = null)
    {
        InitializeComponent();
        ToastText.Text = message;
        if (onClick == null) return;

        ToastBorder.Cursor = new Cursor(StandardCursorType.Hand);
        ToastBorder.PointerPressed += (_, _) =>
        {
            (Parent as Panel)?.Children.Remove(this);
            onClick();
        };
    }
}