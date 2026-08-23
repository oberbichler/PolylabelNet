![Polylabel C# Logo](https://raw.githubusercontent.com/oberbichler/PolylabelNet/main/images/logo.svg)

# Polylabel C#

A fast, allocation-light C# port of [Mapbox Polylabel](https://github.com/mapbox/polylabel). It finds the **pole of inaccessibility** — the point inside a polygon that is furthest from its outline, and therefore the best place to put a label.

![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple.svg)
![.NET 10.0](https://img.shields.io/badge/.NET-10.0-purple.svg)
![ISC License](https://img.shields.io/badge/License-ISC-blue.svg)

## Key Features

* **Allocation-light:** Nothing is allocated per probe; cells, points and results are all value types. The only heap object is the priority queue with its growth buffer — 696 B to 6,144 B per call on the benchmark data below.
* **Fast:** Roughly 1.5× the throughput of the JavaScript reference on the same fixtures (8.0 ms vs 12.1 ms for `water1`, see [Benchmarks](#benchmarks)).
* **Broad Compatibility:** Targets .NET 8.0 and .NET 10.0 — works in Rhino 8 and other .NET 8 hosts, while also supporting the latest runtime.
* **Flexible Input:** `Point` arrays, GeoJSON-style `double[][][]` coordinates, or your own geometry type via `IPolygon<TPoint>`.
* **Custom Point Types:** Use your own point/vector **struct** without boxing or virtual dispatch; types you cannot modify (`System.Numerics.Vector2`, Unity's `Vector2`) are covered by a small adapter struct.

## Installation

Install the library directly from [NuGet](https://www.nuget.org/):

```bash
dotnet add package Polylabel
```

Or via the Package Manager Console:

```powershell
Install-Package Polylabel
```

### Migrating from 1.x

Version 2.0 renames the entry point from `Polylabel` to `PoleOfInaccessibility` and its method from `Run` to `Find`. In 1.x the class name collided with the `Polylabel` namespace, so `Polylabel.Run(...)` never compiled in consumer code — everyone had to write `Polylabel.Polylabel.Run(...)` or use an alias. The new name removes the collision:

```csharp
// 1.x
var (point, distance) = Polylabel.Polylabel.Run(polygon, precision: 0.01);

// 2.0
using Polylabel;
var (point, distance) = PoleOfInaccessibility.Find(polygon, precision: 0.01);
```

Nothing else changed: `Point`, `Polygon`, `Polygon<TPoint>`, `IPoint`, `IPolygon<TPoint>`, `PolylabelResult`, the overload set, the parameters and the results are all identical.

## Usage

A polygon is modeled as a list of closed rings. The first ring defines the outer boundary, while subsequent optional rings define holes.

![Polygon Structure with Rings and Holes](https://raw.githubusercontent.com/oberbichler/PolylabelNet/main/images/polygon-structure.svg)

```csharp
using Polylabel;

// 1. Define a polygon with an outer ring and two holes (matching the diagram above)
var outerRing = new Point[]
{
    new Point(15, 15),
    new Point(135, 15),
    new Point(135, 135),
    new Point(15, 135),
    new Point(15, 15)
};

var holeA = new Point[]
{
    new Point(85, 35),
    new Point(125, 35),
    new Point(125, 85),
    new Point(85, 35)
};

var holeB = new Point[]
{
    new Point(25, 80),
    new Point(55, 80),
    new Point(55, 125),
    new Point(25, 125),
    new Point(25, 80)
};

var polygon = new Polygon(new Point[][] { outerRing, holeA, holeB });

// 2. Find the pole of inaccessibility
var (point, distance) = PoleOfInaccessibility.Find(polygon, precision: 0.01);

Console.WriteLine($"Optimal label position: X={point.X}, Y={point.Y}"); // Output: X=90.7, Y=99.3
Console.WriteLine($"Distance to closest boundary: {distance}");         // Output: Distance=35.7
```

### Choosing a Precision

The precision is an **absolute length in the coordinate units of your polygon**, not a relative tolerance. It must be clearly smaller than the shorter side of the polygon's bounding box, otherwise the search has nothing left to refine.

The default of `1.0` fits projected coordinates in metres. For geographic coordinates in **degrees** it is far too coarse — a polygon spanning half a degree is smaller than the default precision in its entirety:

```csharp
// WGS84 polygon, roughly 0.7° x 0.56°
PoleOfInaccessibility.Find(polygon);              // precision 1.0: no refinement at all
PoleOfInaccessibility.Find(polygon, 1e-6);        // ~0.1 m at the equator
```

When the precision is at least as large as the shorter bounding box side, the better of the polygon centroid and the bounding box centre is returned, together with its true distance to the outline. That is still a point inside the polygon — just not a refined one. (The reference implementation returns the bounding box corner with a distance of zero here, which is usually a point *outside* the polygon; this port deliberately deviates.)

### Diagnostics

`Find` takes an optional callback that reports what the search did — useful when tuning the precision. Without it the library stays silent; it never writes to the console on its own.

```csharp
PoleOfInaccessibility.Find(polygon, 1.0, Console.WriteLine);

// found best 7.0711 after 4 probes
// found best 11.7115 after 96 probes
// num probes: 99
// best distance: 11.711456063402194
```

If the precision was too coarse to refine anything, the callback says so explicitly:

```
precision 50 is not finer than the shorter bounding box side 10; returning the best initial guess without refinement
num probes: 2
best distance: 5
```

### Input Requirements

The polygon is checked when it is constructed:

| Condition | Exception |
| :--- | :--- |
| The rings container is null | `ArgumentNullException` |
| A ring is null | `ArgumentException` — `Polygon ring 1 is null.` |
| A GeoJSON position is null | `ArgumentException` — `Polygon ring 0, vertex 3 is null.` |
| A GeoJSON position has fewer than two values | `ArgumentException` — `Polygon ring 0, vertex 3 has 1 coordinate values, expected at least 2.` |

Both arguments are checked again before the search starts:

| Condition | Exception |
| :--- | :--- |
| `precision` is zero, negative, NaN or infinite | `ArgumentOutOfRangeException` |
| A coordinate in any ring is NaN or infinite | `ArgumentException` (reports ring and vertex index) |

These inputs are rejected rather than tolerated because they have no meaningful answer: a non-positive precision makes the search's termination condition unsatisfiable, and a non-finite coordinate either does the same to the initial grid or silently poisons the distance function. The check costs roughly 0.06 % of a typical search.

**Empty is not null.** An empty ring, or a polygon without any ring, is a valid degenerate value and yields `(0, 0)` with distance `0` — only `null` is treated as an error. A GeoJSON position may carry a third value (elevation); it is ignored.

### Interoperability

Raw coordinate arrays from a GeoJSON deserialiser are accepted directly. The constructor copies them into `Point` arrays:

```csharp
double[][][] geoJsonCoordinates = ...; // Outer boundary and hole coordinates
var polygon = new Polygon(geoJsonCoordinates);

var (point, distance) = PoleOfInaccessibility.Find(polygon, precision: 0.1);
```

### Custom Types

If your application already has its own point or vector type, you can use it directly. It has to be a **struct** — the generic constraint is `where TPoint : struct, IPoint` — which is what lets the JIT specialise the search for your type instead of dispatching through an interface.

Implement `IPoint` on it and pass it to a generic `Polygon<TPoint>`:

```csharp
using Polylabel;

// 1. Implement IPoint on your own struct
public readonly struct CustomVector2 : IPoint
{
    public double X => XCoordinate;
    public double Y => YCoordinate;

    public double XCoordinate { get; }
    public double YCoordinate { get; }

    public CustomVector2(double x, double y)
    {
        XCoordinate = x;
        YCoordinate = y;
    }
}

// 2. Wrap custom coordinates in a generic Polygon
CustomVector2[][] myRings = ...;
var polygon = new Polygon<CustomVector2>(myRings);

// 3. Find the pole; the JIT compiles a specialised version for CustomVector2
var (point, distance) = PoleOfInaccessibility.Find(polygon, precision: 1.0);
```

If the point type comes from an external package (like `System.Numerics.Vector2` or Unity's `Vector2`) and cannot implement `IPoint` itself, wrap it in an adapter struct. The adapter costs nothing at run time — no boxing, no virtual dispatch, and the property accesses are inlined:

```csharp
// 1. External type from another package (cannot implement IPoint directly)
using System.Numerics; // e.g., Vector2

// 2. Define an adapter struct
public readonly struct Vector2Adapter : IPoint
{
    private readonly Vector2 _vector;

    public double X => _vector.X;
    public double Y => _vector.Y;

    public Vector2Adapter(Vector2 vector) => _vector = vector;
}

// 3. Map your rings. Note that this copies the coordinates into new arrays.
Vector2[][] externalRings = ...;
Vector2Adapter[][] wrappedRings = Array.ConvertAll(externalRings,
    ring => Array.ConvertAll(ring, v => new Vector2Adapter(v)));

var polygon = new Polygon<Vector2Adapter>(wrappedRings);
var (point, distance) = PoleOfInaccessibility.Find(polygon);
```

To avoid that copy, implement `IPolygon<TPoint>` over the data you already have. The rings are consumed as `ReadOnlySpan<TPoint>`, so the search reads your existing memory directly:

```csharp
using Polylabel;

// 1. Define a polygon adapter over your own storage
public readonly struct MyCustomPolygon : IPolygon<Point>
{
    private readonly Point[] _outerRing;

    public int RingCount => 1;

    public ReadOnlySpan<Point> GetRing(int index) => index == 0 ? _outerRing : ReadOnlySpan<Point>.Empty;

    public MyCustomPolygon(Point[] outerRing) => _outerRing = outerRing;
}

// 2. Pass it to Find; the JIT specialises the generic for your type
var polygon = new MyCustomPolygon(outerRingPoints);
var (point, distance) = PoleOfInaccessibility.Find<MyCustomPolygon, Point>(polygon);
```

## Benchmarks

Reproduce with `dotnet run --project Polylabel.Benchmarks -c Release`.

```
BenchmarkDotNet v0.15.8, macOS Tahoe 26.5 (25F71) [Darwin 25.5.0]
Apple M1 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.300, .NET 10.0.8, Arm64 RyuJIT armv8.0-a
```

| Dataset | Polygon | Precision | Mean | Allocated | Resulting Pole |
| :--- | :--- | :--- | ---: | ---: | :--- |
| `water1` | 23 rings, 5,030 vertices | `1.0` | 8.023 ms ± 0.158 | 6,144 B | `[3865.85, 2124.88]` (dist 288.85) |
| `water1` | 23 rings, 5,030 vertices | `50.0` | 5.216 ms ± 0.100 | 3,048 B | `[3854.30, 2123.83]` (dist 278.58) |
| `water2` | 26 rings, 3,735 vertices | `1.0` | 3.685 ms ± 0.060 | 1,488 B | `[3263.50, 3263.50]` (dist 960.50) |
| `water2` | 26 rings, 3,735 vertices | `50.0` | 1.854 ms ± 0.033 | 696 B | `[3272.00, 3272.00]` (dist 952.00) |

The allocations are the priority queue object and its growth buffer — nothing is allocated per probe, and the search loop itself causes no GC pressure.

### Comparison with the JavaScript reference

Same fixtures, same precision, [polylabel](https://github.com/mapbox/polylabel) 2.0.1 on Node v26.1.0, same machine:

| Dataset | Precision | This library | polylabel (JS) |
| :--- | :--- | ---: | ---: |
| `water1` | `1.0` | 8.0 ms | 12.1 ms |
| `water1` | `50.0` | 5.2 ms | 7.3 ms |
| `water2` | `1.0` | 3.7 ms | 5.5 ms |
| `water2` | `50.0` | 1.9 ms | 2.7 ms |

A factor of 1.4 to 1.5. Both implementations run the same algorithm over effectively the same probe sequence, so this is RyuJIT against V8 on identical work, not an algorithmic advantage.

### Native PriorityQueue vs Tinyqueue

The library uses the .NET `PriorityQueue`. For comparison, the benchmark project also contains a C# port of the JavaScript `tinyqueue` used by the original:

| Queue | Dataset | Run 1 | Run 2 | Allocated |
| :--- | :--- | ---: | ---: | ---: |
| .NET `PriorityQueue` | `water1` | 8.124 ms | 8.023 ms | 6,144 B |
| Tinyqueue port | `water1` | 8.241 ms | 8.377 ms | 5,144 B |
| .NET `PriorityQueue` | `water2` | 3.362 ms | 3.685 ms | 1,488 B |
| Tinyqueue port | `water2` | 3.955 ms | 3.262 ms | 2,560 B |

The two are indistinguishable in speed — the ranking on `water2` even flips between runs, so the differences are noise rather than a result. The .NET queue was kept because it is part of the framework.

### Visual Results

Results generated directly from the JSON fixtures. The polygon centroid (blue cross) often falls outside the shape or into a narrow area, while the pole of inaccessibility (red circle, with its maximum inscribed circle) finds the optimal interior point.

![Water1 GIS Dataset Result](https://raw.githubusercontent.com/oberbichler/PolylabelNet/main/images/water1.svg)
![Water2 GIS Dataset Result](https://raw.githubusercontent.com/oberbichler/PolylabelNet/main/images/water2.svg)

## License

This project is licensed under the **ISC License** – see the [LICENSE](LICENSE) file for details. Original algorithm copyright (c) 2016 Mapbox.
