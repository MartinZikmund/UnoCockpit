using UnoCockpit.Core.Demo;
using UnoCockpit.Core.ViewModels;

namespace UnoCockpit.Views.Apps;

public sealed partial class PhotoBoothView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(PhotoBoothViewModel), typeof(PhotoBoothView), new PropertyMetadata(null));

    public PhotoBoothViewModel? ViewModel
    {
        get => (PhotoBoothViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public PhotoBoothView() => InitializeComponent();
}
