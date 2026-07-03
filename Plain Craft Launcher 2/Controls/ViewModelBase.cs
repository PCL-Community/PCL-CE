using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PCL.Controls;

public class ViewModelBase: INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void SetProperty<T>(T newValue, ref T value, [CallerMemberName] string propertyName = "")
    {
        value = newValue;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
