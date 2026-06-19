namespace PCL.Controls.MyMsg.Models;

public class ButtonContext
{
    public required string ButtonName { get; set; }
    public bool ExitWhenClick { get; set; }
    public Action<object> Operation { get; set; }
}