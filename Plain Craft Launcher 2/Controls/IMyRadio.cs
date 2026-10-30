namespace PCL;

public interface IMyRadio
{
    delegate void ChangedEventHandler(object sender, Common.RouteEventArgs e);

    delegate void CheckEventHandler(object sender, Common.RouteEventArgs e);

    event CheckEventHandler Check;
    event ChangedEventHandler Changed;
}