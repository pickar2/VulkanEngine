using SimpleMath.Vectors;

var vec3F = new Vector3<float>(5, 1, 3);

Console.WriteLine(vec3F * 4.0);
Console.WriteLine(vec3F.Add(3L).Sub(0f, -.5f, 1f).Add(vec3F).Add((-2.1, 3.1, 0d)));
Console.WriteLine(vec3F * 2L);
Console.WriteLine((Vector3<long>) vec3F * 5.3);

var vec4D = new Vector4<double>(2.4, 5.1, 0, 0);
var otherVec4D = new Vector4<double>(0.5);
var testVec4D = new Vector4<double>(100);

Console.WriteLine(vec4D - (2d, 3d, 1, 1) + otherVec4D);
Console.WriteLine(otherVec4D);
Console.WriteLine(vec4D.Set(otherVec4D).Set((5, 5, 5, 5)).Set(0.4, 0.1, 0.4, 0.1).Set(0.2).Put(ref otherVec4D));
Console.WriteLine(testVec4D);
Console.WriteLine(vec4D.Ceil(ref testVec4D));
