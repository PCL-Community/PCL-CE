using System.Windows;
using System.Windows.Controls;

namespace PCL;

public partial class MySearchBar : MyCard
{
    public delegate void TextChangedEventHandler(object sender, EventArgs e);

    public MySearchBar()
    {
        InitializeComponent();
    }

    public string HintText
    {
        get => (string)GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    public static readonly DependencyProperty HintTextProperty =
        DependencyProperty.Register("HintText", typeof(string), typeof(MySearchBar),
            new PropertyMetadata(string.Empty, (d, e) => ((MySearchBar)d).TextBox.HintText = (string)e.NewValue));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register("Text", typeof(string), typeof(MySearchBar),
            new PropertyMetadata(string.Empty, (d, e) => ((MySearchBar)d).TextBox.Text = (string)e.NewValue));

    public event TextChangedEventHandler? TextChanged;

    private void Text_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateClearButtonState();
        TextChanged?.Invoke(sender, e);
    }

    private void BtnClear_Click(object sender, EventArgs e)
    {
        TextBox.Text = "";
        TextBox.Focus();
    }

    private void UpdateClearButtonState()
    {
        var hasText = !string.IsNullOrEmpty(TextBox.Text);
        ModAnimation.AniStart(ModAnimation.AaOpacity(BtnClear, hasText ? 1d - BtnClear.Opacity : -BtnClear.Opacity, 90),
            "MySearchBar ClearBtn " + GetHashCode());
        BtnClear.IsHitTestVisible = hasText;
    }
}