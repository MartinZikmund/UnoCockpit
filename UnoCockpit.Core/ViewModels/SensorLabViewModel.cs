using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnoCockpit.Core.Sensors;

namespace UnoCockpit.Core.ViewModels;

public sealed partial class SensorLabViewModel(ICapabilityProbe probe) : ObservableObject
{
    public ObservableCollection<CapabilityReport> Reports { get; } = new();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IReadOnlyList<CapabilityReport> reports = await probe.ProbeAsync();
        Reports.Clear();
        foreach (CapabilityReport r in reports)
        {
            Reports.Add(r);
        }
    }

    public void Update(string id, CapabilityStatus status, string detail)
    {
        for (int i = 0; i < Reports.Count; i++)
        {
            if (Reports[i].Id == id)
            {
                if (Reports[i].Status != status || Reports[i].Detail != detail)
                {
                    Reports[i] = Reports[i] with { Status = status, Detail = detail };
                }

                return;
            }
        }
    }

    public string ToPlainText()
    {
        StringBuilder sb = new("Uno Cockpit sensor report\n");
        foreach (CapabilityReport r in Reports)
        {
            sb.Append(r.Name).Append(": ").Append(r.Status).Append(" (").Append(r.Detail).Append(")\n");
        }

        return sb.ToString();
    }
}
