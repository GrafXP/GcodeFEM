namespace GcodeFem.Core.Geometry;

/// <summary>Eigenvalues and eigenvectors of a small symmetric matrix, by Jacobi rotations.</summary>
static class SymmetricEigen
{
    /// <summary>
    /// <paramref name="a"/> is n × n, row-major, and is overwritten. Returns the eigenvalues in
    /// ascending order and, in the same order, their unit eigenvectors.
    /// </summary>
    public static (double[] Values, double[][] Vectors) Solve(double[] a, int n)
    {
        var v = new double[n * n];
        for (var i = 0; i < n; i++) v[i * n + i] = 1;
        var norm = a.Sum(x => x * x);

        for (var sweep = 0; sweep < 100; sweep++)
        {
            double off = 0;
            for (var p = 0; p < n; p++)
            for (var q = p + 1; q < n; q++)
                off += a[p * n + q] * a[p * n + q];
            if (off <= 1e-30 * norm) break;

            for (var p = 0; p < n; p++)
            for (var q = p + 1; q < n; q++)
            {
                var apq = a[p * n + q];
                if (apq == 0) continue;
                var theta = (a[q * n + q] - a[p * n + p]) / (2 * apq);
                var t = theta == 0 ? 1 : Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                var c = 1 / Math.Sqrt(t * t + 1);
                var s = t * c;
                for (var k = 0; k < n; k++)
                {
                    var (kp, kq) = (a[k * n + p], a[k * n + q]);
                    (a[k * n + p], a[k * n + q]) = (c * kp - s * kq, s * kp + c * kq);
                }
                for (var k = 0; k < n; k++)
                {
                    var (pk, qk) = (a[p * n + k], a[q * n + k]);
                    (a[p * n + k], a[q * n + k]) = (c * pk - s * qk, s * pk + c * qk);
                }
                for (var k = 0; k < n; k++)
                {
                    var (kp, kq) = (v[k * n + p], v[k * n + q]);
                    (v[k * n + p], v[k * n + q]) = (c * kp - s * kq, s * kp + c * kq);
                }
            }
        }

        var order = Enumerable.Range(0, n).OrderBy(i => a[i * n + i]).ToArray();
        return ([.. order.Select(i => a[i * n + i])], [.. order.Select(i => Enumerable.Range(0, n).Select(k => v[k * n + i]).ToArray())]);
    }
}
