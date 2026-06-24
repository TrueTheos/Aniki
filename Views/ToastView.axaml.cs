using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Aniki.Views;

public partial class ToastView : UserControl
{
    public ToastView(string message)
    {
        InitializeComponent();
        ToastText.Text = message;
    }
}