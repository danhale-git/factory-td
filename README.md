# Factory Tower Defence

other words

## Terrain

Terrain generation is based on [Worley (Cellular) noise](https://thebookofshaders.com/12/). Worley noise generation is based on [FastNoise.cs](https://assetstore.unity.com/packages/tools/particles-effects/fastnoise-70706).

Worley noise (below) is based on a straight grid which can be scattered to create more natural shapes.
<p align="center">
<img src="https://imgur.com/pszR8ED.png">
</p>
<p align="center">
Worley noise with no scatter, some scatter and high scatter
(generated using [FastNoise Preview](https://github.com/Auburns/FastNoise/releases))
</p>

---

Worley noise, like Perlin/Simplex is deterministic but can be randomised using a seed. It is possible to generate the following information deterministically about worley for any point in world space:
* Cell the point is in
* * Sub thing

* Closest adjacent cell
* Distance from the edge of the cell, in the direction of the closest adjacent cell


<p align="center">
<img src="https://i.imgur.com/0QuGEV6.png">
</p>
<p align="center">
Terrain generation with no scatter
</p>