namespace PCL;

public interface IMyRadio
{
    delegate void ChangedEventHandler(object sender, RouteEventArgs e);

    delegate void CheckEventHandler(object sender, RouteEventArgs e);

    event CheckEventHandler Check;
    event ChangedEventHandler Changed;
}