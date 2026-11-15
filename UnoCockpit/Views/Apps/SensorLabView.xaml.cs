using UnoCockpit.Core.Demo;
using UnoCockpit.Core.ViewModels;

namespace UnoCockpit.Views.Apps;

public sealed partial class SensorLabView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(SensorLabViewModel), typeof(SensorLabView), new PropertyMetadata(null));

    public SensorLabViewModel? ViewModel
    {
        get => (SensorLabViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public SensorLabView() => InitializeComponent();
}
