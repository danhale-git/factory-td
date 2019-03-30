using Unity.Mathematics;
using Unity.Collections;

public struct SimplexNoiseGenerator
{
    GRAD_2D GRAD_2D;

    int X_PRIME;
    int Y_PRIME;

    int seed;
    float frequency;

    public SimplexNoiseGenerator(int seed, float frequency)
    {
        this.seed = seed;
        this.frequency = frequency;

        GRAD_2D = new GRAD_2D();

        X_PRIME = 1619;
        Y_PRIME = 31337;
    }

    int FastFloor(float f) { return (f >= 0 ? (int)f : (int)f - 1); }

    float GradCoord2D(int seed, int x, int y, float xd, float yd)
    {
        int hash = seed;
        hash ^= X_PRIME * x;
        hash ^= Y_PRIME * y;

        hash = hash * hash * hash * 60493;
        hash = (hash >> 13) ^ hash;

        float2 g = GRAD_2D[hash & 7];

        return xd * g.x + yd * g.y;
    }
        
    public float GetSimplex(float x, float y)
    {
        return SingleSimplex(seed, x * frequency, y * frequency);
    }

    const float F2 = (float)(1.0 / 2.0);
    const float G2 = (float)(1.0 / 4.0);

    float SingleSimplex(int seed, float x, float y)
    {
        float t = (x + y) * F2;
        int i = FastFloor(x + t);
        int j = FastFloor(y + t);

        t = (i + j) * G2;
        float X0 = i - t;
        float Y0 = j - t;

        float x0 = x - X0;
        float y0 = y - Y0;

        int i1, j1;
        if (x0 > y0)
        {
            i1 = 1; j1 = 0;
        }
        else
        {
            i1 = 0; j1 = 1;
        }

        float x1 = x0 - i1 + G2;
        float y1 = y0 - j1 + G2;
        float x2 = x0 - 1 + F2;
        float y2 = y0 - 1 + F2;

        float n0, n1, n2;

        t = (float)0.5 - x0 * x0 - y0 * y0;
        if (t < 0) n0 = 0;
        else
        {
            t *= t;
            n0 = t * t * GradCoord2D(seed, i, j, x0, y0);
        }

        t = (float)0.5 - x1 * x1 - y1 * y1;
        if (t < 0) n1 = 0;
        else
        {
            t *= t;
            n1 = t * t * GradCoord2D(seed, i + i1, j + j1, x1, y1);
        }

        t = (float)0.5 - x2 * x2 - y2 * y2;
        if (t < 0) n2 = 0;
        else
        {
            t *= t;
            n2 = t * t * GradCoord2D(seed, i + 1, j + 1, x2, y2);
        }

        return To01(50 * (n0 + n1 + n2));
    }

    float To01(float value)
	{
		return (value * 0.5f) + 0.5f;
	}
}