using SimpleMath.Vectors;

var vec3F = new Vector3<float>(5, 1, 3);

Console.WriteLine((vec3F * 4.0).ToString());
Console.WriteLine(vec3F.Add(3L).Sub(0f, -.5f, 1f).Add(vec3F).Add((-2.1, 3.1, 0d)).ToString());
Console.WriteLine((vec3F * 2L).ToString());
Console.WriteLine(((Vector3<long>) vec3F * 5.3).ToString());

var vec4D = new Vector4<double>(2.4, 5.1, 0, 0);
var otherVec4D = new Vector4<double>(0.5);
var testVec4D = new Vector4<double>(100);

Console.WriteLine((vec4D - (2d, 3d, 1, 1) + otherVec4D).ToString());
Console.WriteLine(otherVec4D);
Console.WriteLine(vec4D.Set(otherVec4D).Set((5, 5, 5, 5)).Set(0.4, 0.1, 0.4, 0.1).Set(0.2).Put(ref otherVec4D).ToString());
Console.WriteLine(testVec4D);
Console.WriteLine(vec4D.Ceil(ref testVec4D).ToString());

var vec2 = new Vector2<float>(2, 4);
var dot = vec2.Dot(vec2);