namespace Lod.Mobile.Core.World;

/// <summary>
/// 월드맵 위의 한 곳. <paramref name="PointX"/>·<paramref name="PointY"/> 는 그림 위의 점이고,
/// <paramref name="X"/>·<paramref name="Y"/> 는 도착한 맵에서 설 칸이다.
/// </summary>
/// <remarks>
/// 점은 원작 640x480 그림을 기준으로 적혀 있다(temuair.json 의 값이 X 78~533 · Y 33~398 이다).
/// 지금 서버가 가진 그림 field001.png 는 1283x962 이므로 그리는 쪽이 두 배로 얹어야 한다.
/// </remarks>
public sealed record WorldMapNode(string Name, int AreaId, int X, int Y, int PointX, int PointY);

/// <summary>한 장의 월드맵. 그림 이름과 그 위의 곳들.</summary>
public sealed record WorldMapInfo(string Field, int FieldNumber, IReadOnlyList<WorldMapNode> Nodes);
