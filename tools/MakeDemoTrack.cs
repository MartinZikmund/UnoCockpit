// Generates UnoCockpit/Assets/demo-track.json: a synthetic ~4 minute drive that ends parked.
// Run: dotnet run tools/MakeDemoTrack.cs
using System.Globalization;
using System.Text;

const double StartLat = 37.7694;   // near Golden Gate Park, San Francisco: a public, neutral spot
const double StartLon = -122.4862;
const double StartAlt = 60;
const double Accel = 2.5;          // m/s², gentle
const double EarthRadius = 6_371_008.8;

// (seconds, target speed m/s, turn rate deg/s)
(double Duration, double Speed, double Turn)[] legs =
[
    (8, 0, 0),
    (30, 14, 0),
    (10, 12, 9),
    (40, 20, 0),
    (15, 12, -6),
    (30, 8, 9),        // roundabout, 270°
    (35, 16, 0),
    (20, 10, -4.5),
    (15, 0, 0),
    (20, 0, 0),
];

double lat = StartLat, lon = StartLon, alt = StartAlt, speed = 0, heading = 75, t = 0;
StringBuilder sb = new("{\"points\":[\n");
void Emit() => sb.Append(string.Create(CultureInfo.InvariantCulture,
    $"  {{\"t\":{t:0},\"lat\":{lat:0.0000000},\"lon\":{lon:0.0000000},\"alt\":{alt:0.0},\"speed\":{speed:0.00},\"heading\":{heading:0.0}}},\n"));

Emit();
foreach ((double duration, double target, double turn) in legs)
{
    for (int s = 0; s < duration; s++)
    {
        speed = speed < target ? Math.Min(target, speed + Accel) : Math.Max(target, speed - Accel);
        if (speed > 0.5)
        {
            heading = ((heading + turn) % 360 + 360) % 360;
        }

        double h = heading * Math.PI / 180;
        lat += speed * Math.Cos(h) / EarthRadius * 180 / Math.PI;
        lon += speed * Math.Sin(h) / (EarthRadius * Math.Cos(lat * Math.PI / 180)) * 180 / Math.PI;
        alt += Math.Sin(t / 20) * 0.3;
        t++;
        Emit();
    }
}

sb.Length -= 2;
sb.Append("\n]}\n");
string path = Path.Combine(AppContext.GetData("EntryPointFileDirectoryPath") as string ?? ".", "..", "UnoCockpit", "Assets", "demo-track.json");
File.WriteAllText(path, sb.ToString());
Console.WriteLine($"{t} s → {Path.GetFullPath(path)}");
