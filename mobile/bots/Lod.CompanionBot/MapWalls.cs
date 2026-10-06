using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;

namespace Lod.CompanionBot;

/// <summary>
/// 맵의 벽 — 앱이 그리는 것과 같은 파일(<c>map번호.txt</c>, <c>tools/dat-extract layout</c>)을 읽는다. 서버와 같은 .map 에서
/// 나와 벽이 맞는다. 파일이 없는 맵은 벽이 없는 것으로 친다(걸음이 막히면 서버가 제자리로 돌려보낸다).
/// </summary>
public sealed class MapWalls(string folder)
{
    private readonly Dictionary<int, MapLayout?> _read = [];

    public Func<Tile, bool> For(int mapId)
    {
        MapLayout? layout;

        // 생태계 봇은 봇 여럿이 이것 하나를 함께 쓴다 — Dictionary 를 여러 스레드가 동시에 고치면 망가진다.
        lock (_read)
        {
            if (!_read.TryGetValue(mapId, out layout))
            {
                string path = Path.Combine(folder, $"map{mapId}.txt");
                layout = folder.Length > 0 && File.Exists(path) ? MapLayout.Read(File.ReadAllText(path)) : null;
                _read[mapId] = layout;
            }
        }

        return layout is null ? _ => false : layout.Blocks;
    }
}
