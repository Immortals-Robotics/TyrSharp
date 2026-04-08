using MemoryPack;
using Tyr.Common.Debug.Drawing.Drawables;
using Path = Tyr.Common.Debug.Drawing.Drawables.Path;

namespace Tyr.Common.Debug.Drawing;

[MemoryPackable]
[MemoryPackUnion(0, typeof(Arc))]
[MemoryPackUnion(1, typeof(Arrow))]
[MemoryPackUnion(2, typeof(Circle))]
[MemoryPackUnion(3, typeof(Line))]
[MemoryPackUnion(4, typeof(LineSegment))]
[MemoryPackUnion(5, typeof(Path))]
[MemoryPackUnion(6, typeof(Point))]
[MemoryPackUnion(7, typeof(Rectangle))]
[MemoryPackUnion(8, typeof(Robot))]
[MemoryPackUnion(9, typeof(Text))]
[MemoryPackUnion(10, typeof(Triangle))]
public partial interface IDrawable;