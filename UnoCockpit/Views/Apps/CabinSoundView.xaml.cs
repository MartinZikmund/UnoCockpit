using UnoCockpit.Core.Demo;
using UnoCockpit.Core.ViewModels;

namespace UnoCockpit.Views.Apps;

public sealed partial class CabinSoundView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(CabinSoundViewModel), typeof(CabinSoundView), new PropertyMetadata(null));

    public CabinSoundViewModel? ViewModel
    {
        get => (CabinSoundViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public CabinSoundView() => InitializeComponent();
}
