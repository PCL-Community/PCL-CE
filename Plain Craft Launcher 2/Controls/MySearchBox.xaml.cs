using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public partial class MySearchBox : MyCard
{
    public delegate void SearchEventHandler(object sender, EventArgs e);

    public delegate void TextChangedEventHandler(object sender, EventArgs e);

    public MySearchBox()
    {
        InitializeComponent();

        Loaded += MySearchBox_Loaded;
    }
    
    private void MySearchBox_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ModMain.RaiseCustomEvent(this);
    }
    
    // 属性
    public string HintText
    {
        get => (string)GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    public static readonly DependencyProperty HintTextProperty =
        DependencyProperty.Register("HintText", typeof(string), typeof(MySearchBox),
            new PropertyMetadata(string.Empty, (d, e) => ((MySearchBox)d).TextBox.HintText = (string)e.NewValue));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register("Text", typeof(string), typeof(MySearchBox),
            new PropertyMetadata(string.Empty, (d, e) => ((MySearchBox)d).TextBox.Text = (string)e.NewValue));

    public Visibility SearchButtonVisibility
    {
        get => BtnSearch.Visibility;
        set
        {
            BtnClear.Margin = new Thickness(0d, 0d, value == Visibility.Visible ? 70 : 10, 0d);
            BtnSearch.Visibility = value;
        }
    }

    public event TextChangedEventHandler? TextChanged;

    private void MySearchBox_Loaded(object sender, RoutedEventArgs e)
    {
        TextBox.Focus();
    }

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

    public event SearchEventHandler? Search;

    private void BtnSearch_Click(object sender, MouseButtonEventArgs e)
    {
        Search?.Invoke(sender, e);
    }

    private void UpdateClearButtonState()
    {
        var hasText = !string.IsNullOrEmpty(TextBox.Text);
        ModAnimation.AniStart(ModAnimation.AaOpacity(BtnClear, hasText ? 1d - BtnClear.Opacity : -BtnClear.Opacity, 90),
            "MySearchBox ClearBtn " + Uuid);
        BtnClear.IsHitTestVisible = hasText;
    }
}
