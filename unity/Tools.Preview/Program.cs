using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;

// Exports every procedural model as JSON for the browser previewer.
var outDir = args.Length > 0 ? args[0] : "preview-out";
Directory.CreateDirectory(outDir);
var models = new List<(string name, MeshData mesh)>();
foreach (var l in JeepneyCatalog.All)
{
    var full = JeepneyBuilder.Build(l);
    var wheel = JeepneyBuilder.BuildWheel(null, l.Chrome);
    foreach (var wp in JeepneyBuilder.WheelPositions) MeshOps.Append(full, wheel, wp);
    models.Add(("jeepney_" + l.Id, full));
}
models.Add(("wheel", JeepneyBuilder.BuildWheel()));
foreach (var (name, mesh) in ModelCatalog.Preview()) models.Add((name, mesh));
{
    // jeepney with passengers seated inside (rotation baked: left bench faces +X, right faces -X)
    var full = JeepneyBuilder.Build(JeepneyCatalog.Get("sanjuan"));
    foreach (var wp in JeepneyBuilder.WheelPositions) MeshOps.Append(full, JeepneyBuilder.BuildWheel(), wp);
    for (int i = 0; i < 8; i++)
    {
        var seat = JeepneyBuilder.Seats[i];
        var mb = new MeshBuilder();
        mb.Push(seat, new Rot(0, seat.X < 0 ? (float)(Math.PI / 2) : -(float)(Math.PI / 2), 0));
        PropBuilder.AddPerson(mb, i, true);
        mb.Pop();
        MeshOps.Append(full, mb.Data, V3.Zero);
    }
    models.Add(("jeepney_pax", full));
}

string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
var sb = new StringBuilder();
sb.Append("{\"palette\":[");
var pal = Palette.Shared.Colors;
for (int i = 0; i < pal.Count; i++) sb.Append(i > 0 ? "," : "").Append($"[{pal[i].r},{pal[i].g},{pal[i].b}]");
sb.Append("],\"models\":{");
for (int m = 0; m < models.Count; m++)
{
    var (name, d) = models[m];
    var (min, max) = d.Bounds();
    Console.WriteLine($"{name}: {d.Positions.Count} verts, {d.TriangleCount} tris, submeshes [{string.Join(",", d.SubmeshKeys)}], size {F(max.X - min.X)} x {F(max.Y - min.Y)} x {F(max.Z - min.Z)}");
    sb.Append(m > 0 ? "," : "").Append($"\"{name}\":{{\"pos\":[");
    for (int i = 0; i < d.Positions.Count; i++) { var p = d.Positions[i]; sb.Append(i > 0 ? "," : "").Append($"{F(p.X)},{F(p.Y)},{F(p.Z)}"); }
    sb.Append("],\"nrm\":[");
    for (int i = 0; i < d.Normals.Count; i++) { var p = d.Normals[i]; sb.Append(i > 0 ? "," : "").Append($"{F(p.X)},{F(p.Y)},{F(p.Z)}"); }
    sb.Append("],\"uv\":[");
    for (int i = 0; i < d.Uvs.Count; i++) { var p = d.Uvs[i]; sb.Append(i > 0 ? "," : "").Append($"{F(p.X)},{F(p.Y)}"); }
    sb.Append("],\"sub\":[");
    for (int s = 0; s < d.SubmeshKeys.Count; s++)
        sb.Append(s > 0 ? "," : "").Append($"{{\"key\":\"{d.SubmeshKeys[s]}\",\"idx\":[{string.Join(",", d.SubmeshIndices[s])}]}}");
    sb.Append("]}");
}
sb.Append("}}");
File.WriteAllText(Path.Combine(outDir, "models.json"), sb.ToString());
Console.WriteLine($"palette colours: {pal.Count}");
