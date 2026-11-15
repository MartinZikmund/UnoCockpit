namespace UnoCockpit.Core.Game;

/// <summary>
/// "Charge Run": steer a car down a scrolling lot, grab charge bolts, dodge cones.
/// The battery drains over time; the run ends when it's empty.
/// </summary>
public sealed class ChargeRunGame(int seed)
{
    public const double CarY = 0.85;
    public const double HitRadius = 0.06;
    public const double RoadMinX = 0.08;
    public const double RoadMaxX = 0.92;
    public const double SteerSpeedPerSecond = 1.2;
    public const double DrainPerSecond = 3;
    public const double BoltCharge = 20;
    public const double ConePenalty = 25;
    public const double ScrollPerSecond = 0.35;
    public const double SpawnIntervalSeconds = 0.6;
    public const double BoltChance = 0.35;
    public const int BoltScore = 50;
    public static readonly TimeSpan InvulnerableFor = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan SpeedStep = TimeSpan.FromSeconds(10);

    private readonly List<Entity> _entities = new();
    private Random _random = new(seed);
    private TimeSpan _elapsed;
    private TimeSpan _invulnerableUntil;
    private double _spawnTimer;
    private double _distance;
    private int _bolts;
    private int _runs;

    public double CarX { get; private set; } = 0.5;

    public double Battery { get; private set; } = 100;

    public int Score => (int)_distance + _bolts * BoltScore;

    public bool IsRunning { get; private set; }

    public bool IsOver { get; private set; }

    public bool IsInvulnerable => _elapsed < _invulnerableUntil;

    public double SpeedFactor => 1 + 0.02 * (_elapsed.Ticks / SpeedStep.Ticks);

    public IReadOnlyList<Entity> Entities => _entities;

    internal bool SpawningEnabled { get; set; } = true;

    public event EventHandler<EntityKind>? Collected;

    public void Update(TimeSpan dt, GameInput input)
    {
        if (!IsRunning)
        {
            if (input.Start)
            {
                Reset();
                IsRunning = true;
            }

            return;
        }

        _elapsed += dt;
        double s = dt.TotalSeconds;
        double speed = SpeedFactor;

        CarX = Math.Clamp(CarX + Math.Clamp(input.Steer, -1, 1) * SteerSpeedPerSecond * s, RoadMinX, RoadMaxX);
        Battery -= DrainPerSecond * s;
        _distance += 10 * speed * s;

        for (int i = _entities.Count - 1; i >= 0; i--)
        {
            Entity e = _entities[i] with { Y = _entities[i].Y + ScrollPerSecond * speed * s };
            if (e.Y > 1.1)
            {
                _entities.RemoveAt(i);
            }
            else if (Math.Abs(e.X - CarX) < HitRadius && Math.Abs(e.Y - CarY) < HitRadius)
            {
                _entities.RemoveAt(i);
                Hit(e.Kind);
            }
            else
            {
                _entities[i] = e;
            }
        }

        if (SpawningEnabled)
        {
            _spawnTimer += s * speed;
            while (_spawnTimer >= SpawnIntervalSeconds)
            {
                _spawnTimer -= SpawnIntervalSeconds;
                EntityKind kind = _random.NextDouble() < BoltChance ? EntityKind.Bolt : EntityKind.Cone;
                _entities.Add(new(kind, RoadMinX + _random.NextDouble() * (RoadMaxX - RoadMinX), -0.05));
            }
        }

        if (Battery <= 0)
        {
            Battery = 0;
            IsRunning = false;
            IsOver = true;
        }
    }

    internal void Spawn(Entity e) => _entities.Add(e);

    private void Hit(EntityKind kind)
    {
        if (kind == EntityKind.Bolt)
        {
            Battery = Math.Min(100, Battery + BoltCharge);
            _bolts++;
        }
        else if (IsInvulnerable)
        {
            return;
        }
        else
        {
            Battery -= ConePenalty;
            _invulnerableUntil = _elapsed + InvulnerableFor;
        }

        Collected?.Invoke(this, kind);
    }

    private void Reset()
    {
        _entities.Clear();
        _random = new(seed + _runs++);
        _elapsed = TimeSpan.Zero;
        _invulnerableUntil = TimeSpan.Zero;
        _spawnTimer = 0;
        _distance = 0;
        _bolts = 0;
        CarX = 0.5;
        Battery = 100;
        IsOver = false;
    }
}
