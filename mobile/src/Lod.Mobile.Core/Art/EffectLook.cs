namespace Lod.Mobile.Core.Art;

/// <summary>
/// 이펙트 그림 하나를 생성기가 미리 훑어 둔 것 — 그려진 가장 아래 줄(<see cref="Bottom" />, 머리 이펙트 판단)과
/// 그 마법에 걸린 괴물을 물들일 색(0~255). 화면이 처음 쓸 때 픽셀마다 훑으면 폰에서 끊겼다.
/// </summary>
public sealed record EffectLook(int Bottom, byte Red, byte Green, byte Blue)
{
    /// <summary>scripts/build-client-effects.py 가 쓰는 <c>effects-look.txt</c>: "번호 바닥줄 빨강 초록 파랑", '#' 줄은 설명.</summary>
    public static IReadOnlyDictionary<int, EffectLook> Read(string text)
    {
        Dictionary<int, EffectLook> looks = [];

        foreach (string line in text.Split('\n'))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 5 || parts[0].StartsWith('#')
                || !int.TryParse(parts[0], out int number) || !int.TryParse(parts[1], out int bottom)
                || !byte.TryParse(parts[2], out byte red) || !byte.TryParse(parts[3], out byte green)
                || !byte.TryParse(parts[4], out byte blue))
            {
                continue;
            }

            looks[number] = new EffectLook(bottom, red, green, blue);
        }

        return looks;
    }
}
