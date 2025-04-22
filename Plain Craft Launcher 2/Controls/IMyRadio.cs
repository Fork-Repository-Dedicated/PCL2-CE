
namespace PCL
{
    public interface IMyRadio
    {
        event CheckEventHandler Check;

        delegate void CheckEventHandler(object sender, ModBase.RouteEventArgs e);
        event ChangedEventHandler Changed;

        delegate void ChangedEventHandler(object sender, ModBase.RouteEventArgs e);
    }
}