namespace Lod.Mobile.Core.Ui;

/// <summary>
/// 새 앱이 나왔나 — 빌드 스크립트가 앱 안(<c>app-version.txt</c>)과 내려받기 페이지(<c>/download/version-ios.txt</c>)에
/// 같은 번호(만든 시각 <c>YYYYMMDDHHMM</c>)를 적는다. 페이지 번호가 더 크면 새로 받으라고 알린다(사용자 2026-10-03).
/// 번호를 못 읽으면(맥에서 띄운 판·페이지 없음) 알리지 않는다.
/// </summary>
public static class AppUpdate
{
    public const string DownloadPage = "https://lodgame.duckdns.org/download/";

    public static bool Outdated(string? mine, string? published) =>
        long.TryParse(mine?.Trim(), out long have) && long.TryParse(published?.Trim(), out long newest) && newest > have;
}
