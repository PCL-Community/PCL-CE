namespace PCL;

public interface IMyRadio
{
    delegate void ChangedEventHandler(object sender, ModBase.RouteEventArgs e);

    delegate void CheckEventHandler(object sender, ModBase.RouteEventArgs e);

    event CheckEventHandler Check;
    event ChangedEventHandler changed;
}