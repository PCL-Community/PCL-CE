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
        get => TextBox.HintText;
        set => TextBox.HintText = value;
    }

    public string Text
    {
        get => TextBox.Text;
        set => TextBox.Text = value;
    }

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
        if (string.IsNullOrEmpty(TextBox.Text))
        {
            ModAnimation.AniStart(ModAnimation.AaOpacity(BtnClear, -BtnClear.Opacity, 90),
                "MySearchBox ClearBtn " + Uuid);
            BtnClear.IsHitTestVisible = false;
        }
        else
        {
            ModAnimation.AniStart(ModAnimation.AaOpacity(BtnClear, 1d - BtnClear.Opacity, 90),
                "MySearchBox ClearBtn " + Uuid);
            BtnClear.IsHitTestVisible = true;
        }

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
}