# Factory Tower Defence

other words

## Terrain

### Design
_The intention is to create procedurally generated deterministic terrain, with an emphasis on gameplay and limited/controlled traversal. Terrain should be broken up by cliffs with sloped providing limited/choked access between areas._

Terrain generation is based on [Worley (Cellular) noise](https://thebookofshaders.com/12/). Worley noise generation is based on [FastNoise.cs](https://assetstore.unity.com/packages/tools/particles-effects/fastnoise-70706).

### Cellular noise terrain generation

Cellular noise (below) is based on an even grid which can be scattered to create more natural shapes. Each cell has with a unique index (int2) in the grid.
<p align="center">
<img src="https://imgur.com/pszR8ED.png">
</p>
<p align="center">
Cellular noise with no scatter, some scatter and high scatter
(generated using [FastNoise Preview](https://github.com/Auburns/FastNoise/releases))
</p>

Terrain cell height is determined by using the cell index values to generate 2D Simplex noise. This causes more natural, gradual height transitions between cells.
<p align="center">
<img src="https://i.imgur.com/0QuGEV6.png">
</p>
<p align="center">
Terrain generation with no scatter
</p>

---

Cellular noise, like Perlin/Simplex is deterministic but can be randomised using a seed. It is possible to generate the following information deterministically about worley for any point in world space:

* Cell the point is inside info:
Index
Value
Position

* Closest adjacent cell info:
Index
Value
Position

* Distance from the edge of the cell, in the direction of the closest adjacent cell (distance-to-edge)

Cell value noise is used to decide if a slope exists. It is a float between 0 and 1 that is unique per cell. Using the value of two neighbouring cells, a third consistent value can be created to decide if a slope connects them.
This allows sloped to be generated deterministically with each half of the slope owned by a different cell and both cells generated independently.
```csharp
float cellPairValue = (cellValue * adjacentValue);
bool slope = CheckIfSlopedBasedOnValue(cellPairValue);
```
<p align="center">
<img src="https://imgur.com/VJBkFBq.png">
</p>
<p align="center">
Slopes generated using Cellular distance-to-edge noise.
</p>

---

distance-to-edge can be used to blend height between cells, where the value 0 is the edge of the current cell. The code below sloped the terrain from the cell height, to half way between it's and the adjacent cell's height.
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





