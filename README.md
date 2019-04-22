# Factory Tower Defence

other words

## Terrain

### Design
##### Procedural terrain with gameplay before visual appearance
_Procedurally generate deterministic terrain, with an emphasis on limited/controlled traversal to encourage interesting logistical/tactical situations._

Terrain is broken up by un-pathable cliffs separating different heights. Slopes provide limited/choked access between areas, much like an RTS map.

Terrain generation uses [Worley (Cellular) noise](https://thebookofshaders.com/12/). All noise generation code is based on [FastNoise.cs](https://assetstore.unity.com/packages/tools/particles-effects/fastnoise-70706).

### Worley Cells

Cellular noise (below) is generated using an even grid of points, where a pixel's cell is the closest point in the grid. Each cell has a unique grid index (int2) and unique value noise (float between 0 and 1).
Below, pixels are coloured using their cell value noise - Color(value, value, value). We also see the change in cell shape between the left and right images, as the grid of points is scattered randomly.
<p align="center">
<img src="https://imgur.com/pszR8ED.png">
</p>
<p align="center">
Cellular noise with no scatter (left), some scatter (middle) and high scatter (right), coloured by cell value.
</p>

Image generated using [FastNoise Preview](https://github.com/Auburns/FastNoise/releases).
A more in-depth explanation of worley implementation can be found [here](https://thebookofshaders.com/12/).

---

### Worley Terrain

Cellular noise, like Perlin or Simplex, is deterministic but can be randomised using a seed. It is possible to generate the following (amongst other) information deterministically for any point in world space:

* Cell containing the point: Index, value noise

* Closest adjacent cell to point: Index, value noise

* Distance from the edge of the cell, in the direction of the closest adjacent cell (distance-to-edge)

Index is an int2 and value noise is a float between 0 and 1. Both are unique to each individual cell.

To determine cell height the cell index x and y values are used as the input for a 2D Simplex noise function. The Simplex output chooses the cell height. This causes more natural, gradual height transitions between cells due to the wave shape generated by simplex noise.
<p align="center">
<img src="https://i.imgur.com/0QuGEV6.png">
</p>
<p align="center">
Terrain cells with different heights and connecting slopes
</p>

Scatter can be added. The grid will no longer be distinguishable and some cells may be lost, but each cell still has the same unique int2 index and value noise.
![](https://i.imgur.com/cP8iCSv.gifv)
Animation showing the effect of increase scatter on terrain cells.


---

### Slopes

Cell value noise is used to decide if a slope exists between two neighbouring cells. Using the value of two cells, a third deterministic value can be created to decide which adjacent cell is connected. Each cell owns half the slope.
```csharp
float cellPairValue = (cellValue * adjacentValue);
int2 slopedEdge = GetSlopeConnectionBasedOnValue(cellPairValue);
```
The connection is expressed as an int2 describing the direction of the connected adjacent cell (e.g. right adjacent cell: int2(1, 0) ). For the cell with the lowest value of the two, this int2 is 'flipped' to describe the direction of the opposite adjacent cell (e.g. int2(-1, 0)). This results in the two cells both having a slope on opposite sides and so being connected. Each cell can be generated independently of any adjacent cells.
<p align="center">
<img src="https://imgur.com/VJBkFBq.png">
</p>
<p align="center">
Slopes generated using Cellular distance-to-edge noise.
</p>

The distance-to-edge value will equal ~0 close to the edge of the cell. Combined with information about the current and adjacent cells, it can be used to blend height between two cell heights. The code below slopes the terrain from the cell height to the mid point between the two cells.
```csharp
float slopeLength = 0.5f;

float halfWayHeight = (point.cellHeight + point.adjacentHeight) / 2;
float interpolator = math.unlerp(0, slopeLength, point.distanceToEdge);

float terrainHeight = math.lerp(halfWayHeight, cellHeight, interpolator);
```
<p align="center">
<img src="https://imgur.com/McWVde3.png">
</p>
<p align="center">
Distance to edge noise visualised using FastNoise Preview.
</p>

### Cell groups





