using UnoCockpit.Core.Demo;
using UnoCockpit.Core.ViewModels;

namespace UnoCockpit.Views.Apps;

public sealed partial class TripHudView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(TripViewModel), typeof(TripHudView), new PropertyMetadata(null));

    public TripViewModel? ViewModel
    {
        get => (TripViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public TripHudView() => InitializeComponent();
}
