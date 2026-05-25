<p align="center">
  <svg width="150" height="150" viewBox="0 0 100 100" fill="none" xmlns="http://www.w3.org/2000/svg">
    <!-- Beautiful geometric concave polygon representing GIS mapping -->
    <path d="M15 45L40 15L85 30L55 50L75 75L35 85L15 45Z" fill="#1b1f23" fill-opacity="0.05" stroke="#087F5B" stroke-width="3" stroke-linejoin="round"/>
    <path d="M15 45L40 15L85 30L55 50L75 75L35 85L15 45Z" stroke="#087F5B" stroke-width="3" stroke-linejoin="round" stroke-dasharray="1 3"/>
    <!-- The Pole of Inaccessibility (optimal placement) -->
    <circle cx="38" cy="48" r="4" fill="#E03131"/>
    <circle cx="38" cy="48" r="17" stroke="#E03131" stroke-width="1.5" stroke-dasharray="2 2"/>
    <!-- Concentric grid lines indicating search cells -->
    <rect x="28" y="38" width="20" height="20" stroke="#228BE6" stroke-width="0.75" stroke-dasharray="1 1"/>
    <rect x="18" y="28" width="40" height="40" stroke="#228BE6" stroke-width="0.5" stroke-dasharray="1 1"/>
  </svg>
</p>

<h1 align="center">Polylabel C#</h1>

<p align="center">
  A blazing fast, zero-allocation C# port of <a href="https://github.com/mapbox/polylabel">Mapbox Polylabel</a>.
  Finds the <b>pole of inaccessibility</b> (the optimal point inside a polygon for label placement) with extreme speed and surgical precision.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-purple.svg" alt=".NET 10.0 ready">
  <img src="https://img.shields.io/badge/Lizenz-ISC-blue.svg" alt="ISC License">
</p>

## Key Features

* **Zero Heap Allocations:** All core data structures use stack-allocated value types to eliminate garbage collection pressure.
* **Peak Performance:** Leverages native priority queues and span slices for ultra-fast execution.
* **Modern .NET 10 Ready:** Fully optimized to take advantage of the latest RyuJIT compiler and JIT features.
* **Flexible API:** Native support for both high-performance double arrays and standard GeoJSON coordinate structures.

## Installation

Install the library directly from [NuGet](https://www.nuget.org/):

```bash
dotnet add package Polylabel
```

Or via the Package Manager Console:

```powershell
Install-Package Polylabel
```

## Usage

```csharp
using Polylabel;

// 1. Define a polygon with an outer ring and optional holes
var outerRing = new Point[]
{
    new Point(0, 0),
    new Point(100, 0),
    new Point(100, 100),
    new Point(0, 100),
    new Point(0, 0)
};

var polygon = new Polygon(new Point[][] { outerRing });

// 2. Find the pole of inaccessibility
var (point, distance) = Polylabel.Run(polygon, precision: 1.0);

Console.WriteLine($"Optimal label position: X={point.X}, Y={point.Y}");
Console.WriteLine($"Distance to closest boundary: {distance}");
```

### Interoperability (GeoJSON / Nested Coordinates)

Polylabel easily handles raw coordinate arrays directly from GeoJSON serializers:

```csharp
double[][][] geoJsonCoordinates = ...; // Outer boundary and hole coordinates
var polygon = new Polygon(geoJsonCoordinates);

var (point, distance) = Polylabel.Run(polygon, precision: 0.1);
```

## Benchmarks

Executed on an **Apple M1 Pro** under **.NET 10.0**:

| Benchmark Case | Polygon Complexity | Search Precision | Mean Execution Time | Allocated Memory | Calculated Pole (Result) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`Water1` (GIS Dataset)** | 25 Rings, 3,073 Vertices | `1.0` | **8.17 ms** | **6.00 KB** | `[3865.85, 2124.88]` (dist: 288.85) |
| **`Water1` (Quick Search)**| 25 Rings, 3,073 Vertices | `50.0` | **5.29 ms** | **2.98 KB** | `[3854.30, 2123.83]` (dist: 278.58) |
| **`Water2` (GIS Dataset)** | 28 Rings, 2,831 Vertices | `1.0` | **3.08 ms** | **1.45 KB** | `[3263.50, 3263.50]` (dist: 960.50) |

*Note: The minimal memory allocated is solely for the initial creation of the priority queue object wrapper and its internal resize buffer. The main search loop operates entirely on the stack and incurs **zero garbage collection pauses**.*

### Visual Results

Below are the actual results of our test and benchmark datasets generated directly from the JSON fixtures. 
Notice how the Polygon Centroid (blue cross) often falls outside the shape or in suboptimal narrow areas, whereas the Pole of Inaccessibility (red circle and its concentric maximum distance circle) finds the absolute optimal center point with millisecond speed.

<p align="center">
  <img src="images/water1.svg" width="380" alt="Water1 GIS Dataset Result">
  <img src="images/water2.svg" width="380" alt="Water2 GIS Dataset Result">
</p>

## License

This project is licensed under the **ISC License** – see the [LICENSE](LICENSE) file for details. Original algorithm copyright (c) 2016 Mapbox.
