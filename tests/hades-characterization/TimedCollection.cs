using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 서버가 제 시각에 하는 일을 재는 시험들. 젠 주기·휘두르는 간격처럼 **벽시계로 재는 것**이 걸려
/// 있어서, 다른 시험과 나란히 돌면 서버가 CPU 를 못 받아 결과가 흔들린다. 한 묶음에 넣어 서로
/// 부딪히지 않게 한다.
/// </summary>
[CollectionDefinition(TimedCollection.Name)]
public sealed class TimedCollection
{
    public const string Name = "서버 시각에 기대는 시험";
}
