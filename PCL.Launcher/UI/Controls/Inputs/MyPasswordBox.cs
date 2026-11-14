namespace PCL.UI.Controls;

/// <summary>密码输入框：沿用 MyTextBox 的模板与验证行为，默认隐藏输入字符。</summary>
public class MyPasswordBox : MyTextBox
{
    public MyPasswordBox()
    {
        PasswordChar = '●';
    }
}