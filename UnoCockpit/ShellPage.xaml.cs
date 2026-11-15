using UnoCockpit.Core.ViewModels;
using UnoCockpit.Services;

namespace UnoCockpit;

public sealed partial class ShellPage : Page
{
    public ShellPage(ShellViewModel viewModel, Sources sources)
    {
        ViewModel = viewModel;
        Sources = sources;
        InitializeComponent();
    }

    public ShellViewModel ViewModel { get; }

    public Sources Sources { get; }
}
