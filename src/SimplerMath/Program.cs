using SimplerMath;

var vec3F = new Vector3<float>(4);
var vec3FOther = new Vector3<float>(1, 2, 3);

Console.WriteLine($"{vec3F.Add(vec3FOther)}");

var vec3D = new Vector3<double>(5, 6, 7);

Console.WriteLine($"{vec3FOther.Add(vec3D.As<float>())}");

Console.WriteLine($"{vec3FOther}");
vec3FOther[1] = Single.E;
vec3FOther[2] = 0;
Console.WriteLine($"{vec3FOther}");

Console.WriteLine($"{vec3F}");
vec3F.MMin(-1, Single.MaxValue, Single.MaxValue).MMul(2).MAdd(0.5f, -0.6f, 0.7f).MRound().MNegate();
Console.WriteLine($"{vec3F}");

var span = vec3F.AsSpan();
span[0] = Single.Pi;

Console.WriteLine($"{vec3F}");
